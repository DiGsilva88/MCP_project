
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OllamaSharp;
using Vendas.Agente;

//o agente arranca o servidor MCP como um programa filho e fala com ele
//por stdin/stdout
// A pasta do executável é ...\src\Vendas.Agente\bin\Debug\net10.0
// Subir quatro níveis dá ...\src, onde está também a pasta do servidor.

var caminhoServidor = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Vendas.Servidor"));

if (!Directory.Exists(caminhoServidor))
    throw new DirectoryNotFoundException($"Não encontrei o servidor em {caminhoServidor}");


var transporte = new StdioClientTransport(new StdioClientTransportOptions
{
    Name= "vendas",
    Command = "dotnet",
Arguments = ["run", "--project", caminhoServidor],
    //a ligação a BD é passada ao servidor. O agente só a reencaminha
    //nunca imprime nem envia nada ao modelo

EnvironmentVariables = new Dictionary<string, string?>
{
    ["VENDAS_SQL"] = Environment.GetEnvironmentVariable("VENDAS_SQL"),
},
});

McpClient mcp;
IList<McpClientTool> ferramentas;
try
{
    mcp = await McpClient.CreateAsync(transporte);
    ferramentas = await mcp.ListToolsAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine("Não consegui arrancar/ligar ao servidor MCP (Vendas.Servidor).");
    Console.Error.WriteLine("Isto acontece antes de o Ollama entrar em jogo — não é um problema do Ollama.");
    Console.Error.WriteLine($"Detalhe: {ex.Message}");
    return;
}
await using var _ = mcp;

foreach (var f in ferramentas)
    Console.WriteLine($"- {f.Name}: {f.Description}");

Console.WriteLine();
Console.WriteLine("Modo de utilização:");
Console.WriteLine("1) Perguntas livres (usa um modelo local via Ollama)");
Console.WriteLine("2) Menu fixo (sem modelo, chama as ferramentas diretamente)");
Console.Write("> ");
if (Console.ReadLine()?.Trim() == "2")
{
    await ModoMenuAsync(mcp);
    return;
}

////--------Bloco 2
////o modelo funções que tornam isto um agente
////executa as ferramentas que o modelo pede e devolve o resultado

var http = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(10),   // modelo local é lento a arrancar
};

IChatClient ollama = new OllamaApiClient(http, "qwen2.5");
IChatClient modelo = ollama
    .AsBuilder()
    .UseFunctionInvocation(null, c => c.MaximumIterationsPerRequest = 5)
    .Build();


// //bloco 3 - regras (políticas de segurança em PoliticasSeguranca.cs)

var regras = PoliticasSeguranca.Regras;


//--bloco 4 - o ciclo de resposta, lê no teclado, pergunta ao modelo e imprime a resposta

var opcoes = new ChatOptions { Tools = [.. ferramentas]};
List<ChatMessage> historico = [new(ChatRole.System, regras)];

Console.WriteLine(" Escreva a sua pergunta ( Enter vazio termina).");

while(true)
{
    Console.Write("> ");
    var pergunta = Console.ReadLine();
    if(string.IsNullOrWhiteSpace(pergunta)) break;

    historico.Add(new(ChatRole.User, pergunta));

    Console.WriteLine("A pensar... (o modelo local pode demorar a arrancar)");

    ChatResponse resposta;
    try
    {
        resposta = await modelo.GetResponseAsync(historico, opcoes);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("O Ollama não respondeu ou devolveu um erro.");
        Console.Error.WriteLine("Confirme que o serviço está a funcionar (ollama server) e que o modelo está conectado");
        Console.Error.WriteLine($"Detalhe: {ex.Message}");
        historico.RemoveAt(historico.Count - 1); // não guarda a pergunta sem resposta
        continue;
    }

    //transparencia : mostra que ferramentas foram usadas na resposta

foreach(var chamada in resposta.Messages
    .SelectMany(m => m.Contents)
    .OfType<FunctionCallContent>())
Console.WriteLine($"[usou {chamada.Name}]");

Console.WriteLine(resposta.Text);
historico.AddMessages(resposta);

//regras +ultimas 20 mensagens
//para evitar que as respostas fiquem caras

if(historico.Count > 21)
    historico = [historico[0], ..historico[^20..]];

    while (historico.Count > 1 && historico[1].Role == ChatRole.Tool)
    historico.RemoveAt(1);
}

// ---- Modo menu: sem modelo, chama as ferramentas MCP diretamente a partir de escolhas numeradas.
// As colunas abaixo espelham os enums ColunaCliente/ColunaFaturacao do Vendas.Servidor
// (o agente não referencia o projeto do servidor, só fala com ele por MCP).

async Task ModoMenuAsync(McpClient mcp)
{
    string[] colunasCliente = ["Zona", "Vendedor", "TipoCliente", "Actividade", "Distrito"];
    string[] colunasFaturacao = ["Pagamento", "Cobranca", "Expedicao", "SituacaoFinanceira", "EscalaoPlafond", "EscalaoVolumeVendas"];

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("1) Consultar clientes");
        Console.WriteLine("2) Consultar faturação");
        Console.WriteLine("3) Sair");
        Console.Write("> ");
        switch (Console.ReadLine()?.Trim())
        {
            case "1": await ConsultarAsync(mcp, "clientes_consultar", colunasCliente, permiteCruzar: true); break;
            case "2": await ConsultarAsync(mcp, "faturacao_consultar", colunasFaturacao, permiteCruzar: true); break;
            case "3" or null or "": return;
            default: Console.WriteLine("Opção inválida."); break;
        }
    }
}

async Task ConsultarAsync(McpClient mcp, string ferramenta, IReadOnlyList<string> colunas, bool permiteCruzar)
{
    var coluna = EscolherColuna("Coluna (Enter = todas as colunas):", colunas);

    string? valor = null;
    var contar = false;
    string? cruzarCom = null;
    string? valorCruzado = null;

    if (coluna is not null)
    {
        Console.Write("Contar clientes por esta coluna? (s/n): ");
        contar = (Console.ReadLine()?.Trim() ?? "").Equals("s", StringComparison.OrdinalIgnoreCase);

        if (!contar)
        {
            Console.Write("Filtrar por um valor? (s/n): ");
            if ((Console.ReadLine()?.Trim() ?? "").Equals("s", StringComparison.OrdinalIgnoreCase))
                valor = EscolherValor($"Valor de {coluna}:", await ValoresAsync(mcp, ferramenta, coluna));
        }
        else if (permiteCruzar)
        {
            var outrasColunas = colunas.Where(c => c != coluna).ToArray();
            Console.Write("Cruzar com outra coluna para filtrar a contagem? (s/n): ");
            if ((Console.ReadLine()?.Trim() ?? "").Equals("s", StringComparison.OrdinalIgnoreCase))
            {
                cruzarCom = EscolherColuna("Cruzar com:", outrasColunas);
                if (cruzarCom is not null)
                    valorCruzado = EscolherValor($"Valor de {cruzarCom}:", await ValoresAsync(mcp, ferramenta, cruzarCom));
            }
        }
    }

    var argumentos = new Dictionary<string, object?>();
    if (coluna is not null) argumentos["coluna"] = coluna;
    if (!string.IsNullOrWhiteSpace(valor)) argumentos["valor"] = valor;
    if (contar) argumentos["contar"] = true;
    if (cruzarCom is not null) argumentos["cruzarCom"] = cruzarCom;
    if (!string.IsNullOrWhiteSpace(valorCruzado)) argumentos["valorCruzado"] = valorCruzado;

    Console.WriteLine("A consultar...");
    try
    {
        var resultado = await mcp.CallToolAsync(ferramenta, argumentos, cancellationToken: default);
        foreach (var bloco in resultado.Content.OfType<TextContentBlock>())
            Console.WriteLine(bloco.Text);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Falha ao chamar {ferramenta}: {ex.Message}");
    }
}

// Valores reais da coluna, pedidos à própria tool com contar=true (mesmo CSV que o modelo recebe).
async Task<IReadOnlyList<string>> ValoresAsync(McpClient mcp, string ferramenta, string coluna)
{
    var argumentos = new Dictionary<string, object?> { ["coluna"] = coluna, ["contar"] = true, ["limite"] = 100 };
    var resultado = await mcp.CallToolAsync(ferramenta, argumentos, cancellationToken: default);
    var texto = string.Concat(resultado.Content.OfType<TextContentBlock>().Select(b => b.Text));
    var linhas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // linha 0 é o título ("Clientes por X:..."), linha 1 é o cabeçalho do CSV; o resto são os valores.
    // ponytail: split pela primeira vírgula não trata valores com vírgula (ex.: "Bento, Filhos") — raro nestas colunas.
    return linhas.Skip(2).Where(l => !l.StartsWith('#')).Select(l => l.Split(',')[0].Trim()).ToArray();
}

// Enter -> null (sem filtro). Escolha inválida -> pergunta outra vez.
string? EscolherValor(string titulo, IReadOnlyList<string> valores)
{
    if (valores.Count == 0)
    {
        Console.WriteLine("Sem valores para filtrar nesta coluna.");
        return null;
    }

    while (true)
    {
        Console.WriteLine(titulo);
        for (var i = 0; i < valores.Count; i++)
            Console.WriteLine($"{i + 1}) {valores[i]}");
        Console.Write("> (Enter = não filtrar) ");

        var escolha = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(escolha)) return null;

        if (int.TryParse(escolha, out var indice) && indice >= 1 && indice <= valores.Count)
            return valores[indice - 1];

        Console.WriteLine("Opção inválida.");
    }
}

// Enter -> null (sem coluna). Escolha inválida -> pergunta outra vez.
string? EscolherColuna(string titulo, IReadOnlyList<string> colunas)
{
    while (true)
    {
        Console.WriteLine(titulo);
        for (var i = 0; i < colunas.Count; i++)
            Console.WriteLine($"{i + 1}) {colunas[i]}");
        Console.Write("> ");

        var escolha = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(escolha)) return null;

        if (int.TryParse(escolha, out var indice) && indice >= 1 && indice <= colunas.Count)
            return colunas[indice - 1];

        Console.WriteLine("Opção inválida.");
    }
}

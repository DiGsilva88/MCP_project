
using System.Diagnostics;
using System.Text;
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


var dllServidor = Path.Combine(caminhoServidor, "bin", "Debug", "net10.0", "Vendas.Servidor.dll");

var transporte = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "vendas",
    Command = "dotnet",
    // dll já compilado arranca em <1 s; "dotnet run" reavalia/compila sempre. Se não houver dll, cai no run.
    // ponytail: dll pode ficar desatualizada se o servidor mudar — voltar a fazer build do Vendas.Servidor.
    Arguments = File.Exists(dllServidor) ? [dllServidor] : ["run", "--project", caminhoServidor],
    // a ligação à BD é passada ao servidor. O agente só a reencaminha,
    // nunca a imprime nem a envia ao modelo
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

var http = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(10),   // modelo local é lento a arrancar
};

IChatClient ollama = new OllamaApiClient(http, "qwen2.5:7b");
IChatClient modelo = ollama
    .AsBuilder()
    .UseFunctionInvocation(null, c => c.MaximumIterationsPerRequest = 5)
    .Build();

// Opções comuns: modelo fica carregado 30 min (sem re-load a frio) e respostas determinísticas.
var opcoes = new ChatOptions
{
    Temperature = 0,
    RawRepresentationFactory = o => new OllamaSharp.Models.Chat.ChatRequest { KeepAlive = "30m" },
};

var regras = PoliticasSeguranca.Regras;

// Aquece o Ollama enquanto o utilizador escolhe o modo: carrega o modelo e o prefixo do prompt (regras).
// Falhas ignoram-se: a 1.ª pergunta real mostra o erro.
var aquecimento = Task.Run(async () =>
{
    try
    {
        await ollama.GetResponseAsync([new(ChatRole.System, regras), new(ChatRole.User, "ola")],
            new ChatOptions { MaxOutputTokens = 1, RawRepresentationFactory = opcoes.RawRepresentationFactory });
    }
    catch { }
});

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

//--bloco 4 - o ciclo de resposta, lê no teclado, pergunta ao modelo e imprime a resposta

opcoes.Tools = [.. ferramentas];
List<ChatMessage> historico = [new(ChatRole.System, regras)];

Console.WriteLine(" Escreva a sua pergunta ( Enter vazio termina).");

while(true)
{
    Console.Write("> ");
    var pergunta = Console.ReadLine();
    if(string.IsNullOrWhiteSpace(pergunta)) break;

    historico.Add(new(ChatRole.User, pergunta));

    var relogio = Stopwatch.StartNew();
    var texto = new StringBuilder();
    long? primeiroToken = null;
    try
    {
        // streaming: o texto aparece à medida que o modelo o gera (o tempo até ao 1.º token é o que o utilizador sente)
        await foreach (var parte in modelo.GetStreamingResponseAsync(historico, opcoes))
        {
            foreach (var chamada in parte.Contents.OfType<FunctionCallContent>())
                Console.WriteLine($"[usou {chamada.Name}]"); // transparência: ferramentas usadas
            if (parte.Text.Length == 0) continue;
            primeiroToken ??= relogio.ElapsedMilliseconds;
            Console.Write(parte.Text);
            texto.Append(parte.Text);
        }
        Console.WriteLine();
        Console.WriteLine($"[1.º token {primeiroToken ?? 0} ms · total {relogio.ElapsedMilliseconds} ms]");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("O Ollama não respondeu ou devolveu um erro.");
        Console.Error.WriteLine("Confirme que o serviço está a funcionar (ollama server) e que o modelo está conectado");
        Console.Error.WriteLine($"Detalhe: {ex.Message}");
        historico.RemoveAt(historico.Count - 1); // não guarda a pergunta sem resposta
        continue;
    }

    // Guarda só pergunta + resposta final: os CSVs das ferramentas não voltam a ser enviados ao modelo
    // (cada token do histórico é reprocessado em todas as perguntas). Regras + últimas 5 trocas.
    historico.Add(new(ChatRole.Assistant, texto.ToString()));
    if (historico.Count > 11)
        historico = [historico[0], .. historico[^10..]];
}

// ---- Modo menu: sem modelo, chama a ferramenta MCP "consultar" diretamente a partir de escolhas numeradas.
// As colunas abaixo espelham o enum Campo do Vendas.Servidor (ficha do cliente + faturação)
// (o agente não referencia o projeto do servidor, só fala com ele por MCP).

async Task ModoMenuAsync(McpClient mcp)
{
    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("1) Consultar clientes");
        Console.WriteLine("2) Sair");
        Console.Write("> ");
        switch (Console.ReadLine()?.Trim())
        {
            case "1": await ConsultarAsync(mcp); break;
            case "2" or null or "": return;
            default: Console.WriteLine("Opção inválida."); break;
        }
    }
}

async Task ConsultarAsync(McpClient mcp)
{
    string[] colunas =
    [
        "Zona", "Localidade", "Vendedor", "TipoCliente", "Actividade", "Distrito",
        "Pagamento", "Cobranca", "Expedicao", "SituacaoFinanceira", "EscalaoPlafond", "EscalaoVolumeVendas",
    ];

    var argumentos = new Dictionary<string, object?>();

    var coluna = Escolher("Coluna (Enter = todas as colunas):", colunas);
    if (coluna is not null)
    {
        argumentos["coluna"] = coluna;

        if (PerguntarSimNao("Contar clientes por esta coluna?"))
            argumentos["contar"] = true;
        else if (PerguntarSimNao("Filtrar por um valor?"))
            argumentos["valor"] = Escolher($"Valor de {coluna}:", await ValoresAsync(mcp, coluna));

        // Cruzamento: a segunda coluna pode ser de qualquer view (ex.: contar por Zona só de quem paga a 30 dias).
        if (PerguntarSimNao("Cruzar com outra coluna?"))
        {
            var cruzarCom = Escolher("Cruzar com:", colunas.Where(c => c != coluna).ToArray());
            var valorCruzado = cruzarCom is null ? null : Escolher($"Valor de {cruzarCom}:", await ValoresAsync(mcp, cruzarCom));
            if (valorCruzado is not null)
            {
                argumentos["cruzarCom"] = cruzarCom;
                argumentos["valorCruzado"] = valorCruzado;
            }
        }
    }

    Console.WriteLine("A consultar...");
    try
    {
        var resultado = await mcp.CallToolAsync("consultar", argumentos);
        foreach (var bloco in resultado.Content.OfType<TextContentBlock>())
            Console.WriteLine(bloco.Text);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Falha ao consultar: {ex.Message}");
    }
}

// Valores reais da coluna, pedidos à própria tool com contar=true (mesmo CSV que o modelo recebe).
async Task<IReadOnlyList<string>> ValoresAsync(McpClient mcp, string coluna)
{
    var argumentos = new Dictionary<string, object?> { ["coluna"] = coluna, ["contar"] = true, ["limite"] = 100 };
    var resultado = await mcp.CallToolAsync("consultar", argumentos);
    var texto = string.Concat(resultado.Content.OfType<TextContentBlock>().Select(b => b.Text));
    var linhas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // linha 0 é o título ("Clientes por X: ..."), linha 1 é o cabeçalho do CSV; o resto são os valores.
    // ponytail: split pela primeira vírgula não trata valores com vírgula (ex.: "Bento, Filhos") — raro nestas colunas.
    return linhas.Skip(2).Where(l => !l.StartsWith('#')).Select(l => l.Split(',')[0].Trim()).ToArray();
}

bool PerguntarSimNao(string pergunta)
{
    Console.Write($"{pergunta} (s/n): ");
    return (Console.ReadLine()?.Trim() ?? "").Equals("s", StringComparison.OrdinalIgnoreCase);
}

// Enter -> null (não escolher). Escolha inválida -> pergunta outra vez.
string? Escolher(string titulo, IReadOnlyList<string> opcoes)
{
    if (opcoes.Count == 0)
    {
        Console.WriteLine("Sem valores para escolher.");
        return null;
    }

    while (true)
    {
        Console.WriteLine(titulo);
        for (var i = 0; i < opcoes.Count; i++)
            Console.WriteLine($"{i + 1}) {opcoes[i]}");
        Console.Write("> (Enter = nenhum) ");

        var escolha = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(escolha)) return null;

        if (int.TryParse(escolha, out var indice) && indice >= 1 && indice <= opcoes.Count)
            return opcoes[indice - 1];

        Console.WriteLine("Opção inválida.");
    }
}

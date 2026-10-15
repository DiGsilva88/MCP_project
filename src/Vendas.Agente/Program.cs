
using System.Diagnostics;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OllamaSharp;
using Vendas.Agente;

//o agente arranca o servidor MCP como um programa filho e fala com ele
//por stdin/stdout
// A pasta do executável é ...\src\Vendas.Agente\bin\<Configuração>\net10.0
// Subir quatro níveis dá ...\src, onde está também a pasta do servidor.
// O servidor é compilado com o agente (ProjectReference no csproj), na mesma configuração (Debug/Release).

var caminhoServidor = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Vendas.Servidor"));
var configuracao = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)));
var dllServidor = Path.Combine(caminhoServidor, "bin", configuracao!, "net10.0", "Vendas.Servidor.dll");

if (!File.Exists(dllServidor))
    throw new FileNotFoundException($"Não encontrei o servidor compilado em {dllServidor}. Compile a solução (dotnet build Vendas.slnx).");

var transporte = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "vendas",
    Command = "dotnet",
    Arguments = [dllServidor],
    // o log do servidor (inclui a exceção real quando a tool devolve "não foi possível consultar")
    StandardErrorLines = linha => Console.Error.WriteLine($"[servidor] {linha}"),
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

var chamadas = 0; // chamadas a ferramentas que devolveram dados na pergunta em curso (ver PoliticasSeguranca.RespostaSemFonte)
var caracteresFerramentas = 0; // texto devolvido pelas ferramentas na pergunta em curso

// Teto de texto das ferramentas por pergunta (~3 letras por token): regras + esquemas + isto cabem em NumCtx.
// Acima disto o Ollama cortaria o início do prompt, que são as regras de segurança.
const int MaxCaracteresFerramentas = 12_000;

IChatClient ollama = new OllamaApiClient(http, Environment.GetEnvironmentVariable("OLLAMA_MODELO") ?? "qwen2.5:7b");
IChatClient modelo = ollama
    .AsBuilder()
    .UseFunctionInvocation(null, c =>
    {
        c.MaximumIterationsPerRequest = 5;
        // tempo de cada chamada à ferramenta (MCP + SQL), para saber de onde vem a demora
        c.FunctionInvoker = async (ctx, ct) =>
        {
            var t = Stopwatch.StartNew();
            var resultado = await ctx.Function.InvokeAsync(ctx.Arguments, ct);
            var args = string.Join(", ", ctx.Arguments.Select(a => $"{a.Key}={a.Value}"));
            var texto = PoliticasSeguranca.TextoDoResultado(resultado);
            // dados pessoais (NIF, contactos) não ficam no ecrã/scrollback: só o tamanho
            var amostra = ctx.Function.Name == "consultar_sensivel"
                ? $"({texto.Length} caracteres)"
                : texto[..Math.Min(texto.Length, 120)].ReplaceLineEndings(" ");
            Console.WriteLine($"[{ctx.Function.Name}({args}) {t.ElapsedMilliseconds} ms] {amostra}");

            caracteresFerramentas += texto.Length;
            if (caracteresFerramentas > MaxCaracteresFerramentas)
                return "Resultado demasiado grande. Peça menos linhas ou filtre por um valor.";

            // Erro ou recusa não é fonte: a resposta seguinte continua a ser validada.
            if (PoliticasSeguranca.ResultadoComDados(texto)) chamadas++;
            return resultado;
        };
    })
    .Build();

// Opções comuns: modelo fica carregado em memória para sempre (-1m = sem expirar, sem re-load a frio) e respostas determinísticas.
// NumCtx fixo: um prompt maior que a janela faz o Ollama cortar o início (as regras) sem avisar.
// Tem de ser igual no aquecimento, senão o Ollama recarrega o modelo.
var opcoes = new ChatOptions
{
    Temperature = 0,
    RawRepresentationFactory = o => new OllamaSharp.Models.Chat.ChatRequest
    {
        KeepAlive = "-1m",
        Options = new OllamaSharp.Models.RequestOptions { NumCtx = 8192 },
    },
};

var regras = PoliticasSeguranca.Regras;

// Aquece o Ollama enquanto o utilizador escolhe o modo: carrega o modelo e o prefixo do prompt
// (regras + esquema das ferramentas). Usa o cliente sem invocação de ferramentas.
// Falhas ignoram-se: a 1.ª pergunta real mostra o erro.
var aquecimento = Task.Run(async () =>
{
    try
    {
        await ollama.GetResponseAsync([new(ChatRole.System, regras), new(ChatRole.User, "ola")],
            new ChatOptions
            {
                MaxOutputTokens = 1,
                Tools = [.. ferramentas],
                RawRepresentationFactory = opcoes.RawRepresentationFactory,
            });
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

Console.WriteLine(" Escreva a sua pergunta ( Enter vazio termina).");

while(true)
{
    Console.Write("> ");
    var pergunta = Console.ReadLine();
    if(string.IsNullOrWhiteSpace(pergunta)) break;

    // Sem histórico: cada pergunta é só regras + pergunta. Em CPU cada token do prompt custa tempo,
    // e o início (regras + ferramentas) fica igual em todos os pedidos, por isso o Ollama reaproveita-o.
    // shortcut: perde "mostra mais" / "e desses…"; voltar a guardar a última troca se houver GPU.
    List<ChatMessage> historico = [new(ChatRole.System, regras), new(ChatRole.User, pergunta)];

    var relogio = Stopwatch.StartNew();
    var partes = new List<ChatResponseUpdate>();
    long? primeiroToken = null;
    try
    {
        // Sem chamada a ferramenta, o texto só é mostrado depois de validado: uma resposta com números
        // mas sem fonte é inventada, por isso é descartada e repete-se uma vez.
        // Depois de uma chamada já não há nada a validar e o texto aparece à medida que chega.
        for (var tentativa = 0; ; tentativa++)
        {
            chamadas = 0;
            caracteresFerramentas = 0;
            partes.Clear();
            var aMostrar = false;
            await foreach (var parte in modelo.GetStreamingResponseAsync(historico, opcoes))
            {
                partes.Add(parte);
                if (parte.Text.Length == 0) continue;
                primeiroToken ??= relogio.ElapsedMilliseconds;
                if (aMostrar) Console.Write(parte.Text);
                else if (chamadas > 0)
                {
                    aMostrar = true;
                    Console.Write(string.Concat(partes.Select(p => p.Text))); // inclui o texto anterior à chamada
                }
            }
            if (aMostrar) break;

            var texto = partes.ToChatResponse().Text;
            if (!PoliticasSeguranca.RespostaSemFonte(texto, chamadas)) { Console.Write(texto); break; }

            if (tentativa == 1)
            {
                partes.Clear(); // cai na mensagem "não chegou a dar uma resposta" abaixo
                break;
            }
            historico.Add(new(ChatRole.System, "Respondeste sem chamar nenhuma ferramenta. Chama a ferramenta adequada e responde só com o que ela devolver."));
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("O Ollama não respondeu ou devolveu um erro.");
        Console.Error.WriteLine("Confirme que o serviço está a funcionar (ollama server) e que o modelo está conectado");
        Console.Error.WriteLine($"Detalhe: {ex.Message}");
        continue;
    }

    // Tokens lidos = soma dos pedidos desta pergunta (escolher a ferramenta + responder). Em CPU é o que mais pesa.
    var resposta = partes.ToChatResponse();
    Console.WriteLine();
    Console.WriteLine($"[1.º token {(primeiroToken is { } ms ? $"{ms} ms" : "n/d")} · total {relogio.ElapsedMilliseconds} ms" +
                      $" · tokens lidos {resposta.Usage?.InputTokenCount?.ToString() ?? "n/d"}]");

    // O ciclo de ferramentas pode esgotar-se (MaximumIterationsPerRequest) sem texto final.
    if (string.IsNullOrWhiteSpace(resposta.Messages.LastOrDefault()?.Text))
        Console.WriteLine("O modelo não chegou a dar uma resposta. Reformule a pergunta.");
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


using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
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

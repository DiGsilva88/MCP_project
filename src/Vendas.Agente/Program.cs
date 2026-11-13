
using System.Diagnostics;
using Anthropic;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OllamaSharp;
using Vendas.Agente;

// O agente arranca o servidor MCP como um programa filho e fala com ele por stdin/stdout.

// A pasta do executável é ...\src\Vendas.Agente\bin\<Configuração>\net10.0
// Subir quatro níveis dá ...\src, onde está também a pasta do servidor.
// O servidor é compilado com o agente (ProjectReference no csproj), na mesma configuração (Debug/Release).

var caminhoServidor = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Vendas.Servidor"));
var configuracao = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)));
// Num pacote (scripts/publicar.ps1) o servidor vem na subpasta "servidor"; VENDAS_SERVIDOR_DLL serve para o ter noutro sítio.
var servidorNoPacote = Path.Combine(AppContext.BaseDirectory, "servidor", "Vendas.Servidor.dll");
var dllServidor = Environment.GetEnvironmentVariable("VENDAS_SERVIDOR_DLL")
    ?? (File.Exists(servidorNoPacote) ? servidorNoPacote : Path.Combine(caminhoServidor, "bin", configuracao!, "net10.0", "Vendas.Servidor.dll"));

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
        ["VENDAS_DEMO"] = Environment.GetEnvironmentVariable("VENDAS_DEMO"),
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

    if (args.Contains("--web"))
    {
        Console.Error.WriteLine("Carregue Enter para fechar");
        Console.ReadLine();
    }
    return;
}
await using var fecharMcp = mcp; // fecha o servidor filho à saída

var http = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(10),   // modelo local é lento a arrancar
};

// Avaliações e agendamentos do email: na pasta do utilizador, fora do repositório.
var pastaDados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vendas");
var ficheiroAgendamentos = Path.Combine(pastaDados, "agendamentos.json");

// Resumos já montados, por dia + período + cliente + zona: cada um custa ~15 s à BD e os dados mudam pouco.
// Guarda a Task: um pedido que chega a meio (ex.: o pré-carregamento do arranque) espera pela mesma consulta.
// shortcut: só em memória, 10 min de validade; encurtar se a view passar a ser atualizada ao longo do dia.
var resumos = new Dictionary<string, (DateTime Feito, Task<Dictionary<string, string>> Partes)>();

// Tarefa agendada das segundas: envia os resumos devidos e sai (sem Ollama: os números vêm só das ferramentas).
if (args.Contains("--enviar-resumo"))
{
    Environment.ExitCode = await EnviarResumosAsync();
    return;
}

var chamadas = 0; // chamadas a ferramentas que devolveram dados na pergunta em curso (ver PoliticasSeguranca.RespostaSemFonte)
var caracteresFerramentas = 0; // texto devolvido pelas ferramentas na pergunta em curso
string? textoDireto = null; // ranking mostrado tal como veio do servidor (ver PoliticasSeguranca.MostrarDireto)
var perguntaAtual = ""; // os filtros do modelo têm de vir daqui ou de um resultado (ver PoliticasSeguranca.ValorInventado)
var resultadosPergunta = new System.Text.StringBuilder();

// Teto de texto das ferramentas por pergunta (~3 letras por token): regras + esquemas + isto cabem em NumCtx.
// Acima disto o Ollama cortaria o início do prompt, que são as regras de segurança.
const int MaxCaracteresFerramentas = 12_000;

// Sem AGENTE_LLM=claude fica o Ollama: os dados só saem da máquina se alguém o pedir.
var usarClaude = string.Equals(Environment.GetEnvironmentVariable("AGENTE_LLM"), "claude", StringComparison.OrdinalIgnoreCase);

// As ferramentas que o modelo vê. A mesma lista no aquecimento e nas perguntas: o Ollama reaproveita esse prefixo.
var paraModelo = ferramentas
    .Where(f => !PoliticasSeguranca.SoDoPainel(f.Name) && (!usarClaude || PoliticasSeguranca.PodeIrParaCloud(f.Name)))
    .ToList();

// 4b instruct (sem "thinking"): o 7b é demasiado pesado para o CPU desta máquina
IChatClient llm = usarClaude
    ? new AnthropicClient().AsIChatClient(Environment.GetEnvironmentVariable("CLAUDE_MODELO") ?? "claude-opus-5-5")
    : new OllamaApiClient(http, Environment.GetEnvironmentVariable("OLLAMA_MODELO") ?? "qwen3:4b-instruct");
IChatClient modelo = llm
    .AsBuilder()
    .UseFunctionInvocation(null, c =>
    {
        c.MaximumIterationsPerRequest = 5;
        // tempo de cada chamada à ferramenta (MCP + SQL), para saber de onde vem a demora
        c.FunctionInvoker = async (ctx, ct) =>
        {
            var t = Stopwatch.StartNew();
            // Os argumentos da sensível trazem nomes de clientes: ficam fora da consola, como os resultados.
            var argumentos = ctx.Function.Name == "consultar_sensivel"
                ? "…"
                : string.Join(", ", ctx.Arguments.Select(a => $"{a.Key}={a.Value}"));

            // Valor fora do esquema: corrige-se aqui, sem ir ao servidor (lá daria um erro genérico).
            if (PoliticasSeguranca.ArgumentoInvalido(ctx.Function.JsonSchema, ctx.Arguments) is { } invalido)
            {
                Console.WriteLine($"[{ctx.Function.Name}({argumentos}) recusado] {invalido}");
                return invalido;
            }
            if ((PoliticasSeguranca.ValorInventado(perguntaAtual, resultadosPergunta.ToString(), ctx.Arguments)
                 ?? PoliticasSeguranca.ContarEmVezDeVendas(ctx.Function.Name, perguntaAtual, ctx.Arguments)) is { } inventado)
            {
                Console.WriteLine($"[{ctx.Function.Name}({argumentos}) recusado] {inventado}");
                return inventado;
            }
            if (PoliticasSeguranca.PeriodoEmFalta(ctx.Function.Name, perguntaAtual, ctx.Arguments) is { } periodo)
                ctx.Arguments["periodo"] = periodo;
            if (PoliticasSeguranca.CorrigirVendas(ctx.Function.Name, perguntaAtual, ctx.Arguments) is { } corrigido)
                Console.WriteLine($"[{ctx.Function.Name}({argumentos}) corrigido] {corrigido}");

            var resultado = await ctx.Function.InvokeAsync(ctx.Arguments, ct);
            var texto = PoliticasSeguranca.TextoDoResultado(resultado);
            resultadosPergunta.AppendLine(texto);
            // dados pessoais (NIF, contactos) não ficam no ecrã/scrollback: só o tamanho
            var amostra = ctx.Function.Name == "consultar_sensivel"
                ? $"({texto.Length} caracteres)"
                : texto[..Math.Min(texto.Length, 120)].ReplaceLineEndings(" ");
            Console.WriteLine($"[{ctx.Function.Name}({argumentos}) {t.ElapsedMilliseconds} ms] {amostra}");

            // Ranking pronto: termina o ciclo aqui, sem 2.º pedido ao modelo; o ResponderAsync mostra-o.
            if (PoliticasSeguranca.MostrarDireto(ctx.Function.Name, texto))
            {
                // Duas chamadas no mesmo turno (ex.: por mês e por localidade): mostra as duas.
                textoDireto = textoDireto is null ? texto : $"{textoDireto}\n{texto}";
                ctx.Terminate = true;
                chamadas++;
                return resultado;
            }

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
// A Claude exige MaxOutputTokens e recusa Temperature com erro 400.
var opcoes = usarClaude
    ? new ChatOptions { MaxOutputTokens = 16_000 }
    : new ChatOptions
    {
        Temperature = 0,
        RawRepresentationFactory = o => new OllamaSharp.Models.Chat.ChatRequest
        {
            KeepAlive = "-1m",
            Options = new OllamaSharp.Models.RequestOptions { NumCtx = 8192 },
        },
    };

// Calculado a cada pergunta: o modo web fica aberto de um dia para o outro.
static string Regras() => PoliticasSeguranca.Regras + $"\nHoje é {DateTime.Today:dd-MM-yyyy}.";

// Aquece o Ollama enquanto o utilizador escolhe o modo: carrega o modelo e o prefixo do prompt
// (regras + esquema das ferramentas). Usa o cliente sem invocação de ferramentas.
// Falhas ignoram-se: a 1.ª pergunta real mostra o erro. Na Claude não há nada a aquecer e cada pedido custa dinheiro.
var aquecimento = usarClaude ? Task.CompletedTask : Task.Run(async () =>
{
    try
    {
        await llm.GetResponseAsync([new(ChatRole.System, Regras()), new(ChatRole.User, "ola")],
            new ChatOptions
            {
                MaxOutputTokens = 1,
                Tools = [.. paraModelo],
                RawRepresentationFactory = opcoes.RawRepresentationFactory,
            });
    }
    catch { }
});

foreach (var f in ferramentas)
    Console.WriteLine($"- {f.Name}: {f.Description}");


var arrancarWeb = args.Contains("--web"); // lançado pelo botão do Visual
if (!arrancarWeb)
{
    Console.WriteLine();
    Console.WriteLine("Modo de utilização:");
    Console.WriteLine($"1) Perguntas livres (usa {(usarClaude ? "a Claude, na cloud" : "um modelo local via Ollama")})");
    Console.WriteLine("2) Menu fixo (sem modelo, chama as ferramentas diretamente)");
    Console.WriteLine($"3) Chat no browser (como o utilizador final, usa {(usarClaude ? "a Claude, na cloud" : "o modelo local")})");
    Console.Write("> ");
}
var modo = arrancarWeb ? "3" : Console.ReadLine()?.Trim();


if (modo == "2")
{
    await ModoMenuAsync(mcp);
    return;
}

// Ciclo de resposta: lê no teclado, pergunta ao modelo e imprime a resposta.

opcoes.Tools = [.. paraModelo];

if (modo == "3")
{
    await ModoWebAsync();
    return;
}

Console.WriteLine(" Escreva a sua pergunta ( Enter vazio termina).");

while (true)
{
    Console.Write("> ");
    var pergunta = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(pergunta)) break;
    await ResponderAsync(pergunta, texto => { Console.Write(texto); return Task.CompletedTask; });
}

// Responde a uma pergunta; "escrever" recebe o texto à medida que pode ser mostrado (consola ou browser).
// As estatísticas e os erros técnicos vão sempre para a consola, nunca para o utilizador.
async Task ResponderAsync(string pergunta, Func<string, Task> escrever, CancellationToken ct = default)
{
    // Sem histórico: cada pergunta é só regras + pergunta. Em CPU cada token do prompt custa tempo,
    // e o início (regras + ferramentas) fica igual em todos os pedidos, por isso o Ollama reaproveita-o.
    // shortcut: perde "mostra mais" / "e desses…"; voltar a guardar a última troca se houver GPU.
    List<ChatMessage> historico = [new(ChatRole.System, Regras()), new(ChatRole.User, pergunta)];
    perguntaAtual = pergunta;
    resultadosPergunta.Clear();

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
            textoDireto = null;
            partes.Clear();
            var aMostrar = false;
            await foreach (var parte in modelo.GetStreamingResponseAsync(historico, opcoes, ct))
            {
                partes.Add(parte);
                if (parte.Text.Length == 0) continue;
                primeiroToken ??= relogio.ElapsedMilliseconds;
                if (aMostrar) await escrever(parte.Text);
                else if (chamadas > 0)
                {
                    aMostrar = true;
                    await escrever(string.Concat(partes.Select(p => p.Text))); // inclui o texto anterior à chamada
                }
            }
            // O texto que o modelo escreveu antes da chamada (se houve) não se mostra: o ranking já diz tudo.
            if (textoDireto is not null)
            {
                primeiroToken ??= relogio.ElapsedMilliseconds;
                await escrever(textoDireto);
                break;
            }
            if (aMostrar) break;

            var texto = partes.ToChatResponse().Text;
            if (!PoliticasSeguranca.RespostaSemFonte(texto, chamadas)) { await escrever(texto); break; }

            if (tentativa == 1)
            {
                partes.Clear(); // cai na mensagem "não chegou a dar uma resposta" abaixo
                break;
            }
            historico.Add(new(ChatRole.System, "Respondeste sem chamar nenhuma ferramenta. Chama a ferramenta adequada e responde só com o que ela devolver."));
        }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        Console.WriteLine("[pergunta cancelada]"); // Parar ou nova pergunta: o browser já não espera resposta
        return;
    }
    // O browser fechou a meio (HttpListenerException ao escrever): não é falha do Ollama, sobe para o ModoWebAsync.
    catch (Exception ex) when (ex is not System.Net.HttpListenerException)
    {
        Console.Error.WriteLine("O modelo não respondeu ou devolveu um erro.");
        Console.Error.WriteLine(usarClaude
            ? "Confirme a variável ANTHROPIC_API_KEY, os créditos da conta e a ligação à internet."
            : "Confirme que o serviço está a funcionar (ollama server) e que o modelo está conectado");
        Console.Error.WriteLine($"Detalhe: {ex.Message}");
        await escrever("Não foi possível obter uma resposta neste momento. Tente novamente.");
        return;
    }

    // Tokens lidos = soma dos pedidos desta pergunta (escolher a ferramenta + responder). Em CPU é o que mais pesa.
    var resposta = partes.ToChatResponse();
    Console.WriteLine();
    Console.WriteLine($"[1.º token {(primeiroToken is { } ms ? $"{ms} ms" : "n/d")} · total {relogio.ElapsedMilliseconds} ms" +
                      $" · tokens lidos {resposta.Usage?.InputTokenCount?.ToString() ?? "n/d"}]");

    // O ciclo de ferramentas pode esgotar-se (MaximumIterationsPerRequest) sem texto final.
    if (textoDireto is null && string.IsNullOrWhiteSpace(resposta.Messages.LastOrDefault()?.Text))
        await escrever("O modelo não chegou a dar uma resposta. Reformule a pergunta.");
}

// ---- Modo web: a mesma ResponderAsync, servida numa página. Por omissão só em localhost, para uma pessoa.
// Em RDS corre uma só instância central: VENDAS_ENDERECO diz onde ouve (ver README).
// Cada pedido é sempre autenticado com o login do Windows: sem perfis.json só entra quem arrancou o agente.
// Pedidos em paralelo, mas uma pergunta ao modelo de cada vez (o Ollama em CPU também só responde a uma).
async Task ModoWebAsync()
{
    var enderecoFixo = Environment.GetEnvironmentVariable("VENDAS_ENDERECO");
    // Fora deste PC só HTTPS: em HTTP os dados iam em claro na rede, e o browser não manda o Sec-Fetch-Site (o painel dava 403).
    if (enderecoFixo is not null && !enderecoFixo.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("VENDAS_ENDERECO tem de começar por https:// (ver README). O painel não arrancou.");
        return;
    }
    var endereco = enderecoFixo ?? "http://localhost:5080/";
    var perfis = Perfis.Ler(Path.Combine(pastaDados, "perfis.json"));
    var dono = $"{Environment.UserDomainName}\\{Environment.UserName}";
    const int MaxPergunta = 500;

    using var servidor = new System.Net.HttpListener();
    servidor.Prefixes.Add(endereco);
    // O browser envia o login do Windows sozinho; quem não se autentica recebe 401 e nunca chega ao TratarAsync.
    servidor.AuthenticationSchemes = System.Net.AuthenticationSchemes.IntegratedWindowsAuthentication;
    try { servidor.Start(); }
    // Já há um painel aberto neste PC (segundo clique no atalho ou no botão do VB6): mostra esse. Numa instalação com endereço, a falha tem de se ver.
    catch (System.Net.HttpListenerException) when (enderecoFixo is null)
    {
        Process.Start(new ProcessStartInfo(endereco) { UseShellExecute = true });
        return;
    }
    // O painel abre no resumo deste ano: começa já, enquanto o browser abre (a falha aparece no 1.º pedido).
    var preCarregar = ResumoGuardadoAsync("AnoAtual", null, null, null);
    Console.WriteLine($"Chat em {endereco} (Ctrl+C termina). As chamadas às ferramentas aparecem aqui.");
    Console.WriteLine(perfis is null ? $"Sem perfis.json: só {dono} tem acesso." : $"Acesso pelo perfis.json ({perfis.Count} utilizadores).");
    // Com endereço definido é uma instalação sem ninguém a olhar para este ecrã.
    if (enderecoFixo is null) Process.Start(new ProcessStartInfo(endereco) { UseShellExecute = true });

    var umaPergunta = new SemaphoreSlim(1); // ResponderAsync usa estado partilhado (chamadas, textoDireto, perguntaAtual…)
    // Por pessoa: o Parar e uma pergunta nova de um utilizador não cancelam a de outro. Sem Dispose: Cancel() num CTS já descartado lançaria exceção.
    var perguntasEmCurso = new System.Collections.Concurrent.ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
    var bloqueioLogs = new object(); // auditoria.log e feedback.log: dois pedidos ao mesmo tempo não podem escrever juntos
    var api = new ApiPainel(
        async (nome, argumentos, ct) =>
        {
            var r = await mcp.CallToolAsync(nome, argumentos, cancellationToken: ct);
            return string.Concat(r.Content.OfType<TextContentBlock>().Select(b => b.Text));
        },
        (perfil, documento) =>
        {
            // Uma falha do disco não impede a consulta, mas fica na consola.
            try
            {
                Directory.CreateDirectory(pastaDados);
                lock (bloqueioLogs)
                    File.AppendAllText(Path.Combine(pastaDados, "auditoria.log"), ApiPainel.LinhaAuditoria(DateTimeOffset.Now, perfil.Login, documento) + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Não consegui escrever na auditoria: {ex.Message}");
            }
        });

    while (true)
    {
        var ctx = await servidor.GetContextAsync();
        // Cada pedido à parte: uma resposta do modelo (minutos em CPU) não prende o Resumo, o feedback nem o Parar.
        _ = Task.Run(() => TratarAsync(ctx));
    }

    async Task TratarAsync(System.Net.HttpListenerContext ctx)
    {
        var (pedido, resposta) = (ctx.Request, ctx.Response);
        try
        {
            var perfil = Perfis.Resolver(ctx.User?.Identity?.Name, perfis, dono);
            if (perfil is null)
            {
                resposta.StatusCode = 403;
                resposta.ContentType = "text/plain; charset=utf-8";
                await resposta.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Este utilizador ainda não tem acesso ao painel. Peça à equipa técnica para o acrescentar."));
                return;
            }

            if (pedido.HttpMethod == "GET" && pedido.Url?.AbsolutePath == "/")
            {
                resposta.ContentType = "text/html; charset=utf-8";
                resposta.Headers["Cache-Control"] = "no-cache"; // a página leva o nome do utilizador: valida sempre
                var HTML = System.Text.Encoding.UTF8.GetBytes(PaginaChat.Para(perfil.Login));
                if ((pedido.Headers["Accept-Encoding"] ?? "").Contains("gzip"))
                {
                    resposta.Headers["Content-Encoding"] = "gzip";
                    using var gzip = new System.IO.Compression.GZipStream(resposta.OutputStream, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true);
                    await gzip.WriteAsync(HTML);
                }
                else
                await resposta.OutputStream.WriteAsync(HTML);
            }
            // Só devolve recursos embutidos com este nome exato: um caminho com ../ simplesmente não existe.
            else if (pedido.HttpMethod == "GET" && pedido.Url?.AbsolutePath is { } caminho && (caminho.StartsWith("/fonts/") || caminho.StartsWith("/imagens/"))
                     && typeof(PaginaChat).Assembly.GetManifestResourceStream(caminho[1..]) is { } fonte)
            {
                using (fonte)
                {
                    resposta.ContentType = caminho.StartsWith("/imagens/") ? "image/png" : "font/woff2";
                    resposta.Headers["Cache-Control"] = "max-age=86400";
                    await fonte.CopyToAsync(resposta.OutputStream);
                }
            }
            // Outra página aberta no browser pode fazer POST para localhost sem CORS; o browser diz sempre a Origin.
            else if (pedido.HttpMethod == "POST" && pedido.Headers["Origin"] is { } origem && origem != endereco.TrimEnd('/'))
                resposta.StatusCode = 403;
            // Botão "Parar": cancelar só o fetch deixava o Ollama a gerar até ao fim.
            else if (pedido.HttpMethod == "POST" && pedido.Url?.AbsolutePath == "/parar")
            {
                if (perguntasEmCurso.TryGetValue(perfil.Login, out var emCurso)) emCurso.Cancel();
                resposta.StatusCode = 204;
            }
            // O modelo e o email da empresa veem todos os clientes: um perfil de vendedor fica pelo painel, que filtra.
            else if (perfil.Vendedor is not null && pedido.HttpMethod == "POST" && pedido.Url?.AbsolutePath is "/perguntar" or "/agendamentos")
            {
                resposta.StatusCode = 403;
                resposta.ContentType = "text/plain; charset=utf-8";
                await resposta.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Esta função não está disponível para o perfil de vendedor."));
            }
            else if (pedido.HttpMethod == "POST" && pedido.Url?.AbsolutePath == "/feedback")
            {
                if (await LerCorpoAsync(pedido, 200) is { } corpo && Feedback.Linha(corpo, DateTimeOffset.Now) is { } linha)
                {
                    Directory.CreateDirectory(pastaDados);
                    lock (bloqueioLogs) File.AppendAllText(Path.Combine(pastaDados, "feedback.log"), linha + Environment.NewLine);
                    resposta.StatusCode = 204;
                }
                else resposta.StatusCode = 400;
            }
            else if (pedido.HttpMethod == "POST" && pedido.Url?.AbsolutePath == "/agendamentos")
            {
                var (agendamento, erro) = await LerCorpoAsync(pedido, 300) is { } corpo ? Agendamentos.DoPedido(corpo) : (null, "Pedido inválido.");
                resposta.StatusCode = agendamento is null ? 400 : 200;
                // Sem isto, um erro do disco caía no catch de baixo e a página via 200: "agendado" sem estar.
                // Quem pediu fica guardado: qualquer perfil "*" pode inscrever qualquer endereço da empresa.
                try { if (agendamento is not null) Agendamentos.Guardar(ficheiroAgendamentos, agendamento = agendamento with { Criador = perfil.Login }); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    Console.Error.WriteLine($"Não consegui guardar o agendamento: {ex.Message}");
                    (agendamento, erro, resposta.StatusCode) = (null, "Não foi possível guardar o agendamento.", 500);
                }
                resposta.ContentType = "text/plain; charset=utf-8";
                await resposta.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(erro ?? (agendamento!.Freq == Agendamentos.Cancelar ? "Envio cancelado." : "Agendado.")));
                Console.WriteLine(agendamento is null ? "[agendamento recusado]" : $"[agendamento {agendamento.Freq} por {perfil.Login}]"); // sem o endereço
            }
            else if (pedido.HttpMethod == "POST" && pedido.Url?.AbsolutePath == "/perguntar")
            {
                var pergunta = (await LerCorpoAsync(pedido, MaxPergunta))?.Trim(); // null = longa demais
                resposta.ContentType = "text/plain; charset=utf-8";
                resposta.SendChunked = true; // o texto vai aparecendo no browser, como na consola

                if (string.IsNullOrEmpty(pergunta))
                {
                    resposta.StatusCode = 400;
                    await resposta.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"A pergunta tem de ter entre 1 e {MaxPergunta} caracteres."));
                }
                else
                {
                    Console.WriteLine($"> {pergunta}");
                    // A página só pergunta de novo depois de Parar ou recarregar: a pergunta anterior está abandonada.
                    var cancelar = new CancellationTokenSource();
                    perguntasEmCurso.AddOrUpdate(perfil.Login, cancelar, (_, anterior) => { anterior.Cancel(); return cancelar; });
                    await umaPergunta.WaitAsync();
                    try
                    {
                        await ResponderAsync(pergunta, async texto =>
                        {
                            await resposta.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(texto));
                            await resposta.OutputStream.FlushAsync();
                        }, cancelar.Token);
                    }
                    finally { umaPergunta.Release(); }
                }
            }
            // Separador "Resumo": só a própria página (same-origin) ou o endereço escrito à mão (none).
            // Outra porta de localhost é "same-site" e também fica de fora.
            else if (pedido.HttpMethod == "GET" && pedido.Url?.AbsolutePath is { } rota && (rota == "/resumo" || rota.StartsWith("/api/")) && pedido.Headers["Sec-Fetch-Site"] is not ("same-origin" or "none"))
                resposta.StatusCode = 403;
            else if (pedido.HttpMethod == "GET" && pedido.Url?.AbsolutePath == "/resumo")
                await ResumoAsync(pedido.QueryString, resposta, perfil.Vendedor);
            else if (pedido.HttpMethod == "GET" && pedido.Url?.AbsolutePath is { } rotaApi && rotaApi.StartsWith("/api/"))
            {
                // Uma consulta presa à BD não pode deixar o painel à espera para sempre.
                using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var r = await api.TratarAsync(rotaApi, pedido.QueryString, perfil, limite.Token);
                resposta.StatusCode = r.Estado;
                resposta.ContentType = r.Tipo;
                await resposta.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(r.Corpo));
            }
            else resposta.StatusCode = 404;
        }
        catch (Exception ex) when (ex is System.Net.HttpListenerException or IOException)
        {
            Console.Error.WriteLine("O browser fechou a ligação a meio da resposta."); // ex.: página recarregada
        }
        catch (Exception ex) // dentro do Task.Run perdia-se em silêncio
        {
            Console.Error.WriteLine($"Erro no pedido {pedido.Url?.AbsolutePath}: {ex.Message}");
        }
        finally
        {
            try { resposta.Close(); } catch { }
        }
    }
}

// Lê no máximo max + 1 caracteres: um corpo enorme nunca chega todo à memória. null = maior que max.
static async Task<string?> LerCorpoAsync(System.Net.HttpListenerRequest pedido, int max)
{
    using var leitor = new StreamReader(pedido.InputStream, System.Text.Encoding.UTF8);
    var buffer = new char[max + 1];
    var lidos = await leitor.ReadBlockAsync(buffer);
    return lidos > max ? null : new string(buffer, 0, lidos);
}

// Resumo das vendas (geral, de uma zona ou de um cliente): chama a ferramenta diretamente, sem modelo, por isso é rápido
// e os números são os da BD. Devolve os textos tal como a ferramenta os escreve; a página lê-os com o mesmo leitor do chat.
async Task ResumoAsync(System.Collections.Specialized.NameValueCollection q, System.Net.HttpListenerResponse resposta, string? vendedor)
{
    string[] periodos = ["Ultimos7Dias", "MesAtual", "TrimestreAtual", "AnoAtual", "Ultimos12Meses", "AnoAnterior"];
    var periodo = periodos.Contains(q["periodo"]) ? q["periodo"]! : "AnoAtual";
    var cliente = q["cliente"]?.Trim() is { Length: > 0 and <= 100 } c ? c : null;
    var zona = q["zona"]?.Trim() is { Length: > 0 and <= 100 } z ? z : null;
    // Botão "Unidades" da vista Artigos: só a lista dos artigos, por unidades, pedida quando se carrega nele
    var porQuantidade = q["ordenar"] == "Quantidade";

    Dictionary<string, string> partes;
    try
    {
        partes = await ResumoGuardadoAsync(periodo, cliente, zona, vendedor, porQuantidade);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Falha ao montar o resumo: {ex.Message}");
        resposta.StatusCode = 500;
        return;
    }
    Console.WriteLine($"[resumo {periodo}{(cliente is null ? "" : ", cliente")}{(zona is null ? "" : ", zona")}]"); // sem o nome: pode ser dado pessoal
    resposta.ContentType = "application/json; charset=utf-8";
    await resposta.OutputStream.WriteAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(partes));
}

// O resumo da página, da cache (ver "resumos") ou montado agora. Uma falha não fica guardada: o pedido seguinte tenta de novo.
Task<Dictionary<string, string>> ResumoGuardadoAsync(string periodo, string? cliente, string? zona, string? vendedor, bool artigosPorQuantidade = false)
{
    var chave = $"{DateTime.Today:yyyyMMdd}|{periodo}|{cliente}|{zona}|{vendedor}|{artigosPorQuantidade}";
    var validade = TimeSpan.FromMinutes(10);
    lock (resumos)
    {
        foreach (var velha in resumos.Where(r => DateTime.UtcNow - r.Value.Feito > validade).Select(r => r.Key).ToList())
            resumos.Remove(velha);
        if (!resumos.TryGetValue(chave, out var guardado) || guardado.Partes.IsFaulted || guardado.Partes.IsCanceled)
            resumos[chave] = guardado = (DateTime.UtcNow, Montar());
        return guardado.Partes;
    }

    async Task<Dictionary<string, string>> Montar()
    {
        // Uma consulta presa à BD não pode deixar o painel à espera para sempre.
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        // "Este mês" por mês: a ferramenta alargaria a AnoAtual (um mês só não é evolução); mes/ano mantêm os totais do mês.
        var meses = periodo == "MesAtual"
            ? new Dictionary<string, object?> { ["mes"] = DateTime.Today.Month, ["ano"] = DateTime.Today.Year }
            : [];
        return await MontarResumoAsync(new() { ["periodo"] = periodo }, meses, cliente, zona, limite.Token, vendedor: vendedor, artigosPorQuantidade: artigosPorQuantidade);
    }
}

// As consultas do resumo, iguais na página e no email. janela: o período de todas (periodo, ou dataInicio e dataFim).
// meses: argumentos a mais da evolução por mês; null = sem ela.
async Task<Dictionary<string, string>> MontarResumoAsync(
    Dictionary<string, object?> janela, Dictionary<string, object?>? meses, string? cliente, string? zona, CancellationToken ct, DateOnly? dia = null, string? vendedor = null,
    bool artigosPorQuantidade = false)
{
    // Só o email passa dia: continua com as zonas da ficha. No painel, "zona" é o distrito escolhido.
    var painel = dia is null;

    async Task<string> Texto(string ferramenta, Dictionary<string, object?> argumentos)
    {
        var r = await mcp.CallToolAsync(ferramenta, argumentos, cancellationToken: ct);
        return string.Concat(r.Content.OfType<TextContentBlock>().Select(b => b.Text));
    }

    Task<string> Filtrada(string ferramenta, Dictionary<string, object?> argumentos, bool soDistrito = true)
    {
        // Quem já traz o seu periodo (os clientes parados: 90 dias) fica com ele.
        if (!argumentos.ContainsKey("periodo"))
            foreach (var (chave, valor) in janela) argumentos.TryAdd(chave, valor);
        if (cliente is not null) argumentos["cliente"] = cliente;
        if (soDistrito && zona is not null) { argumentos["coluna"] = "Distrito"; argumentos["valor"] = zona; }
        // shortcut: as tools só aceitam uma coluna, por isso o perfil de vendedor ignora o filtro de distrito no resumo; a lista de créditos aplica os dois.
        if (vendedor is not null) { argumentos["coluna"] = "Vendedor"; argumentos["valor"] = vendedor; }
        return Texto(ferramenta, argumentos);
    }

    Task<string> Vendas(Dictionary<string, object?> argumentos, bool soDistrito = true) => Filtrada("consultar_vendas", argumentos, soDistrito);

    // Só enriquece a vista Clientes: se falhar ou passar do tempo, o resto do painel carrega na mesma.
    static async Task<string> Opcional(Task<string> consulta)
    {
        try { return await consulta; }
        catch (Exception ex) { Console.Error.WriteLine($"Consulta opcional do resumo falhou: {ex.Message}"); return ""; }
    }

    // Só a lista da vista Artigos, por unidades: os mesmos filtros, sem as outras consultas
    if (artigosPorQuantidade)
        return new() { ["artigos"] = await Vendas(new() { ["agrupar"] = "Artigo", ["limite"] = 100, ["ordenar"] = "Quantidade", ["compararAnoAnterior"] = true }) };

    var partes = new Dictionary<string, Task<string>>
    {
        // No painel, os 100 maiores para a lista "Ver artigos" da vista Artigos; o email usa 10.
        ["artigos"] = Vendas(new() { ["agrupar"] = "Artigo", ["limite"] = painel ? 100 : 10, ["compararAnoAnterior"] = true }),
    };
    if (meses is not null)
        partes["meses"] = Vendas(new(meses) { ["agrupar"] = "Mes", ["compararAnoAnterior"] = true });
    // Vista Faturação do painel (o email não a usa)
    if (painel)
        partes["documentos"] = Filtrada("consultar_documentos", new() { ["limite"] = 10 });
    // Cartão "Faturação por marca" da vista Artigos (o email não o usa). Marca e não família: na BD a família é quase toda "Familia Geral".
    if (painel)
        partes["marcas"] = Vendas(new() { ["agrupar"] = "Marca", ["limite"] = 6, ["compararAnoAnterior"] = true });
    // Só cliente: os rankings de zonas e clientes e os clientes parados não dizem nada sobre um cliente.
    if (cliente is null)
    {
        // Sem o filtro: o mosaico mostra sempre todos os distritos, com o escolhido em destaque.
        partes["zonas"] = Vendas(new() { ["agrupar"] = painel ? "Distrito" : "Zona", ["limite"] = 8, ["compararAnoAnterior"] = true }, soDistrito: false);
        if (painel)
            partes["distritosClientes"] = Texto("consultar", vendedor is null
                ? new() { ["coluna"] = "Distrito", ["contar"] = true, ["limite"] = 100 }
                : new() { ["coluna"] = "Distrito", ["contar"] = true, ["limite"] = 100, ["cruzarCom"] = "Vendedor", ["valorCruzado"] = vendedor });
        // Tabela das localidades no painel (o email não a usa)
        partes["localidades"] = Vendas(new() { ["agrupar"] = "Localidade", ["limite"] = 12, ["compararAnoAnterior"] = true });
        // No painel, a vista Clientes lista os 100 maiores; os outros cartões usam só os 10 primeiros.
        partes["clientes"] = Vendas(new() { ["agrupar"] = "Cliente", ["limite"] = painel ? 100 : 10, ["compararAnoAnterior"] = true });
        if (painel)
            partes["carteira"] = Opcional(Filtrada("consultar_carteira", new() { ["limite"] = 100 }));
        // Reenvio com --dia: os 90 dias contam desse dia (igual a Ultimos90Dias), não de hoje.
        partes["semVendas"] = Vendas(dia is { } d
            ? new() { ["semVendas"] = true, ["dataInicio"] = $"{d.AddDays(-89):dd-MM-yyyy}", ["dataFim"] = $"{d:dd-MM-yyyy}", ["limite"] = 1 }
            : new() { ["semVendas"] = true, ["periodo"] = "Ultimos90Dias", ["limite"] = 1 });
    }

    await Task.WhenAll(partes.Values);
    return partes.ToDictionary(p => p.Key, p => p.Value.Result);
}

// ---- Resumo por email: corrido pela tarefa agendada às segundas (ver README). Devolve 1 se algum envio falhou,
// para a tarefa aparecer como falhada no Agendador.
async Task<int> EnviarResumosAsync()
{
    // --dia dd-MM-aaaa: reenviar o de uma segunda que falhou (ex.: PC desligado).
    var i = Array.IndexOf(args, "--dia");
    var hoje = DateOnly.FromDateTime(DateTime.Today);
    if (i >= 0 && (i + 1 >= args.Length || !DateOnly.TryParseExact(args[i + 1], "dd-MM-yyyy", out hoje)))
    {
        Console.Error.WriteLine("--dia precisa de uma data dd-MM-aaaa.");
        return 1;
    }
    var devidos = Agendamentos.Ler(ficheiroAgendamentos).Where(a => Agendamentos.Devido(a.Freq, hoje)).ToList();
    Console.WriteLine($"{devidos.Count} resumo(s) para enviar hoje.");
    if (devidos.Count == 0) return 0;

    var remetente = Environment.GetEnvironmentVariable("VENDAS_SMTP_UTILIZADOR");
    var ficheiroPwd = Path.Combine(pastaDados, "smtp.pwd");
    var pastaTeste = Environment.GetEnvironmentVariable("VENDAS_SMTP_PASTA");
    if (remetente is null || (pastaTeste is null && !File.Exists(ficheiroPwd)))
    {
        Console.Error.WriteLine("Faltam VENDAS_SMTP_UTILIZADOR ou o smtp.pwd (ver README).");
        return 1;
    }

    using var smtp = pastaTeste is not null
        // Para experimentar sem servidor: grava os emails (.eml) nesta pasta em vez de os enviar.
        ? new System.Net.Mail.SmtpClient { DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.SpecifiedPickupDirectory, PickupDirectoryLocation = pastaTeste }
        : new System.Net.Mail.SmtpClient(
            Environment.GetEnvironmentVariable("VENDAS_SMTP_HOST") ?? "smtp.office365.com",
            int.TryParse(Environment.GetEnvironmentVariable("VENDAS_SMTP_PORTA"), out var porta) ? porta : 587)
        {
            EnableSsl = true, // STARTTLS: a password e os dados nunca vão em claro
            Credentials = new System.Net.NetworkCredential(remetente, LerPwd(ficheiroPwd)),
        };

    var falhas = 0;
    // Um resumo por frequência, enviado a todos os que a escolheram.
    foreach (var grupo in devidos.GroupBy(a => a.Freq))
    {
        var (desde, ate) = Agendamentos.Janela(grupo.Key, hoje);
        var intervalo = $"{desde:dd-MM-yyyy} a {ate.AddDays(-1):dd-MM-yyyy}";
        string corpo;
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var p = await MontarResumoAsync(
                new() { ["dataInicio"] = $"{desde:dd-MM-yyyy}", ["dataFim"] = $"{ate.AddDays(-1):dd-MM-yyyy}" }, null, null, null, limite.Token, hoje);
            corpo = Agendamentos.CorpoHtml($"Resumo de vendas: {intervalo}",
            [
                ("Por zona", p["zonas"]), ("Melhores clientes", p["clientes"]), ("Melhores artigos", p["artigos"]),
                ("Clientes sem compras nos últimos 90 dias", p["semVendas"]),
            ]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Falha ao montar o resumo {grupo.Key}: {ex.Message}");
            falhas += grupo.Count();
            continue;
        }

        foreach (var a in grupo)
        {
            try
            {
                using var email = new System.Net.Mail.MailMessage(remetente, a.Email, $"Resumo de vendas: {intervalo}", corpo) { IsBodyHtml = true };
                await smtp.SendMailAsync(email);
                Console.WriteLine($"Enviado ({grupo.Key}).");  // sem o endereço: é dado pessoal
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Falha ao enviar ({grupo.Key}): {ex.Message}");
                falhas++;
            }
        }
    }
    return falhas > 0 ? 1 : 0;
}

// O mesmo formato do sql.pwd (DPAPI: só este utilizador Windows o lê).
static string LerPwd(string ficheiro) => OperatingSystem.IsWindows()
    ? System.Text.Encoding.Unicode.GetString(System.Security.Cryptography.ProtectedData.Unprotect(
        Convert.FromHexString(File.ReadAllText(ficheiro).Trim()), null, System.Security.Cryptography.DataProtectionScope.CurrentUser))
    : throw new PlatformNotSupportedException("O smtp.pwd usa DPAPI: só em Windows.");

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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vendas.Servidor.Modelos.Dados;



// Cria o construtor do host da aplicação
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(opcoes =>
{
    opcoes.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Repositório de vendas: liga sempre ao SQL Server via RepoSql.
// Requer a variável de ambiente VENDAS_SQL (connection string) — não há fallback em memória.

    var ligacao = Environment.GetEnvironmentVariable("VENDAS_SQL")
        ?? throw new InvalidOperationException(
            "VENDAS_SQL não definida. Defina a ligação ao SQL antes de arrancar o servidor");
    
    builder.Services.AddSingleton<IVendasRepo>(_ => new RepoSql(ligacao));

    builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
 
// Constrói e executa o host
await builder.Build().RunAsync();


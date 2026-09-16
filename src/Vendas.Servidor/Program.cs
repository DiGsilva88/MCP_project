using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vendas.Servidor.Dados;
using Vendas.Servidor.Modelos.Dados;

// Cria o construtor do host da aplicação
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(opcoes =>
{
    opcoes.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Adiciona o repositório de vendas em memória como um serviço singleton.
// Quando VendasRepo (Modelos/Dados/VendasRepo.cs) tiver as consultas SQL implementadas,
 // Sem VENDAS_SQL corre em memória; com ela, vai ao SQL Server.

 var cs = Environment.GetEnvironmentVariable("VENDAS_SQL");
   

    if (string.IsNullOrWhiteSpace(cs))
    {
        Console.Error.WriteLine("[Vendas.Servidor] VENDAS_SQL não definida — a usar RepoMemoria (dados em memória).");
        builder.Services.AddSingleton<IVendasRepo, RepoMemoria>();
    }
    else
    {
        Console.Error.WriteLine("[Vendas.Servidor] VENDAS_SQL definida — a usar RepoSql (SQL Server).");
        builder.Services.AddSingleton<IVendasRepo>(_ => new RepoSql(cs));
    }

    builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
 
// Constrói e executa o host
await builder.Build().RunAsync();


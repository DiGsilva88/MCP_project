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

// Adiciona o repositório de vendas em memória como um serviço singleton.
// Quando VendasRepo (Modelos/Dados/VendasRepo.cs) tiver as consultas SQL implementadas,
 // Sem VENDAS_SQL corre em memória; com ela, vai ao SQL Server.

 var cs = Environment.GetEnvironmentVariable("VENDAS_SQL");
   

    var ligacao = Environment.GetEnvironmentVariable("VENDAS_SQL")
        ?? throw new InvalidOperationException(
            "Vendas_Sql não definida. Defina a ligação ao SQL antes de arrancar o servidor");
    
    builder.Services.AddSingleton<IVendasRepo>(_ => new RepoSql(ligacao));

    builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
 
// Constrói e executa o host
await builder.Build().RunAsync();


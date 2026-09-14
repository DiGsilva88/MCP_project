
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
// trocar para: builder.Services.AddSingleton<IVendasRepo, VendasRepo>();
builder.Services.AddSingleton<IVendasRepo, RepoMemoria>();

// Configura o servidor MCP com transporte padrão e registra as ferramentas do assembly
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();


// Constrói e executa o host
await builder.Build().RunAsync();


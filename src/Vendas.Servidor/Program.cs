
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vendas.Servidor.Modelos.Dados;

var builder = Host.CreateApplicationBuilder(args);

// Configura o logging para exibir mensagens no console
builder.Logging.AddConsole(opcoes =>
{
    opcoes.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Adiciona o repositório de vendas em memória como um serviço singleton
builder.Services.AddSingleton<IVendasRepo, RepoMemoria>();

// Configura o servidor MCP com transporte padrão e registra as ferramentas do assembly
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();


// Constrói e executa o host
await builder.Build().RunAsync();


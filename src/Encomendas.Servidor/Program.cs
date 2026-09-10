using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;


var builder=Host.CreateApplicationBuilder(args);
// Add services to the container.

builder.Logging.AddConsole(opcoes =>
{
    // Configura o formato de saída do log
    
    opcoes.LogToStandardErrorThreshold = LogLevel.Trace;
    // Configura o nível mínimo de log para ser exibido no console
});

builder.Services
    .AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

await builder.Build().RunAsync();
    



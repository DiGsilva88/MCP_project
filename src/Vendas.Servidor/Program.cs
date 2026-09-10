using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;


var builder=Host.CreateApplicationBuilder(args);
// Add services to the container.

builder.Logging.AddConsole(opcoes =>
{
    
    opcoes.LogToStandardErrorThreshold = LogLevel.Trace; 
    //opcoes.FormatterName = "json";
});

builder.Services 
    .AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();// Add your services here

await builder.Build().RunAsync();

    



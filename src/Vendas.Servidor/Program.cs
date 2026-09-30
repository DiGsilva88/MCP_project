using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
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
builder.Logging.SetMinimumLevel(LogLevel.Information); // Trace/Debug do host só gastam tempo

// Repositório de vendas: liga sempre ao SQL Server via RepoSql.
// Requer a variável de ambiente VENDAS_SQL (connection string) — não há fallback em memória.
var ligacao = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("VENDAS_SQL")
    ?? throw new InvalidOperationException(
        "VENDAS_SQL não definida. Defina a ligação ao SQL antes de arrancar o servidor"));

// A password pode vir à parte, encriptada com DPAPI (só este utilizador Windows a lê).
// Ficheiro criado com: Read-Host -AsSecureString | ConvertFrom-SecureString | Set-Content <ficheiro>
var ficheiroPwd = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vendas", "sql.pwd");
if (OperatingSystem.IsWindows() && File.Exists(ficheiroPwd))
    ligacao.Password = Encoding.Unicode.GetString(ProtectedData.Unprotect(
        Convert.FromHexString(File.ReadAllText(ficheiroPwd).Trim()), null, DataProtectionScope.CurrentUser));

builder.Services.AddSingleton<IVendasRepo>(_ => new RepoSql(ligacao.ConnectionString));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Constrói e executa o host
await builder.Build().RunAsync();


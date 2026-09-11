using System.ComponentModel;
using System.Globalization;
using System.Text;
using ModelContextProtocol.Server;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Ferramentas;

[McpServerToolType]
public sealed class VendasTools(IVendasRepo vendasRepo)
{
    private readonly IVendasRepo _vendasRepo = vendasRepo;

    [McpServerTool(Name = "vendas_top_clientes")]
    [Description("Clientes com maior total vendido, do maior para o menor. Devolve CSV com cliente, número de vendas e total vendido.")]
    public async Task<string> TopClientesAsync(
        [Description("Número máximo de clientes a devolver (1 a 100).")] int limite = 10,
        CancellationToken ct = default)
    {
        var limiteEfetivo = Math.Clamp(limite, 1, 100);

        try
        {
            var linhas = await _vendasRepo.ObterTopClientesAsync(limiteEfetivo, ct);

            var sb = new StringBuilder();
            sb.AppendLine("cliente,vendas,total_vendido");

            foreach (var l in linhas)
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"{Escapar(l.Cliente)},{l.NumeroVendas},{l.TotalVendido:0.00}"));
            }

            if (linhas.Count == limiteEfetivo)
            {
                sb.AppendLine($"# resultado truncado em {limiteEfetivo} linhas");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[vendas_top_clientes] {ex}");
            return "Não foi possível obter os dados de vendas neste momento.";
        }
    }

    private static string Escapar(string valor) =>
        valor.Contains(',') || valor.Contains('"')
            ? $"\"{valor.Replace("\"", "\"\"")}\""
            : valor;
}
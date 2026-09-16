using System.ComponentModel;
using System.Globalization;
using System.Text;
using ModelContextProtocol.Server;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Ferramentas;

// [McpServerToolType] // sem dados reais ainda
public sealed class VendasTools(
    IVendasRepo vendasRepo)
{
    private readonly IVendasRepo _vendasRepo = vendasRepo;

    // [McpServerTool(Name = "vendas_top_clientes")]
    // [Description("Clientes com maior total vendido, do maior para o menor. Devolve CSV com cliente, número de vendas e total vendido.")]
    // public async Task<string> TopClientesAsync(
    //     [Description("Número máximo de clientes a devolver (1 a 100).")] int limite = 5,
    //     [Description("Quantos dias de vendas a considerar(1 a 365).")] int dias = 90,
    //     CancellationToken ct = default)

    // {

    //     limite = Math.Clamp(limite, 1, 100);
    //     dias = Math.Clamp(dias, 1, 365);

    //     try
    //     {
    //         var todas = await _vendasRepo.ObterTopClientesAsync(limite +1,dias , ct);
    //         if (todas.Count == 0) return $"Não há vendas registadas neste periodo, {dias} dias.";

    //         var sb = new StringBuilder();
    //         sb.AppendLine("cliente,vendas,total_vendido");

    //         foreach (var l in todas.Take(limite))
    //         {
    //             sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
    //                 $"{Escapar(l.Cliente)},{l.NumeroVendas},{l.TotalVendido:0.00}"));
    //         }

    //         if (todas.Count == limite)
    //         {
    //             sb.AppendLine($"# há mais clientes além destes,{limite} ");
    //         }

    //         return sb.ToString();
    //     }
    //     catch (OperationCanceledException) {throw;}
    //     catch (Exception ex)
    //     {
    //         Console.Error.WriteLine($"[vendas_top_clientes] {ex}");
    //         return "Não foi possível obter os dados de vendas neste momento.";
    //     }
    // }

    // [McpServerTool(Name = "vendas_top_produtos")]
    // [Description("Produtos com maior total vendido, do maior para o menor. Devolve CSV com produto, número de vendas e total vendido.")]
    // public async Task<string> TopProdutosAsync(
    // [Description("Número máximo de produtos a devolver (1 a 100).")] int limite = 10,
    // [Description("Quantos dias de vendas a considerar (1 a 365).")] int dias = 90,
    //     CancellationToken ct = default)
    // {
    //     limite = Math.Clamp(limite, 1, 100);
    //     dias = Math.Clamp(dias, 1, 365);

    //     try
    //     {
    //         var todas = await _vendasRepo.ObterTopProdutosAsync(limite +1,dias, ct);
    //         if(todas.Count == 0) return $"Não há vendas registadas neste periodo,{dias}";

    //         var sb = new StringBuilder();
    //         sb.AppendLine("produto,vendas,total_vendido");

    //         foreach (var l in todas)
    //         {
    //             sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
    //                 $"{Escapar(l.Produto)},{l.NumeroVendas},{l.TotalVendido:0.00}"));
    //         }

    //         if (todas.Count == limite)
    //         {
    //             sb.AppendLine($"# há mais produtos além destes,{limite} ");
    //         }

    //         return sb.ToString();
    //     }
    //     catch (OperationCanceledException) {throw;}
    //     catch (Exception ex)
    //     {
    //         Console.Error.WriteLine($"[vendas_top_produtos] {ex}");
    //         return "Não foi possível obter os dados de vendas neste momento.";
    //     }
    // }

    private static string Escapar(string valor) =>
        valor.Contains(',') || valor.Contains('"')
            ? $"\"{valor.Replace("\"", "\"\"")}\""
            : valor;
}

using System.ComponentModel;
using ModelContextProtocol.Server;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Ferramentas;

[McpServerToolType]
public sealed class VendasTools(
    IVendasRepo vendasRepo)
{
    private readonly IVendasRepo _vendasRepo = vendasRepo;

    [McpServerTool(Name = "listar-vendas-por-cliente")]
    [Description("Devolve as vendas agregadas por cliente.")]
    public async Task<IReadOnlyList<VendaPorCliente>> ListarClientesAsync(
        [Description("Texto opcional para filtrar por nome do cliente.")] string? filtro = null,
        CancellationToken cancellationToken = default)
    {
        var clientes = await _vendasRepo.ObterTopClientesAsync(50, cancellationToken);

        if (string.IsNullOrWhiteSpace(filtro))
        {
            return clientes;
        }

        var filtroNormalizado = filtro.Trim();
        return clientes
            .Where(c => c.Cliente.Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

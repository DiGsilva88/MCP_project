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

    [McpServerTool(Name = "listar-vendas-detalhadas")]
    [Description("Devolve a lista detalhada de vendas (produto, quantidade, valor e data), com filtro opcional por cliente.")]
    public async Task<IReadOnlyList<Venda>> ListarVendasAsync(
        [Description("Texto opcional para filtrar por nome do cliente.")] string? filtro = null,
        CancellationToken cancellationToken = default)
    {
        var vendas = await _vendasRepo.ListarVendasAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(filtro))
        {
            return vendas;
        }

        var filtroNormalizado = filtro.Trim();
        return vendas
            .Where(v => v.Cliente.Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

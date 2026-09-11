using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Modelos.Dados;

public interface IVendasRepo
{
    Task<IReadOnlyList<VendaPorCliente>> ObterTopClientesAsync(
        int limite,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Venda>> ListarVendasAsync(
        CancellationToken cancellationToken = default);
}
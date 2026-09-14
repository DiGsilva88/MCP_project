using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Modelos.Dados;

public interface IVendasRepo
{
    Task<IReadOnlyList<VendaPorCliente>> ObterTopClientesAsync(
        int limite,int dias, CancellationToken ct = default);

    Task<IReadOnlyList<VendaPorProduto>> ObterTopProdutosAsync(
        int limite,int dias, CancellationToken ct = default);

    Task<IReadOnlyList<ClienteInativo>> ObterInativosAsync(
        int dias, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ObterNomesClientesAsync(
        CancellationToken ct = default);

    
}
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
        int limite, CancellationToken ct = default);

    Task<IReadOnlyList<ContagemCliente>> ContarClientesAsync(

        DimensaoCliente agrupar, int limite, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContagemCliente>> ContarFaturacaoAsync(
    DimensaoFaturacao agrupar, int limite, CancellationToken cancellationToken = default);

    
}
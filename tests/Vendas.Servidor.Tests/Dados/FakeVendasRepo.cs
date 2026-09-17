using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Tests.Dados;

/// <summary>
/// Dublê de teste para <see cref="IVendasRepo"/>: devolve dados fixos definidos pelo teste
/// em vez de ir à base de dados, e regista os argumentos com que foi chamado.
/// </summary>
internal sealed class FakeVendasRepo : IVendasRepo
{
    public IReadOnlyList<string> Nomes { get; set; } = [];
    public IReadOnlyList<ContagemCliente> Contagens { get; set; } = [];
    public Exception? LancarExcecao { get; set; }

    public int? UltimoLimiteNomes { get; private set; }
    public int? UltimoLimiteContagem { get; private set; }
    public DimensaoCliente? UltimaDimensaoCliente { get; private set; }
    public DimensaoFaturacao? UltimaDimensaoFaturacao { get; private set; }

    public Task<IReadOnlyList<string>> ObterNomesClientesAsync(
        int limite, CancellationToken cancellationToken = default)
    {
        UltimoLimiteNomes = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Nomes);
    }

    public Task<IReadOnlyList<ContagemCliente>> ContarClientesAsync(
        DimensaoCliente agrupar, int limite, CancellationToken cancellationToken = default)
    {
        UltimaDimensaoCliente = agrupar;
        UltimoLimiteContagem = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Contagens);
    }

    public Task<IReadOnlyList<ContagemCliente>> ContarFaturacaoAsync(
        DimensaoFaturacao agrupar, int limite, CancellationToken cancellationToken = default)
    {
        UltimaDimensaoFaturacao = agrupar;
        UltimoLimiteContagem = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Contagens);
    }

    public Task<IReadOnlyList<VendaPorCliente>> ObterTopClientesAsync(
        int limite, int dias, CancellationToken ct = default) =>
        throw new NotSupportedException("Não é usado pelas ferramentas testadas.");

    public Task<IReadOnlyList<VendaPorProduto>> ObterTopProdutosAsync(
        int limite, int dias, CancellationToken ct = default) =>
        throw new NotSupportedException("Não é usado pelas ferramentas testadas.");

    public Task<IReadOnlyList<ClienteInativo>> ObterInativosAsync(
        int dias, CancellationToken ct = default) =>
        throw new NotSupportedException("Não é usado pelas ferramentas testadas.");
}

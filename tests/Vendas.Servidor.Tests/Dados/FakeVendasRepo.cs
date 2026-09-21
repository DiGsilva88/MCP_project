using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Tests.Dados;

/// <summary>
/// Dublê de teste para <see cref="IVendasRepo"/>: devolve dados fixos definidos pelo teste
/// em vez de ir à base de dados, e regista os argumentos com que foi chamado.
/// </summary>
internal sealed class FakeVendasRepo : IVendasRepo
{
    public PaginaVista Pagina { get; set; } = new([], [], 0);
    public IReadOnlyList<ContagemCliente> Contagens { get; set; } = [];
    public Exception? LancarExcecao { get; set; }

    public int? UltimoLimite { get; private set; }
    public string? UltimoValor { get; private set; }
    public string? UltimaColuna { get; private set; }
    public Vista? UltimaVista { get; private set; }

    public Task<PaginaVista> ConsultarAsync(
        Vista vista, string? colunaFiltro, string? valor, int limite, CancellationToken ct = default)
    {
        UltimaVista = vista;
        UltimaColuna = colunaFiltro;
        UltimoValor = valor;
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Pagina);
    }

    public Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        Vista vista, string coluna, int limite, CancellationToken ct = default)
    {
        UltimaVista = vista;
        UltimaColuna = coluna;
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Contagens);
    }
}

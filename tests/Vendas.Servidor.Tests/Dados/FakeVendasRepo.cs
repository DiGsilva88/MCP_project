using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Tests.Dados;

/// <summary>
/// Dublê de teste para <see cref="IVendasRepo"/>: devolve dados fixos definidos pelo teste
/// em vez de ir à base de dados, e regista os argumentos com que foi chamado.
/// </summary>
internal sealed class FakeVendasRepo : IVendasRepo
{
    public PaginaClientes Pagina { get; set; } = new([], [], 0);
    public IReadOnlyList<ContagemCliente> Contagens { get; set; } = [];
    public Exception? LancarExcecao { get; set; }

    public int? UltimoLimite { get; private set; }
    public string? UltimoNome { get; private set; }
    public IReadOnlyDictionary<Campo, string>? UltimosFiltros { get; private set; }
    public Campo? UltimoAgrupar { get; private set; }
    public IReadOnlyList<Campo>? UltimoMostrar { get; private set; }

    public Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        Campo agrupar, IReadOnlyDictionary<Campo, string> filtros, int limite, CancellationToken ct = default)
    {
        UltimoAgrupar = agrupar;
        UltimosFiltros = filtros;
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Contagens);
    }

    public Task<PaginaClientes> ListarAsync(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, string? nome, int limite,
        CancellationToken ct = default)
    {
        UltimoMostrar = mostrar;
        UltimosFiltros = filtros;
        UltimoNome = nome;
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Pagina);
    }
}

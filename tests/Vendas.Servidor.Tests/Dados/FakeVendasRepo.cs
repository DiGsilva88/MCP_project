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

    // Resultado de ContarAsync por chamada (p.ex. vazio com filtros, valores existentes sem filtros).
    // Sem isto, todas as chamadas devolvem Contagens.
    public Func<Campo, IReadOnlyDictionary<Campo, string>, IReadOnlyList<ContagemCliente>>? ContagensPor { get; set; }
    public List<(Campo Agrupar, IReadOnlyDictionary<Campo, string> Filtros, int Limite)> ChamadasContar { get; } = [];

    public int? UltimoLimite { get; private set; }
    public int? UltimoDeslocamento { get; private set; }
    public IReadOnlyDictionary<Campo, string>? UltimosFiltros { get; private set; }
    public Campo? UltimoAgrupar { get; private set; }
    public IReadOnlyList<Campo>? UltimoMostrar { get; private set; }

    public Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        Campo agrupar, IReadOnlyDictionary<Campo, string> filtros, int limite, CancellationToken ct = default)
    {
        UltimoAgrupar = agrupar;
        UltimosFiltros = filtros;
        UltimoLimite = limite;
        ChamadasContar.Add((agrupar, filtros, limite));
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(ContagensPor?.Invoke(agrupar, filtros) ?? Contagens);
    }

    public (CampoSensivel Dado, bool Maiores)? UltimoSensivel { get; private set; }

    public Task<PaginaClientes> ListarSensivelAsync(
        CampoSensivel mostrar, IReadOnlyDictionary<Campo, string> filtros, bool maiores, int deslocamento, int limite,
        CancellationToken ct = default)
    {
        UltimoSensivel = (mostrar, maiores);
        UltimosFiltros = filtros;
        UltimoDeslocamento = deslocamento;
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Pagina);
    }

    public Task<PaginaClientes> ListarAsync(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, int deslocamento, int limite,
        CancellationToken ct = default)
    {
        UltimoMostrar = mostrar;
        UltimosFiltros = filtros;
        UltimoDeslocamento = deslocamento;
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Pagina);
    }
}

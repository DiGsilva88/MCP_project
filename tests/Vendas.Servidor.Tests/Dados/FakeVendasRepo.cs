using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Tests.Dados;

/// <summary>
/// Copia de teste para <see cref="IVendasRepo"/>: devolve dados fixos definidos pelo teste
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

    public string? UltimoNome { get; private set; }
    public Campo? UltimoTambem { get; private set; }
    public int? UltimoLimite { get; private set; }
    public int? UltimoDeslocamento { get; private set; }
    public IReadOnlyDictionary<Campo, string>? UltimosFiltros { get; private set; }
    public Campo? UltimoAgrupar { get; private set; }
    public IReadOnlyList<Campo>? UltimoMostrar { get; private set; }
    public IReadOnlyList<ValorAgrupado> Valores { get; set; } = [];
    public (DateOnly Desde, DateOnly Ate)? UltimoPeriodo { get; private set; }
    public DimensaoVenda? UltimaDimensao { get; private set; }

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
        string? nome = null, Campo? tambem = null, CancellationToken ct = default)
    {
        UltimoNome = nome;
        UltimoTambem = tambem;
        UltimoSensivel = (mostrar, maiores);
        UltimosFiltros = filtros;
        UltimoDeslocamento = deslocamento;
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Pagina);
    }

    public Task<PaginaClientes> ListarAsync(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, int deslocamento, int limite,
        string? nome = null, CancellationToken ct = default)
    {
        UltimoNome = nome;
        UltimoMostrar = mostrar;
        UltimosFiltros = filtros;
        UltimoDeslocamento = deslocamento;
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Pagina);
    }

    public string? UltimoCliente { get; private set; }

    // Por omissão, qualquer texto é um cliente com esse ID (os testes antigos não precisam de o preparar).
    public Func<string, IReadOnlyList<(string Id, string Nome)>> Clientes { get; set; } = c => [(c.Trim(), "Cliente " + c.Trim())];

    public Task<IReadOnlyList<(string Id, string Nome)>> ProcurarClientesAsync(
        string cliente, int limite, CancellationToken ct = default)
    {
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult<IReadOnlyList<(string Id, string Nome)>>(Clientes(cliente).Take(limite).ToList());
    }

    public Task<IReadOnlyList<ValorAgrupado>> TopClientesAsync(
        DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken ct = default)
    {
        UltimoCliente = cliente;
        UltimosFiltros = filtros;
        UltimoPeriodo = (desde, ate);
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Valores);
    }

    public Task<IReadOnlyList<ValorAgrupado>> TopArtigosAsync(
        DimensaoVenda dimensao, DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, bool porQuantidade = false, CancellationToken ct = default)
    {
        UltimoCliente = cliente;
        UltimosFiltros = filtros;
        UltimaDimensao = dimensao;
        UltimoPeriodo = (desde, ate);
        UltimoLimite = limite;
        UltimoPorQuantidade = porQuantidade;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(Valores);
    }

    public bool? UltimoPorQuantidade { get; private set; }
    public IReadOnlyList<ClienteSemVendas> SemVendas { get; set; } = [];
    public DateOnly? UltimaVenda { get; set; }

    public Task<IReadOnlyList<ClienteSemVendas>> ClientesSemVendasAsync(
        DateOnly desde, DateOnly ate, int limite, IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken ct = default)
    {
        UltimosFiltros = filtros;
        UltimoPeriodo = (desde, ate);
        UltimoLimite = limite;
        if (LancarExcecao is not null) throw LancarExcecao;
        return Task.FromResult(SemVendas);
    }

    public Task<DateOnly?> UltimaVendaAsync(CancellationToken ct = default) => Task.FromResult(UltimaVenda);
}


namespace Vendas.Servidor.Modelos.Dados;

// Dados fictícios (VENDAS_DEMO=1): a equipa vê o painel e os documentos sem aceder à produção. Os testes usam os mesmos dados.
public sealed class RepoDemo : IVendasRepo
{
    private sealed record Cliente(string Id, string Nome, string Vendedor, string Distrito);

    private static readonly Cliente[] Clientes =
    [
        new("1001", "Mercearia Demonstração", "Ana Demo", "Lisboa"),
        new("1002", "Café Exemplo", "Rui Demo", "Porto"),
        new("1003", "Restaurante Fictício", "Ana Demo", "Setúbal"),
        new("1004", "Hotel Amostra", "Rui Demo", "Faro"),
    ];

    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.Today);

    // 40 documentos, um por dia, para trás a partir de hoje. Um em cada três é C.
    private static readonly DetalheDocumento[] Documentos = Enumerable.Range(0, 40).Select(i =>
    {
        var data = Hoje.AddDays(-i);
        var tipo = i % 3 == 0 ? "C" : "N";
        var cliente = Clientes[i % Clientes.Length];
        return new DetalheDocumento($"{tipo} 10/{data.Year}/{1034000 + i}", tipo, data, cliente.Id, cliente.Nome,
            Enumerable.Range(1, i % 3 + 1).Select(l =>
            {
                var liquido = -(10m + i + l);
                return new LinhaDocumento($"ART{l:000}", $"Artigo de demonstração {l}", -l, liquido - 1m, 1m, liquido, Math.Round(liquido * 0.23m, 2));
            }).ToList());
    }).ToArray();

    private static decimal Total(DetalheDocumento d) => d.Linhas.Sum(l => l.Liquido);

    private static bool Cumpre(string clienteId, IReadOnlyDictionary<Campo, string>? filtros)
    {
        var c = Clientes.First(x => x.Id == clienteId);
        return Igual(filtros, Campo.Vendedor, c.Vendedor) && Igual(filtros, Campo.Distrito, c.Distrito);
    }

    private static bool Igual(IReadOnlyDictionary<Campo, string>? filtros, Campo campo, string valor) =>
        filtros is null || !filtros.TryGetValue(campo, out var v) || v.Equals(valor, StringComparison.OrdinalIgnoreCase);

    public Task<PaginaCreditos> ListarCreditosAsync(
        DateOnly desde, DateOnly ate, string? tipo, string? pesquisa, int deslocamento, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default)
    {
        var candidatos = Documentos
            .Where(d => d.DataDoc >= desde && d.DataDoc < ate && (cliente is null || d.ClienteId == cliente) && Cumpre(d.ClienteId, filtros))
            .Where(d => string.IsNullOrWhiteSpace(pesquisa)
                        || d.DocumentoId.Contains(pesquisa.Trim(), StringComparison.OrdinalIgnoreCase)
                        || d.Cliente.Contains(pesquisa.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        var itens = candidatos.Where(d => tipo is null || d.TipoDocId == tipo).Skip(deslocamento).Take(limite)
            .Select(d => new CreditoListado(d.DocumentoId, d.TipoDocId, d.DataDoc, d.ClienteId, d.Cliente, Total(d))).ToList();
        return Task.FromResult(new PaginaCreditos(itens, candidatos.Count(d => d.TipoDocId == "N"), candidatos.Count(d => d.TipoDocId == "C")));
    }

    public Task<DetalheDocumento?> DetalheDocumentoAsync(
        string documento, string tipo, int ano, IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Documentos.FirstOrDefault(d => d.DocumentoId == documento && Cumpre(d.ClienteId, filtros)));

    public Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        Campo agrupar, IReadOnlyDictionary<Campo, string> filtros, int limite, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ContagemCliente> r = Clientes.Where(c => Cumpre(c.Id, filtros)).GroupBy(c => agrupar == Campo.Vendedor ? c.Vendedor : c.Distrito)
            .Select(g => new ContagemCliente(g.Key, g.Count(), Clientes.Length, 2)).Take(limite).ToList();
        return Task.FromResult(r);
    }

    public Task<PaginaClientes> ListarAsync(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, int deslocamento, int limite,
        string? nome = null, CancellationToken cancellationToken = default)
    {
        var colunas = new[] { "NomeCliente" }.Concat(mostrar.Select(c => c.ToString())).ToArray();
        var linhas = Clientes.Where(c => Cumpre(c.Id, filtros) && (nome is null || c.Id == nome || c.Nome.StartsWith(nome, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new[] { c.Nome }.Concat(mostrar.Select(m => m switch
            {
                Campo.Vendedor => c.Vendedor,
                Campo.Distrito => c.Distrito,
                _ => "Demonstração",
            })).ToArray()).ToList();
        return Task.FromResult(new PaginaClientes(colunas, linhas, linhas.Count));
    }

    public Task<PaginaClientes> ListarSensivelAsync(
        CampoSensivel mostrar, IReadOnlyDictionary<Campo, string> filtros, bool maiores, int deslocamento, int limite,
        string? nome = null, Campo? tambem = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaginaClientes(["NomeCliente", mostrar.ToString()], [], 0));

    public Task<IReadOnlyList<(string Id, string Nome)>> ProcurarClientesAsync(string cliente, int limite, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(string, string)> r = Clientes
            .Where(c => c.Id == cliente.Trim() || c.Nome.StartsWith(cliente.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(c => (c.Id, c.Nome)).Take(limite).ToList();
        return Task.FromResult(r);
    }

    private static IReadOnlyList<ValorAgrupado> Ranking(string[] nomes, DateOnly ate) => nomes
        .Select((n, i) => new ValorAgrupado(n, 3000m - i * 500, 100 - i * 10, 10 - i, nomes.Length * 2500m, nomes.Length,
            3100m - i * 500, -100m, 690m - i * 115, 40, ate.AddDays(-1)))
        .ToList();

    public Task<IReadOnlyList<ValorAgrupado>> RankingPorClienteAsync(
        DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Ranking(Clientes.Where(c => Cumpre(c.Id, filtros)).Select(c => $"{c.Nome} ({c.Id})").ToArray(), ate));

    public Task<IReadOnlyList<ValorAgrupado>> RankingPorDimensaoAsync(
        DimensaoVenda dimensao, DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, bool porQuantidade = false,
        DimensaoVenda? emCada = null, bool menores = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(Ranking(dimensao switch
        {
            DimensaoVenda.Mes => Meses(desde, ate),
            DimensaoVenda.Distrito => Clientes.Select(c => c.Distrito).ToArray(),
            _ => [$"{dimensao} A", $"{dimensao} B", $"{dimensao} C"],
        }, ate));

    private static string[] Meses(DateOnly desde, DateOnly ate) =>
        Enumerable.Range(0, Math.Max(1, (ate.Year - desde.Year) * 12 + ate.Month - desde.Month))
            .Select(i => desde.AddMonths(i).ToString("yyyy-MM")).ToArray();

    public Task<IReadOnlyList<ClienteSemVendas>> ClientesSemVendasAsync(
        DateOnly desde, DateOnly ate, int limite, IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ClienteSemVendas>>([]);

    public Task<ResumoDocumentos> ResumoDocumentosAsync(
        DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default)
    {
        var creditos = Documentos.Where(d => d.DataDoc >= desde && d.DataDoc < ate && (cliente is null || d.ClienteId == cliente) && Cumpre(d.ClienteId, filtros))
            .Select(d => new DocumentoEmitido(d.DocumentoId, d.TipoDocId, d.DataDoc, d.Cliente, Total(d))).ToList();
        var valorN = creditos.Where(c => c.TipoDocId == "N").Sum(c => c.Total);
        var valorC = creditos.Where(c => c.TipoDocId == "C").Sum(c => c.Total);
        var mes = Hoje.ToString("yyyy-MM");
        return Task.FromResult(new ResumoDocumentos(
            [new("F", 8000m, 20), new("V", 4000m, 15), new("N", valorN, creditos.Count(c => c.TipoDocId == "N")), new("C", valorC, creditos.Count(c => c.TipoDocId == "C"))],
            [new(mes, "F", 8000m, 20), new(mes, "V", 4000m, 15), new(mes, "N", valorN, creditos.Count(c => c.TipoDocId == "N")), new(mes, "C", valorC, creditos.Count(c => c.TipoDocId == "C"))],
            creditos.Take(limite).ToList(), creditos));
    }

    public Task<ResumoCarteira> ResumoCarteiraAsync(
        DateOnly desde, DateOnly ate, IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default)
    {
        var escolhidos = Clientes.Where(c => Cumpre(c.Id, filtros)).ToList();
        return Task.FromResult(new ResumoCarteira(
            escolhidos.Select(c => new ClienteCarteira(c.Id, c.Distrito, Hoje)).ToList(),
            escolhidos.Select(c => new CompraMensal(c.Id, Hoje.ToString("yyyy-MM"), 1000m, 3)).ToList()));
    }

    public Task<DateOnly?> UltimaVendaAsync(CancellationToken cancellationToken = default) => Task.FromResult<DateOnly?>(Hoje);
}

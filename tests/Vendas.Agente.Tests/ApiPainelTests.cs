using System.Collections.Specialized;

namespace Vendas.Agente.Tests;

public class ApiPainelTests
{
    private static readonly Perfil Todos = new("DOM\\gestor", null), Ana = new("DOM\\ana", "Ana");
    private const string JsonCreditos = """{"itens":[],"contadores":{"N":0,"C":0}}""";

    private sealed class Tools(string resposta)
    {
        public List<(string Nome, Dictionary<string, object?> Args)> Chamadas { get; } = [];
        public List<(Perfil Perfil, string Documento)> Auditoria { get; } = [];
        public string Resposta { get; set; } = resposta;
        public ApiPainel Api() => new((nome, args, _) => { Chamadas.Add((nome, args)); return Task.FromResult(Resposta); }, (p, d) => Auditoria.Add((p, d)));
    }

    private static NameValueCollection Q(params (string, string)[] pares)
    {
        var q = new NameValueCollection();
        foreach (var (k, v) in pares) q[k] = v;
        return q;
    }

    [Fact]
    public async Task Creditos_PassaFiltrosEPagina()
    {
        var t = new Tools(JsonCreditos);

        var r = await t.Api().TratarAsync("/api/creditos", Q(("tipo", "C"), ("q", "costa"), ("page", "4"), ("periodo", "MesAtual"), ("zona", "Lisboa")), Todos);

        Assert.Equal(200, r.Estado);
        var (nome, a) = Assert.Single(t.Chamadas);
        Assert.Equal("consultar_creditos", nome);
        Assert.Equal("C", a["tipo"]);
        Assert.Equal("costa", a["pesquisa"]);
        Assert.Equal(4, a["pagina"]);
        Assert.Equal("MesAtual", a["periodo"]);
        Assert.Equal("Distrito", a["coluna"]);
        Assert.Equal("Lisboa", a["valor"]);
        Assert.False(a.ContainsKey("vendedor"));
    }

    [Theory]
    [InlineData("F")]
    [InlineData("N' OR 1=1")]
    public async Task Creditos_TipoInvalido_400SemIrAFerramenta(string tipo)
    {
        var t = new Tools(JsonCreditos);

        Assert.Equal(400, (await t.Api().TratarAsync("/api/creditos", Q(("tipo", tipo)), Todos)).Estado);
        Assert.Empty(t.Chamadas);
    }

    [Fact]
    public async Task Creditos_PerfilDeVendedor_VemDoServidorENaoDoPedido()
    {
        var t = new Tools(JsonCreditos);

        await t.Api().TratarAsync("/api/creditos", Q(("vendedor", "Outro")), Ana);

        Assert.Equal("Ana", t.Chamadas[0].Args["vendedor"]);
    }

    [Fact]
    public async Task Creditos_PedidoIgual_UsaCache_MasNaoEntrePerfis()
    {
        var t = new Tools(JsonCreditos);
        var api = t.Api();

        await api.TratarAsync("/api/creditos", Q(), Todos);
        await api.TratarAsync("/api/creditos", Q(), Todos);
        Assert.Single(t.Chamadas);

        await api.TratarAsync("/api/creditos", Q(), Ana);
        Assert.Equal(2, t.Chamadas.Count);
    }

    [Fact]
    public async Task Creditos_ErroDaFerramenta_502ENaoFicaEmCache()
    {
        var t = new Tools("Não foi possível consultar os dados neste momento");
        var api = t.Api();

        Assert.Equal(502, (await api.TratarAsync("/api/creditos", Q(), Todos)).Estado);
        t.Resposta = JsonCreditos;
        Assert.Equal(200, (await api.TratarAsync("/api/creditos", Q(), Todos)).Estado);
    }

    [Fact]
    public async Task Documento_ConverteOIdPassaOVendedorERegistaAuditoria()
    {
        var t = new Tools("""{"documento":"N 10/2026/5"}""");

        var r = await t.Api().TratarAsync("/api/documentos/N-10-2026-5", Q(), Ana);

        Assert.Equal(200, r.Estado);
        Assert.Equal("N 10/2026/5", t.Chamadas[0].Args["documento"]);
        Assert.Equal("Ana", t.Chamadas[0].Args["vendedor"]);
        Assert.Equal((Ana, "N 10/2026/5"), Assert.Single(t.Auditoria));
    }

    [Fact]
    public async Task Documento_DeOutroVendedor_404SemAuditoria()
    {
        var t = new Tools("""{"erro":"nao_encontrado"}""");

        Assert.Equal(404, (await t.Api().TratarAsync("/api/documentos/N-10-2026-5", Q(), Ana)).Estado);
        Assert.Empty(t.Auditoria);
    }

    [Theory]
    [InlineData("1034675")]            // só o número repete-se entre anos e secções
    [InlineData("N-10-2026-1;DROP")]
    public async Task Documento_IdInvalido_404SemIrAFerramenta(string id)
    {
        var t = new Tools("{}");

        Assert.Equal(404, (await t.Api().TratarAsync($"/api/documentos/{id}", Q(), Todos)).Estado);
        Assert.Empty(t.Chamadas);
    }

    [Fact]
    public async Task Pdf_SemOrigemConhecida_404()
    {
        var t = new Tools("{}");

        Assert.Equal(404, (await t.Api().TratarAsync("/api/documentos/N-10-2026-5/pdf", Q(), Todos)).Estado);
        Assert.Empty(t.Chamadas);
    }

    [Fact]
    public async Task Cliente_PerfilGeral_DevolveAFichaDaFerramenta()
    {
        var t = new Tools("NomeCliente,Zona\nCasa,Sul\n");

        var r = await t.Api().TratarAsync("/api/clientes/1003", Q(), Todos);

        Assert.Equal(200, r.Estado);
        Assert.StartsWith("text/plain", r.Tipo);
        Assert.Equal("1003", t.Chamadas[0].Args["cliente"]);
        Assert.False(t.Chamadas[0].Args.ContainsKey("cruzarCom"));
    }

    [Theory]
    [InlineData("NomeCliente,Zona\n# Mostrados 0 de 0\n")]                 // cliente de outro vendedor
    [InlineData("Vários clientes começam por \"1\". Indique o ID:\nid,cliente\n1,Casa\n")] // não pode revelar nomes
    public async Task Cliente_Vendedor_SemFichaSua_404(string resposta)
    {
        var t = new Tools(resposta);

        Assert.Equal(404, (await t.Api().TratarAsync("/api/clientes/1", Q(), Ana)).Estado);
        Assert.Equal("Vendedor", t.Chamadas[0].Args["cruzarCom"]);
        Assert.Equal("Ana", t.Chamadas[0].Args["valorCruzado"]);
    }

    [Fact]
    public async Task FalhaDaFerramenta_502()
    {
        var api = new ApiPainel((_, _, _) => throw new InvalidOperationException("MCP em baixo"), (_, _) => { });

        Assert.Equal(502, (await api.TratarAsync("/api/creditos", Q(), Todos)).Estado);
    }

    [Theory]
    [InlineData("DOM\\ANA", "Ana")]
    [InlineData("dom\\maria", null)]
    public void Perfis_Resolver_IgnoraMaiusculas_EAsteriscoVeTudo(string login, string? vendedor)
    {
        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DOM\\ana"] = "Ana", ["DOM\\maria"] = Perfis.VeTudo };

        Assert.Equal(vendedor, Perfis.Resolver(login, mapa)!.Vendedor);
    }

    [Fact]
    public void Perfis_LoginForaDoFicheiro_FicaSemAcesso() =>
        Assert.Null(Perfis.Resolver("DOM\\intruso", new Dictionary<string, string>()));

    [Fact]
    public void Perfis_SemFicheiro_NinguemEntra() =>
        Assert.Empty(Perfis.Ler(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));

    [Fact]
    public void LinhaAuditoria_TemQuemOQueEQuando() =>
        Assert.Equal("2026-11-10 09:05:00\tDOM\\ana\tN 10/2026/5",
            ApiPainel.LinhaAuditoria(new DateTimeOffset(2026, 11, 10, 9, 5, 0, TimeSpan.Zero), "DOM\\ana", "N 10/2026/5"));
}

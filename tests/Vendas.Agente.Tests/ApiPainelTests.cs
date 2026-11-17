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
        Assert.Equal(["Lisboa"], (string[])a["zonas"]!);
        Assert.False(a.ContainsKey("vendedor"));
    }

    [Fact]
    public void Filtros_TodosOsCamposComVariosValores()
    {
        var q = new NameValueCollection { { "de", "01-02-2026" }, { "ate", "28-02-2026" }, { "zona", "Braga" }, { "zona", "Porto" }, { "zona", "braga" },
                                          { "marca", "Bosch" }, { "tipodoc", "F" }, { "cliente", " Casa " }, { "comparar", "nao" } };

        var f = FiltrosPainel.Ler(q, new DateOnly(2026, 11, 17))!;
        var args = f.Janela();
        f.Aplicar(args);

        Assert.Equal("01-02-2026", args["dataInicio"]);
        Assert.Equal("28-02-2026", args["dataFim"]);
        Assert.False(args.ContainsKey("periodo"));       // as datas ganham ao período
        Assert.Equal(["Braga", "Porto"], (string[])args["zonas"]!);
        Assert.Equal(["Bosch"], (string[])args["marcas"]!);
        Assert.Equal(["F"], (string[])args["tiposDoc"]!);
        Assert.Equal("Casa", args["cliente"]);
        Assert.False(f.Comparar);
    }

    [Theory]
    [InlineData("de", "28-02-2026", "ate", "01-02-2026")]   // De depois de Até
    [InlineData("de", "2026-02-01", "ate", "28-02-2026")]   // formato errado
    [InlineData("de", "01-02-2026", "x", "")]               // só uma das datas
    [InlineData("tipodoc", "F' OR 1=1", "x", "")]
    public void Filtros_Invalidos_DaoNull(string k1, string v1, string k2, string v2) =>
        Assert.Null(FiltrosPainel.Ler(new NameValueCollection { { k1, v1 }, { k2, v2 } }, new DateOnly(2026, 11, 17)));

    [Fact]
    public void Filtros_SemNada_SaoOAnoAtualComComparacao() =>
        Assert.Equal(FiltrosPainel.Omissao.Chave, FiltrosPainel.Ler(new NameValueCollection(), new DateOnly(2026, 11, 17))!.Chave);

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
        Assert.False(t.Chamadas[0].Args.ContainsKey("vendedor"));
    }

    [Theory]
    [InlineData("NomeCliente,Zona\n# Mostrados 0 de 0\n")]                 // cliente de outro vendedor
    [InlineData("Vários clientes começam por \"1\". Indique o ID:\nid,cliente\n1,Casa\n")] // não pode revelar nomes
    public async Task Cliente_Vendedor_SemFichaSua_404(string resposta)
    {
        var t = new Tools(resposta);

        Assert.Equal(404, (await t.Api().TratarAsync("/api/clientes/1", Q(), Ana)).Estado);
        Assert.Equal("Ana", t.Chamadas[0].Args["vendedor"]);
    }

    [Fact]
    public async Task Briefing_PassaOVendedorDoPerfil_EDevolveOJson()
    {
        var t = new Tools("""{"cliente":"Casa (1003)","deixouDeComprar":[]}""");

        var r = await t.Api().TratarAsync("/api/clientes/1003/briefing", Q(), Ana);

        Assert.Equal(200, r.Estado);
        Assert.Equal(("consultar_briefing", "1003", "Ana"), (t.Chamadas[0].Nome, t.Chamadas[0].Args["cliente"], t.Chamadas[0].Args["vendedor"]));
    }

    [Fact]
    public async Task Briefing_ClienteDeOutroVendedor_404SemNomes()
    {
        var t = new Tools("""{"erro":"Vários clientes começam por \"1\"..."}""");

        var r = await t.Api().TratarAsync("/api/clientes/1/briefing", Q(), Ana);

        Assert.Equal(404, r.Estado);
        Assert.DoesNotContain("Vários", r.Corpo);
    }

    [Theory]
    // POST: só da própria origem (sem Origin = não veio de um browser noutra página)
    [InlineData("POST", "/feedback", "http://localhost:5080", null, false, false)]
    [InlineData("POST", "/feedback", null, null, false, false)]
    [InlineData("POST", "/perguntar", "https://evil.example", null, false, true)]
    [InlineData("POST", "/feedback", "http://localhost:5081", null, false, true)]   // outra porta do mesmo PC
    [InlineData("POST", "/feedback", "nao-e-url", null, false, true)]
    // Vendedor: sem chat nem email
    [InlineData("POST", "/perguntar", "http://localhost:5080", null, true, true)]
    [InlineData("POST", "/agendamentos", "http://localhost:5080", null, true, true)]
    [InlineData("POST", "/feedback", "http://localhost:5080", null, true, false)]
    // /resumo e /api/: só a própria página ou o endereço escrito à mão
    [InlineData("GET", "/resumo", null, "same-origin", false, false)]
    [InlineData("GET", "/api/creditos", null, "none", false, false)]
    [InlineData("GET", "/resumo", null, "same-site", false, true)]
    [InlineData("GET", "/api/creditos", null, "cross-site", false, true)]
    [InlineData("GET", "/api/creditos", null, null, false, true)]
    [InlineData("GET", "/", null, "cross-site", false, false)]                       // a página em si abre de qualquer lado
    public void Recusa_RegrasDeAcesso(string metodo, string caminho, string? origem, string? secFetchSite, bool vendedor, bool recusado)
        => Assert.Equal(recusado, ApiPainel.Recusa(metodo, caminho, origem, secFetchSite, "http://localhost:5080/", vendedor) is not null);

    [Fact]
    public void Recusa_OrigemComPortaPorOmissaoEMaiusculas_Aceite()
        => Assert.Null(ApiPainel.Recusa("POST", "/feedback", "https://vendas.empresa.local", null, "https://Vendas.Empresa.local:443/", false));

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

        Assert.Equal(vendedor, Perfis.Resolver(login, mapa, "DOM\\dono")!.Vendedor);
    }

    [Fact]
    public void Perfis_LoginForaDoFicheiro_FicaSemAcesso() =>
        Assert.Null(Perfis.Resolver("DOM\\intruso", new Dictionary<string, string>(), "DOM\\intruso"));

    [Fact]
    public void Perfis_SemAutenticacao_NinguemEntra() =>
        Assert.Null(Perfis.Resolver(null, null, "DOM\\dono"));

    [Fact]
    public void Perfis_SemFicheiro_SoODonoEntraEVeTudo()
    {
        var mapa = Perfis.Ler(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));

        Assert.Null(mapa);
        Assert.Null(Perfis.Resolver("DOM\\diana", mapa, "DOM\\dono"));   // outra sessão do mesmo PC (RDS)
        Assert.Equal(new Perfil("dom\\DONO", null), Perfis.Resolver("dom\\DONO", mapa, "DOM\\dono"));
    }

    [Fact]
    public void LinhaAuditoria_TemQuemOQueEQuando() =>
        Assert.Equal("2026-11-10 09:05:00\tDOM\\ana\tN 10/2026/5",
            ApiPainel.LinhaAuditoria(new DateTimeOffset(2026, 11, 10, 9, 5, 0, TimeSpan.Zero), "DOM\\ana", "N 10/2026/5"));
}

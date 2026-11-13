using Microsoft.Extensions.Logging.Abstractions;
using Vendas.Servidor.Ferramentas;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Tests.Dados;

namespace Vendas.Servidor.Tests.Ferramentas;

public class VendasToolsTests
{
    private static VendasTools CriarSut(FakeVendasRepo repo) => new(repo, NullLogger<VendasTools>.Instance);

    // Relógio fixo: o "hoje" da tool deixa de depender do dia em que os testes correm.
    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    [Fact]
    public async Task Hoje_VemDoRelogioInjetado()
    {
        var sut = new VendasTools(new FakeVendasRepo(), NullLogger<VendasTools>.Instance,
            new RelogioFixo(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal("ano tem de ser de 2000 a 2026.", await sut.ConsultarVendasAsync(ano: 2027));
    }

    [Theory]
    [InlineData(DimensaoVenda.Trimestre, Periodo.TrimestreAtual)]
    [InlineData(DimensaoVenda.Mes, Periodo.MesAnterior)]
    public async Task AgruparNoTempo_NumPeriodoDeUmSoGrupo_PassaParaOAnoAtual(DimensaoVenda agrupar, Periodo periodo)
    {
        var repo = new FakeVendasRepo();
        var sut = new VendasTools(repo, NullLogger<VendasTools>.Instance,
            new RelogioFixo(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero)));

        await sut.ConsultarVendasAsync(agrupar, periodo);

        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 29)), repo.UltimoPeriodo);
    }

    [Fact]
    public async Task ConsultarCreditos_PassaPaginaTipoPesquisa_EDevolveContadores()
    {
        var repo = new FakeVendasRepo
        {
            Creditos = new([new("N 1/2026/3", "N", new DateOnly(2026, 9, 2), "1003", "Casa Costa", -10m)], 7, 2),
        };

        var json = await CriarSut(repo).ConsultarCreditosAsync(tipo: "N", pesquisa: "costa", pagina: 3, limite: 15);

        Assert.Equal(("N", "costa", 30, 15), repo.UltimaPaginaCreditos);
        using var d = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("1003", d.RootElement.GetProperty("itens")[0].GetProperty("clienteId").GetString());
        Assert.Equal(7, d.RootElement.GetProperty("contadores").GetProperty("N").GetInt32());
        Assert.Equal(2, d.RootElement.GetProperty("contadores").GetProperty("C").GetInt32());
    }

    [Fact]
    public async Task ConsultarCreditos_TipoForaDaLista_NaoVaiAoRepo()
    {
        var repo = new FakeVendasRepo();

        Assert.Equal("tipo tem de ser N ou C.", await CriarSut(repo).ConsultarCreditosAsync(tipo: "F"));
        Assert.Null(repo.UltimaPaginaCreditos);
    }

    [Fact]
    public async Task ConsultarCreditos_Vendedor_GanhaAoFiltroPedidoNaMesmaColuna()
    {
        var repo = new FakeVendasRepo();

        await CriarSut(repo).ConsultarCreditosAsync(coluna: Campo.Vendedor, valor: "Outro", vendedor: "Ana");

        Assert.Equal("Ana", repo.UltimosFiltros![Campo.Vendedor]);
    }

    [Fact]
    public async Task ConsultarVendas_ClienteComFiltroDeVendedor_SoProcuraNosClientesDele()
    {
        // O resumo do painel pede cliente=A com o vendedor do perfil: a lista "Vários clientes…" não pode trazer clientes de outros.
        var repo = new FakeVendasRepo
        {
            Clientes = _ => [("1", "Alfa"), ("2", "Alfaiate")],
            Contagens = [new("Ana", 2, 2, 1)],
        };

        await CriarSut(repo).ConsultarVendasAsync(cliente: "A", coluna: Campo.Vendedor, valor: "Ana");

        Assert.Equal("Ana", repo.UltimosFiltrosProcura![Campo.Vendedor]);
    }

    [Fact]
    public async Task ConsultarDocumento_SomaTotais_ETipoEAnoVemDoNumero()
    {
        var repo = new FakeVendasRepo
        {
            Detalhe = new("N 10/2026/5", "N", new DateOnly(2026, 3, 1), "1003", "Casa Costa",
                [new("A1", "Artigo", -1, -11m, 1m, -10m, -2.3m), new("A2", "Outro", -2, -20m, 0m, -20m, -4.6m)]),
        };

        var json = await CriarSut(repo).ConsultarDocumentoAsync("N 10/2026/5", "Ana");

        Assert.Equal(("N 10/2026/5", "N", 2026), repo.UltimoDocumento);
        Assert.Equal("Ana", repo.UltimosFiltros![Campo.Vendedor]);
        using var d = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(-36.9m, d.RootElement.GetProperty("totais").GetProperty("total").GetDecimal());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, d.RootElement.GetProperty("origem").ValueKind);
    }

    [Theory]
    [InlineData("N 10/2026/5")]      // existe noutro perfil ou não existe: a resposta é a mesma
    [InlineData("N 10/0000/5")]      // ano impossível: não pode rebentar no DateOnly
    [InlineData("N'; DROP--")]       // nem chega ao repositório
    [InlineData("")]
    public async Task ConsultarDocumento_Inexistente_DevolveNaoEncontrado(string documento)
    {
        var json = await CriarSut(new FakeVendasRepo()).ConsultarDocumentoAsync(documento);

        Assert.Equal("""{"erro":"nao_encontrado"}""", json);
    }

    [Fact]
    public async Task Fixtures_VendedorSoVeOsSeusDocumentos_ENumDocumentoAlheioRecebeNaoEncontrado()
    {
        var sut = new VendasTools(new Vendas.Servidor.Modelos.Dados.RepoDemo(), NullLogger<VendasTools>.Instance);

        using var todos = System.Text.Json.JsonDocument.Parse(await sut.ConsultarCreditosAsync(Periodo.Ultimos12Meses, limite: 100));
        using var daAna = System.Text.Json.JsonDocument.Parse(await sut.ConsultarCreditosAsync(Periodo.Ultimos12Meses, limite: 100, vendedor: "Ana Demo"));
        var alheio = todos.RootElement.GetProperty("itens").EnumerateArray()
            .First(c => !daAna.RootElement.GetProperty("itens").EnumerateArray().Any(a => a.GetProperty("documento").GetString() == c.GetProperty("documento").GetString()));

        Assert.True(daAna.RootElement.GetProperty("itens").GetArrayLength() < todos.RootElement.GetProperty("itens").GetArrayLength());
        Assert.Equal("""{"erro":"nao_encontrado"}""", await sut.ConsultarDocumentoAsync(alheio.GetProperty("documento").GetString()!, "Ana Demo"));
    }

    [Fact]
    public async Task ConsultarDocumentos_DevolveJsonComDocumentoTipoEData_EPassaOFiltro()
    {
        var repo = new FakeVendasRepo
        {
            Documentos = new(
                [new("FT", 100.5m, 2)],
                [new("2026-09", "FT", 100.5m, 2)],
                [new("FT 1/2026/7", "FT", new DateOnly(2026, 9, 4), "Casa Costa", 60.25m)],
                [new("N 1/2026/3", "N", new DateOnly(2026, 9, 2), "Casa Costa", -10m)]),
        };

        var json = await CriarSut(repo).ConsultarDocumentosAsync(coluna: Campo.Distrito, valor: "Setúbal");

        using var d = System.Text.Json.JsonDocument.Parse(json);
        var ultimo = d.RootElement.GetProperty("ultimos")[0];
        Assert.Equal("FT 1/2026/7", ultimo.GetProperty("documento").GetString());
        Assert.Equal("FT", ultimo.GetProperty("tipo").GetString());
        Assert.Equal("04-09-2026", ultimo.GetProperty("data").GetString());
        Assert.Equal(100.5m, d.RootElement.GetProperty("tipos")[0].GetProperty("valor").GetDecimal());
        var mes = d.RootElement.GetProperty("meses")[0];
        Assert.Equal("2026-09", mes.GetProperty("mes").GetString());
        Assert.Equal("FT", mes.GetProperty("tipo").GetString());
        Assert.Equal(100.5m, mes.GetProperty("valor").GetDecimal());
        var credito = d.RootElement.GetProperty("creditos")[0];
        Assert.Equal("N", credito.GetProperty("tipo").GetString());
        Assert.Equal(-10m, credito.GetProperty("total").GetDecimal());
        Assert.Equal("Setúbal", repo.UltimosFiltros![Campo.Distrito]);
    }

    [Theory]
    [InlineData(60, "Ativo")]
    [InlineData(61, "Em risco")]
    [InlineData(90, "Em risco")]
    [InlineData(91, "Perdido")]
    public async Task ConsultarCarteira_EstadoContaDesdeAUltimaCompraRegistada(int dias, string esperado)
    {
        var ultimaVenda = new DateOnly(2026, 9, 4);
        var repo = new FakeVendasRepo
        {
            Carteira = new(
                [new("7", "ALM", ultimaVenda.AddDays(-dias)), new("8", "Balcão", ultimaVenda)],
                [new("7", "2026-05", 12.5m, 2), new("7", "2026-06", 7.5m, 1)]),
        };

        using var d = System.Text.Json.JsonDocument.Parse(await CriarSut(repo).ConsultarCarteiraAsync());
        var cliente = Assert.Single(d.RootElement.GetProperty("clientes").EnumerateArray());

        Assert.Equal(esperado, cliente.GetProperty("estado").GetString());
        Assert.Equal("ALM", cliente.GetProperty("zona").GetString());
        Assert.Equal(3, cliente.GetProperty("documentos").GetInt32());
        Assert.Equal("2026-05", cliente.GetProperty("meses")[0].GetProperty("mes").GetString());
    }

    [Fact]
    public async Task ConsultarCarteira_FicaComOsMaioresPeloValorDoPeriodo()
    {
        var repo = new FakeVendasRepo { Carteira = new([], [new("7", "2026-05", 10m, 1), new("8", "2026-05", 30m, 1), new("8", "2026-06", -5m, 1)]) };

        using var d = System.Text.Json.JsonDocument.Parse(await CriarSut(repo).ConsultarCarteiraAsync(limite: 1));

        Assert.Equal("8", Assert.Single(d.RootElement.GetProperty("clientes").EnumerateArray()).GetProperty("id").GetString());
    }

    [Fact]
    public async Task ConsultarCarteira_ClienteSemCompras_FicaSemEstado()
    {
        var repo = new FakeVendasRepo { Carteira = new([new("7", "ALM", null), new("8", "ALM", new DateOnly(2026, 9, 4))], [new("7", "2026-05", -3m, 1)]) };

        using var d = System.Text.Json.JsonDocument.Parse(await CriarSut(repo).ConsultarCarteiraAsync());

        Assert.Equal(System.Text.Json.JsonValueKind.Null, d.RootElement.GetProperty("clientes")[0].GetProperty("estado").ValueKind);
    }

    [Fact]
    public async Task ConsultarDocumentos_ColunaSemValor_ERecusado()
    {
        Assert.Equal("coluna e valor têm de ser usados juntos.",
            await CriarSut(new FakeVendasRepo()).ConsultarDocumentosAsync(coluna: Campo.Distrito));
    }

    [Fact]
    public async Task Menores_ChegaAoRepoEDizNoTitulo()
    {
        var repo = new FakeVendasRepo { Valores = [new ValorAgrupado("Seixal", 10m, 1, 1, 10m, 1)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Localidade, menores: true);

        Assert.True(repo.UltimoMenores);
        Assert.Contains("do menor para o maior", r);
    }

    [Fact]
    public async Task Menores_ComCliente_DevolveErroParaCorrigir()
        => Assert.StartsWith("menores não se combina",
            await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(DimensaoVenda.Cliente, menores: true));

    [Fact]
    public async Task Cliente_ChamaRankingPorCliente()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Cliente, limite: 5);

        Assert.Null(repo.UltimaDimensao); // não foi ao RankingPorDimensao
        Assert.Equal(5, repo.UltimoLimite);
    }

    [Fact]
    public async Task Marca_ChamaRankingPorDimensaoComADimensao()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Marca);

        Assert.Equal(DimensaoVenda.Marca, repo.UltimaDimensao);
    }

    [Fact]
    public async Task Limite_AcimaDe100_FicaEm100()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(limite: 500);
        Assert.Equal(100, repo.UltimoLimite);
    }

    [Fact]
    public async Task Falha_DevolveErroNeutro()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new InvalidOperationException("SQL secreto") };
        var r = await CriarSut(repo).ConsultarVendasAsync();

        Assert.Equal(Respostas.ErroNeutro, r);
    }

    [Fact]
    public async Task Texto_ListaNumeradaNumerosPtEAvisaCorte()
    {
        // O modelo trocava dígitos ao converter 1234.50 sozinho: os números já vêm em pt-PT, prontos a copiar.
        var repo = new FakeVendasRepo { Valores = [new("Bento, Filhos", 1234.5m, 0, 3, 2469m, 2, 1371.67m, -137.17m, 283.94m)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Cliente);

        Assert.Contains("1. Bento, Filhos – 1 234,50 € (50,0%)", r);
        Assert.Contains("total 2 469,00 €", r);
        Assert.Contains("Mostro 1 de 2 clientes.", r);
        Assert.DoesNotContain("posicao;", r); // já não há CSV
    }

    [Fact]
    public async Task Texto_NomeComPontoEVirgula_ApareceTalComo()
    {
        var repo = new FakeVendasRepo { Valores = [new("Ana; Rui", 10m, 0, 1, 10m, 1)] };
        var r = await CriarSut(repo).ConsultarVendasAsync();

        Assert.Contains("1. Ana; Rui – 10,00 € (100,0%)", r);
        Assert.Contains("Lista completa: 1 clientes.", r);
    }

    [Fact]
    public async Task Cliente_PassaAoRepoEApareceNoCabecalho()
    {
        var repo = new FakeVendasRepo { Valores = [new("2024-12", 30.6m, 1, 1, 30.6m, 1)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, cliente: " 1003 ");

        Assert.Equal("1003", repo.UltimoCliente);
        Assert.Equal(DimensaoVenda.Mes, repo.UltimaDimensao);
        Assert.Contains("cliente \"Cliente 1003 (1003)\"", r);
        Assert.Contains("1. 2024-12 – 30,60 €", r);
    }

    [Fact]
    public async Task Mes_CadaLinhaTrazOValorMedioPorDocumento()
    {
        var repo = new FakeVendasRepo { Valores = [new("2026-01", 300m, 0, 4, 500m, 2), new("2026-02", 200m, 0, 0, 500m, 2)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes);

        Assert.Contains("1. 2026-01 – 300,00 € (60,0%) · média 75,00 €", r);
        Assert.Contains("2. 2026-02 – 200,00 € (40,0%)\n", r.ReplaceLineEndings("\n")); // sem documentos: sem média
    }

    [Fact]
    public async Task ClienteInexistente_DizQueNaoExisteSemSomarVendas()
    {
        var repo = new FakeVendasRepo { Clientes = _ => [] };
        var r = await CriarSut(repo).ConsultarVendasAsync(cliente: "Bentoo");

        Assert.Equal("Cliente \"Bentoo\" não encontrado. Indique o ID ou o início do nome.", r);
        Assert.Null(repo.UltimoPeriodo); // não chegou a pedir vendas
    }

    [Fact]
    public async Task VariosClientes_PedeOIdEmVezDeSomar()
    {
        var repo = new FakeVendasRepo { Clientes = _ => [("7", "Casa Costa"), ("9", "Casa Silva, Lda")] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, cliente: "Casa");

        Assert.StartsWith("Vários clientes começam por \"Casa\"", r);
        Assert.Contains("\n7,Casa Costa\n9,\"Casa Silva, Lda\"", r);
        Assert.Null(repo.UltimoPeriodo);
    }

    [Fact]
    public async Task IdExato_GanhaAosNomesComOMesmoInicio()
    {
        var repo = new FakeVendasRepo { Clientes = _ => [("100", "Alfa"), ("1001", "100 Limites")] };
        await CriarSut(repo).ConsultarVendasAsync(cliente: "100");

        Assert.Equal("100", repo.UltimoCliente);
    }

    [Fact]
    public async Task TotalZero_NaoDividePorZero()
    {
        var repo = new FakeVendasRepo { Valores = [new("Ana", 0m, 2m, 1, 0m, 1)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Artigo);

        // Sem percentagem: ao valor segue-se logo a média.
        Assert.Contains("1. Ana – 0,00 € · média 0,00 €" + Environment.NewLine, r);
    }

    [Fact]
    public async Task TotalNegativoOuParteMaiorQueOTodo_SemPercentagem()
    {
        // Total do período negativo (mais notas de crédito do que vendas): -56,5% não quer dizer nada.
        var repo = new FakeVendasRepo { Valores = [new("Ana", 16m, 2m, 1, -29m, 2), new("Rui", -45m, 1m, 1, -29m, 2)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Artigo);

        Assert.Contains("1. Ana – 16,00 € · média", r);
        Assert.DoesNotContain("%", r);
    }

    [Fact]
    public async Task MesEAno_PedemEsseMes()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(mes: 3, ano: 2026);
        Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 1)), repo.UltimoPeriodo);
    }

    [Fact]
    public async Task Mes13_DevolveMensagem()
        => Assert.Equal("mes tem de ser de 1 a 12.", await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(mes: 13));

    // ---- Intervalo livre (dataInicio/dataFim): trimestre, semestre, semana.

    [Fact]
    public async Task Datas_PedemEsseIntervalo_ComOFimIncluido()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(dataInicio: "01-01-2026", dataFim: "31-03-2026");

        // "ate" é exclusivo no repositório: 31-03 incluído = até 01-04.
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 1)), repo.UltimoPeriodo);
    }

    [Fact]
    public async Task Datas_TemPrioridadeSobreMesEAno()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(mes: 9, ano: 2026, dataInicio: "01-08-2026", dataFim: "07-08-2026");

        Assert.Equal((new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 8)), repo.UltimoPeriodo);
    }

    [Fact]
    public async Task Datas_SoUma_DevolveMensagem()
        => Assert.Equal("dataInicio e dataFim têm de ser usados juntos.",
            await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(dataInicio: "01-01-2026"));

    [Fact]
    public async Task Datas_FormatoErrado_DevolveMensagem()
        => Assert.Equal("Datas no formato dd-MM-aaaa, ex.: 01-08-2026.",
            await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(dataInicio: "2026-01-01", dataFim: "2026-03-31"));

    [Fact]
    public async Task Datas_FimAntesDoInicio_DevolveMensagem()
        => Assert.Equal("dataFim tem de ser igual ou depois de dataInicio.",
            await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(dataInicio: "31-03-2026", dataFim: "01-01-2026"));

    // ----

    [Fact]
    public async Task ColunaSemValor_DevolveMensagem()
        => Assert.Equal("coluna e valor têm de ser usados juntos.",
            await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(coluna: Campo.Localidade));

    [Fact]
    public async Task Localidade_ChegaAoRepoEDaAMedia()
    {
        var repo = new FakeVendasRepo { Valores = [new("Ana (1)", 10m, 0, 2, 10m, 1, TotalDocumentos: 2)], Clientes = _ => [] };
        var r = await CriarSut(repo).ConsultarVendasAsync(coluna: Campo.Localidade, valor: " Seixal ");

        Assert.Equal("Seixal", repo.UltimosFiltros![Campo.Localidade]);
        Assert.Contains("Localidade = Seixal", r);
        Assert.Contains("2 documentos.", r);
        Assert.Contains("Valor médio por documento: 5,00 €.", r); // a média geral, numa linha só sua
    }

    [Fact]
    public async Task ValorSemColuna_UsaAColunaDeAgrupar()
    {
        var repo = new FakeVendasRepo { Valores = [new("Lisboa", 10m, 0, 1, 10m, 1)], Clientes = _ => [] };
        await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Localidade, valor: "Lisboa");

        Assert.Equal("Lisboa", repo.UltimosFiltros![Campo.Localidade]);
    }

    [Fact]
    public async Task ValorSemColuna_ComArtigo_DizQueColunaUsar()
        => Assert.StartsWith("valor precisa de coluna",
            await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(DimensaoVenda.Artigo, valor: "Lisboa"));

    [Fact]
    public async Task LocalidadeQueEDistrito_FiltraPeloDistrito()
    {
        var repo = new FakeVendasRepo
        {
            ContagensPor = (_, f) => f.ContainsKey(Campo.Distrito) ? [new ContagemCliente("Setubal", 5, 5, 1)] : [],
            Valores = [new("Artigo A", 10m, 0, 1, 10m, 1)],
        };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Artigo, coluna: Campo.Localidade, valor: "Setubal");

        Assert.Contains("Distrito = Setubal", r);
        Assert.Contains("1. Artigo A", r);
    }

    [Fact]
    public async Task LocalidadeInexistente_ListaAsExistentes()
    {
        var repo = new FakeVendasRepo
        {
            ContagensPor = (_, f) => f.Count > 0 ? [] : [new ContagemCliente("Seixal", 5, 5, 1)],
            Clientes = _ => [],
        };
        var r = await CriarSut(repo).ConsultarVendasAsync(coluna: Campo.Localidade, valor: "Seixall");

        Assert.StartsWith("Nenhuma correspondência", r);
    }

    [Fact]
    public async Task ValorQueEUmCliente_FiltraPeloCliente()
    {
        // "Vendas do cliente Público Com Desconto" vinha como coluna=Localidade: não há essa localidade, há esse cliente.
        var repo = new FakeVendasRepo
        {
            Valores = [new("2026-01", 10m, 1m, 1, 10m, 1)],
            Clientes = c => c.StartsWith("Público") ? [("1003", "PÚBLICO COM DESCONTO OFICINAL")] : [],
        };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, coluna: Campo.Localidade, valor: "Público Com Desconto");

        Assert.Equal("1003", repo.UltimoCliente);
        Assert.Empty(repo.UltimosFiltros!);
        Assert.Contains("cliente \"PÚBLICO COM DESCONTO OFICINAL (1003)\"", r);
    }

    [Fact]
    public async Task Localidade_AgrupaVendasPorLocalidade()
    {
        var repo = new FakeVendasRepo { Valores = [new("Seixal", 100m, 4m, 2, 400m, 3, TotalDocumentos: 8)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Localidade);

        Assert.Equal(DimensaoVenda.Localidade, repo.UltimaDimensao);
        Assert.Contains("Vendas por localidade", r);
        Assert.Contains("1. Seixal – 100,00 € (25,0%) · média 50,00 €", r);
        Assert.Contains("Mostro 1 de 3 localidades.", r);
    }

    [Fact]
    public async Task Quantidade_OrdenaPorUnidadesEPoeAQuantidadeAFrente()
    {
        var repo = new FakeVendasRepo { Valores = [new("Filtro (F1)", 50m, 627m, 3, 50m, 1)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Artigo, ordenar: OrdenarVenda.Quantidade);

        Assert.True(repo.UltimoPorQuantidade);
        Assert.Contains("Vendas por artigo, por quantidade", r);
        // A quantidade vem antes do valor: é ela que responde à pergunta.
        Assert.Contains("1. Filtro (F1) – 627 unidades (50,00 €)", r);
    }

    [Fact]
    public async Task QuantidadePorCliente_Recusa()
    {
        var repo = new FakeVendasRepo();
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Cliente, ordenar: OrdenarVenda.Quantidade);

        Assert.StartsWith("ordenar=Quantidade não se aplica a Cliente", r);
        Assert.Null(repo.UltimoPeriodo);
    }

    [Fact]
    public async Task DadosAcabamAntesDoFimDoPeriodo_Avisa()
    {
        var repo = new FakeVendasRepo { Valores = [new("2026-09", 10m, 1, 1, 10m, 1, TotalDocumentos: 1, UltimaData: new DateOnly(2026, 9, 5))] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, mes: 9, ano: 2026);

        Assert.Contains("Atenção: a última venda registada neste período é de 05-09-2026.", r);
    }

    [Fact]
    public async Task DadosAteAoFimDoPeriodo_NaoAvisa()
    {
        var repo = new FakeVendasRepo { Valores = [new("2026-03", 10m, 1, 1, 10m, 1, TotalDocumentos: 1, UltimaData: new DateOnly(2026, 3, 30))] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, mes: 3, ano: 2026);

        Assert.DoesNotContain("Atenção", r);
    }

    [Fact]
    public async Task PeriodoVazio_DizAteQuandoHaDados()
    {
        var repo = new FakeVendasRepo { UltimaVenda = new DateOnly(2026, 9, 5) };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Artigo, Periodo.MesAtual);

        Assert.StartsWith("Sem vendas: mês atual", r);
        Assert.Contains("Atenção: as últimas vendas registadas são de 05-09-2026.", r);
    }

    [Fact]
    public async Task SemVendas_ListaClientesComUltimaCompra()
    {
        var repo = new FakeVendasRepo
        {
            SemVendas = [new("Ana (1)", new DateOnly(2026, 5, 2), 7), new("Rui (2)", null, 7)],
        };
        var r = await CriarSut(repo).ConsultarVendasAsync(periodo: Periodo.Ultimos90Dias, semVendas: true, limite: 2);

        Assert.Null(repo.UltimaDimensao); // não foi aos rankings
        Assert.Contains("Clientes sem vendas, últimos 90 dias", r);
        Assert.Contains(": 7 clientes.", r);
        Assert.Contains("1. Ana (1) – última compra a 02-05-2026", r);
        Assert.Contains("2. Rui (2) – nunca comprou", r);
        Assert.Contains("Mostro 2 de 7 clientes.", r);
    }

    [Fact]
    public async Task SemVendas_NenhumCliente_DizQueTodosCompraram()
    {
        var r = await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(semVendas: true);
        Assert.StartsWith("0 clientes sem vendas: ano atual", r);
    }

    [Fact]
    public async Task SemVendas_ComCliente_Recusa()
        => Assert.StartsWith("semVendas lista vários clientes",
            await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(semVendas: true, cliente: "1003"));

    // ---- Evolução no tempo (Mes, Trimestre) e emCada.

    [Fact]
    public async Task Mes_VemSempreCompletoEDizOMelhor()
    {
        var repo = new FakeVendasRepo
        {
            Valores = [new("2026-08", 200m, 5m, 2, 330m, 3), new("2026-09", 30m, 1m, 1, 330m, 3), new("2026-07", 100m, 9m, 1, 330m, 3)],
        };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, limite: 1);

        Assert.Equal(100, repo.UltimoLimite); // limite ignorado: 12 meses não podem perder os mais recentes
        Assert.Contains("Vendas por mês, ano atual", r);
        Assert.Contains("Melhor mês: 2026-08 – 200,00 €.", r);
    }

    [Fact]
    public async Task MesPorQuantidade_MelhorEmUnidades()
    {
        var repo = new FakeVendasRepo { Valores = [new("2026-08", 200m, 5m, 2, 300m, 2), new("2026-07", 100m, 9m, 1, 300m, 2)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, ordenar: OrdenarVenda.Quantidade);

        Assert.Contains("Melhor mês: 2026-07 – 9 unidades (100,00 €).", r);
        Assert.StartsWith("Vendas por mês, em quantidade,", r); // distinto da análise em euros
        Assert.Contains("Total em unidades: 14 unidades.", r);
    }

    [Fact]
    public async Task Trimestre_ChamaODimensaoTrimestre()
    {
        var repo = new FakeVendasRepo { Valores = [new("2026-T1", 10m, 1m, 1, 30m, 2), new("2026-T2", 20m, 1m, 1, 30m, 2)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Trimestre);

        Assert.Equal(DimensaoVenda.Trimestre, repo.UltimaDimensao);
        Assert.Contains("Vendas por trimestre", r);
        Assert.Contains("Melhor trimestre: 2026-T2 – 20,00 €.", r);
        Assert.Contains("Lista completa: 2 trimestres.", r);
    }

    [Fact]
    public async Task EmCada_PercentagemDentroDoGrupo()
    {
        var repo = new FakeVendasRepo
        {
            Valores = [new("Setúbal: Filtro (F1)", 50m, 2m, 1, 1000m, 2, TotalGrupo: 200m), new("Lisboa: Óleo (O1)", 10m, 1m, 1, 1000m, 2, TotalGrupo: 40m)],
        };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Artigo, emCada: DimensaoVenda.Distrito);

        Assert.Equal(DimensaoVenda.Distrito, repo.UltimoEmCada);
        Assert.StartsWith("Melhor artigo em cada distrito, ano atual", r);
        Assert.Contains("1. Setúbal: Filtro (F1) – 50,00 € (25,0% do distrito)", r);
        Assert.Contains("2. Lisboa: Óleo (O1) – 10,00 € (25,0% do distrito)", r);
        Assert.Contains("Lista completa: 2 distritos.", r);
        Assert.DoesNotContain("Melhor distrito", r);
    }

    [Fact]
    public async Task EmCadaLocalidade_UsaDa()
    {
        var repo = new FakeVendasRepo { Valores = [new("Seixal: 2026-08", 50m, 2m, 1, 50m, 1, TotalGrupo: 100m)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, emCada: DimensaoVenda.Localidade);

        Assert.Contains("(50,0% da localidade)", r);
    }

    [Fact]
    public async Task EmCadaMes_VemCompleto()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Localidade, emCada: DimensaoVenda.Mes, limite: 3);

        Assert.Equal(100, repo.UltimoLimite);
    }

    // ---- compararAnoAnterior.

    private static VendasTools CriarSutEm(FakeVendasRepo repo, int ano, int mes, int dia) =>
        new(repo, NullLogger<VendasTools>.Instance, new RelogioFixo(new DateTimeOffset(ano, mes, dia, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Comparar_MesmosDiasUmAnoAntesEEmparelhaPorMes()
    {
        var repo = new FakeVendasRepo
        {
            ValoresPor = desde => desde.Year == 2026
                ? [new("2026-01", 110m, 1m, 1, 160m, 2, TotalDocumentos: 2), new("2026-02", 50m, 1m, 1, 160m, 2, TotalDocumentos: 2)]
                : [new("2025-01", 100m, 1m, 1, 200m, 2, TotalDocumentos: 2), new("2025-02", 100m, 1m, 1, 200m, 2, TotalDocumentos: 2)],
        };
        var r = await CriarSutEm(repo, 2026, 10, 26).ConsultarVendasAsync(DimensaoVenda.Mes, compararAnoAnterior: true);

        // 01-01-2026 a 26-10-2026 contra 01-01-2025 a 26-10-2025: os mesmos dias, não o ano 2025 inteiro.
        Assert.Equal((new DateOnly(2025, 1, 1), new DateOnly(2025, 10, 27), VendasTools.MaxLinhasComparacao), repo.ChamadasRanking[1]);
        Assert.Contains("Comparado com 01-01-2025 a 26-10-2025: total 200,00 € (-20,0%).", r);
        // Por mês não se compara o n.º de meses: só documentos e valor médio (160/2 = 80 € contra 200/2 = 100 €)
        Assert.Contains("Um ano antes: 2 documentos (0,0%), valor médio por documento 100,00 € (-20,0%).", r);
        Assert.Contains("1. 2026-01 – 110,00 € · 2025: 100,00 € (+10,0%)", r);
        Assert.Contains("2. 2026-02 – 50,00 € · 2025: 100,00 € (-50,0%)", r);
    }

    [Fact]
    public async Task Comparar_PorCliente_DizClientesDocumentosEValorMedioUmAnoAntes()
    {
        // 2026: 3 clientes, 4 documentos, 300 €; 2025: 2 clientes, 5 documentos, 250 €
        var repo = new FakeVendasRepo
        {
            ValoresPor = desde => desde.Year == 2026
                ? [new("Ana (1)", 300m, 0, 4, 300m, 3, TotalDocumentos: 4)]
                : [new("Ana (1)", 250m, 0, 5, 250m, 2, TotalDocumentos: 5)],
            Clientes = _ => [],
        };
        var r = await CriarSutEm(repo, 2026, 10, 26).ConsultarVendasAsync(DimensaoVenda.Cliente, compararAnoAnterior: true);

        Assert.Contains("Um ano antes: 2 clientes (+50,0%), 5 documentos (-20,0%), valor médio por documento 50,00 € (+50,0%).", r);
    }

    [Fact]
    public async Task Comparar_UmCliente_UsaSingular()
    {
        var repo = new FakeVendasRepo
        {
            ValoresPor = _ => [new("Ana (1)", 300m, 0, 4, 300m, 1, TotalDocumentos: 4)],
            Clientes = _ => [],
        };
        var r = await CriarSutEm(repo, 2026, 10, 26).ConsultarVendasAsync(DimensaoVenda.Cliente, compararAnoAnterior: true);

        Assert.Contains("em 1 cliente,", r);
        Assert.Contains("Um ano antes: 1 cliente (0,0%),", r);
    }

    [Fact]
    public async Task Comparar_Ultimos12Meses_OutubroDeCadaAnoComOSeu()
    {
        // 27-10-2025 a 26-10-2026 tem dois outubros: cada um emparelha com o do seu ano anterior.
        var repo = new FakeVendasRepo
        {
            ValoresPor = desde => desde.Year == 2025
                ? [new("2025-10", 10m, 1m, 1, 30m, 2), new("2026-10", 20m, 1m, 1, 30m, 2)]
                : [new("2024-10", 5m, 1m, 1, 45m, 2), new("2025-10", 40m, 1m, 1, 45m, 2)],
        };
        var r = await CriarSutEm(repo, 2026, 10, 26).ConsultarVendasAsync(DimensaoVenda.Mes, Periodo.Ultimos12Meses, compararAnoAnterior: true);

        Assert.Contains("2025-10 – 10,00 € · um ano antes: 5,00 € (+100,0%)", r);
        Assert.Contains("2026-10 – 20,00 € · um ano antes: 40,00 € (-50,0%)", r);
    }

    [Fact]
    public async Task Comparar_SemParOuSemBase_NaoInventaPercentagem()
    {
        var repo = new FakeVendasRepo
        {
            ValoresPor = desde => desde.Year == 2026
                ? [new("Filtro (F1)", 10m, 1m, 1, 20m, 2), new("Óleo (O1)", 10m, 1m, 1, 20m, 2)]
                : [new("Óleo (O1)", 0m, 1m, 1, 0m, 1)],
        };
        var r = await CriarSutEm(repo, 2026, 10, 26).ConsultarVendasAsync(DimensaoVenda.Artigo, compararAnoAnterior: true);

        Assert.Contains("1. Filtro (F1) – 10,00 € · 2025: –", r);
        Assert.Contains("2. Óleo (O1) – 10,00 € · 2025: 0,00 €", r); // base 0: sem % (não divide por zero)
        Assert.Contains("total 0,00 €.", r);
        Assert.StartsWith("Vendas por artigo", r); // o agente continua a mostrar direto
    }

    [Fact]
    public async Task Comparar_PeriodoVazio_NaoVaiAoAnoAnterior()
    {
        var repo = new FakeVendasRepo();
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, compararAnoAnterior: true);

        Assert.StartsWith("Sem vendas:", r);
        Assert.Single(repo.ChamadasRanking);
    }

    [Fact]
    public async Task Comparar_DadosPorAtualizar_AnoAnteriorCortaNaUltimaVenda()
    {
        var repo = new FakeVendasRepo
        {
            ValoresPor = desde => desde.Year == 2026
                ? [new("2026-09", 10m, 1m, 1, 10m, 1, TotalDocumentos: 1, UltimaData: new DateOnly(2026, 9, 4))]
                : [new("2025-09", 10m, 1m, 1, 10m, 1, TotalDocumentos: 1)],
        };
        var r = await CriarSutEm(repo, 2026, 10, 27).ConsultarVendasAsync(DimensaoVenda.Mes, compararAnoAnterior: true);

        // 01-01-2026 a 04-09-2026 (onde os dados acabam) contra os mesmos dias de 2025, não até 27-10-2025.
        Assert.Equal((new DateOnly(2025, 1, 1), new DateOnly(2025, 9, 5), VendasTools.MaxLinhasComparacao), repo.ChamadasRanking[1]);
        Assert.Contains("Comparado com 01-01-2025 a 04-09-2025", r);
    }

    // ---- Dois meses comparados entre si (intervalo de meses inteiros).

    [Fact]
    public async Task MesesInteiros_DizAVariacaoDoPrimeiroParaOUltimo()
    {
        var repo = new FakeVendasRepo
        {
            Valores = [new("2026-01", 200m, 1m, 1, 590m, 3), new("2026-03", 240m, 1m, 1, 590m, 3), new("2026-05", 150m, 1m, 1, 590m, 3)],
        };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, dataInicio: "01-01-2026", dataFim: "31-05-2026");

        Assert.Contains("Variação janeiro → maio: -25,0%.", r);
    }

    [Fact]
    public async Task AnoAtual_NaoDizVariacao() // o mês atual vai a meio: a queda seria falsa
    {
        var repo = new FakeVendasRepo { Valores = [new("2026-01", 200m, 1m, 1, 210m, 2), new("2026-10", 10m, 1m, 1, 210m, 2)] };
        var r = await CriarSutEm(repo, 2026, 10, 27).ConsultarVendasAsync(DimensaoVenda.Mes);

        Assert.DoesNotContain("Variação", r);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, DimensaoVenda.Distrito)]
    public async Task Comparar_ComSemVendasOuEmCada_Recusa(bool semVendas, DimensaoVenda? emCada)
        => Assert.Equal("compararAnoAnterior não se combina com semVendas nem emCada. Chama de novo sem eles.",
            await CriarSut(new FakeVendasRepo()).ConsultarVendasAsync(DimensaoVenda.Artigo, semVendas: semVendas, emCada: emCada, compararAnoAnterior: true));

    [Fact]
    public async Task EmCadaIgualAAgrupar_Ignora()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, emCada: DimensaoVenda.Mes);

        Assert.Equal(DimensaoVenda.Mes, repo.UltimaDimensao);
        Assert.Null(repo.UltimoEmCada);
    }

    [Theory]
    [InlineData(DimensaoVenda.Cliente, DimensaoVenda.Distrito, "emCada não se aplica a Cliente")]
    [InlineData(DimensaoVenda.Artigo, DimensaoVenda.Cliente, "emCada não se aplica a Cliente")]
    [InlineData(DimensaoVenda.Localidade, DimensaoVenda.Distrito, "agrupar e emCada não podem ser os dois da ficha")]
    public async Task EmCada_CombinacoesInvalidas_Recusa(DimensaoVenda agrupar, DimensaoVenda emCada, string erro)
    {
        var repo = new FakeVendasRepo();
        var r = await CriarSut(repo).ConsultarVendasAsync(agrupar, emCada: emCada);
        Assert.StartsWith(erro, r);
        // Diz como corrigir: sem isso o modelo desistia e mostrava o erro ao utilizador.
        Assert.Matches("Chama de novo sem emCada|use agrupar=", r);
        Assert.DoesNotContain('\n', r); // uma linha: o agente não a mostra direto nem a conta como dados
        Assert.Null(repo.UltimoPeriodo); // não chegou ao repo
    }
}
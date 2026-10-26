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

        // Sem percentagem: a linha acaba logo a seguir ao valor.
        Assert.Contains("1. Ana – 0,00 €" + Environment.NewLine, r);
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
        var repo = new FakeVendasRepo { Valores = [new("Ana (1)", 10m, 0, 2, 10m, 1, TotalDocumentos: 2)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(coluna: Campo.Localidade, valor: " Seixal ");

        Assert.Equal("Seixal", repo.UltimosFiltros![Campo.Localidade]);
        Assert.Contains("Localidade = Seixal", r);
        Assert.Contains("2 documentos.", r);
        Assert.Contains("Valor médio por documento: 5,00 €.", r); // a média geral, numa linha só sua
    }

    [Fact]
    public async Task LocalidadeInexistente_ListaAsExistentes()
    {
        var repo = new FakeVendasRepo
        {
            ContagensPor = (_, f) => f.Count > 0 ? [] : [new ContagemCliente("Seixal", 5, 5, 1)],
        };
        var r = await CriarSut(repo).ConsultarVendasAsync(coluna: Campo.Localidade, valor: "Seixall");

        Assert.StartsWith("Nenhuma correspondência", r);
    }

    [Fact]
    public async Task Localidade_AgrupaVendasPorLocalidade()
    {
        var repo = new FakeVendasRepo { Valores = [new("Seixal", 100m, 4m, 2, 400m, 3, TotalDocumentos: 8)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Localidade);

        Assert.Equal(DimensaoVenda.Localidade, repo.UltimaDimensao);
        Assert.Contains("Vendas por localidade", r);
        Assert.Contains("1. Seixal – 100,00 € (25,0%)", r);
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

    [Theory]
    [InlineData(DimensaoVenda.Artigo, DimensaoVenda.Artigo, "emCada tem de ser diferente de agrupar.")]
    [InlineData(DimensaoVenda.Cliente, DimensaoVenda.Distrito, "emCada não se aplica a Cliente")]
    [InlineData(DimensaoVenda.Artigo, DimensaoVenda.Cliente, "emCada não se aplica a Cliente")]
    [InlineData(DimensaoVenda.Localidade, DimensaoVenda.Distrito, "agrupar e emCada não podem ser os dois da ficha")]
    public async Task EmCada_CombinacoesInvalidas_Recusa(DimensaoVenda agrupar, DimensaoVenda emCada, string erro)
    {
        var repo = new FakeVendasRepo();
        Assert.StartsWith(erro, await CriarSut(repo).ConsultarVendasAsync(agrupar, emCada: emCada));
        Assert.Null(repo.UltimoPeriodo); // não chegou ao repo
    }
}
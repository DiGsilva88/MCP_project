using Microsoft.Extensions.Logging.Abstractions;
using Vendas.Servidor.Ferramentas;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Tests.Dados;

namespace Vendas.Servidor.Tests.Ferramentas;

public class VistasToolsTests
{
    private const string ErroNeutro = "Não foi possivel consultar os dados neste momento";

    private static VistasTools CriarSut(FakeVendasRepo repo) =>
        new(repo, NullLogger<VistasTools>.Instance);

    private static string NL(params string[] linhas) =>
        string.Concat(linhas.Select(l => l + Environment.NewLine));

    // ---------- sensível ----------

    [Fact]
    public async Task Sensivel_MaioresNumaColunaDeTexto_RecusaSemIrAoRepo()
    {
        var repo = new FakeVendasRepo();
        var r = await CriarSut(repo).ConsultarSensivelAsync(CampoSensivel.Contribuinte, maiores: true);

        Assert.Equal("maiores só se aplica a VolumeVendas e Plafond.", r);
        Assert.Null(repo.UltimoSensivel);
    }

    [Fact]
    public async Task Sensivel_TopPorVolume_PassaMaioresEDevolveCsv()
    {
        var repo = new FakeVendasRepo { Pagina = new(["NomeCliente", "VolumeVendas"], [["Ana", "900000"]], 1) };
        var r = await CriarSut(repo).ConsultarSensivelAsync(CampoSensivel.VolumeVendas, maiores: true, limite: 3);

        Assert.Equal((CampoSensivel.VolumeVendas, true), repo.UltimoSensivel);
        Assert.Equal(3, repo.UltimoLimite);
        Assert.Equal("NomeCliente,VolumeVendas\nAna,900000\n# Lista completa: 1 clientes.\n", r);
    }

    [Fact]
    public async Task Sensivel_MostrarTambem_PassaAColunaEDevolveAsTresColunas()
    {
        var repo = new FakeVendasRepo
        {
            Pagina = new(["NomeCliente", "VolumeVendas", "Actividade"], [["Ana", "900000", "Oficina"]], 1),
        };
        var r = await CriarSut(repo).ConsultarSensivelAsync(
            CampoSensivel.VolumeVendas, maiores: true, mostrarTambem: Campo.Actividade);

        Assert.Equal(Campo.Actividade, repo.UltimoTambem);
        Assert.Equal("NomeCliente,VolumeVendas,Actividade\nAna,900000,Oficina\n# Lista completa: 1 clientes.\n", r);
    }

    // ---------- dados ----------

    [Fact]
    public async Task SemColuna_MostraAsColunasDasDuasViews()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarAsync();
        Assert.Equal(Enum.GetValues<Campo>(), repo.UltimoMostrar);
    }

    [Fact]
    public async Task SemLinhas_DevolveMensagemDeVazio()
    {
        Assert.Equal("Nenhuma linha encontrada com esse filtro.",
            await CriarSut(new FakeVendasRepo()).ConsultarAsync());
    }

    [Fact]
    public async Task ComColunaEValor_DevolveCsvEPassaOFiltro()
    {
        var repo = new FakeVendasRepo
        {
            Pagina = new(["NomeCliente", "Distrito"], [["Ana", "Lisboa"], ["Bento, Filhos", "Porto"]], 2),
        };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Distrito, "Lisboa");

        Assert.Equal("NomeCliente,Distrito\nAna,Lisboa\n\"Bento, Filhos\",Porto\n# Lista completa: 2 clientes.\n", resultado);
        var filtro = Assert.Single(repo.UltimosFiltros!);
        Assert.Equal(Campo.Distrito, filtro.Key);
        Assert.Equal("Lisboa", filtro.Value);
    }

    [Fact]
    public async Task SoColuna_ChamaORepositorioSemFiltros()
    {
        var repo = new FakeVendasRepo { Pagina = new(["NomeCliente", "Zona"], [["Ana", "Norte"]], 1) };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Zona);

        Assert.Equal("NomeCliente,Zona\nAna,Norte\n# Lista completa: 1 clientes.\n", resultado);
        Assert.Equal([Campo.Zona], repo.UltimoMostrar);
        Assert.Empty(repo.UltimosFiltros!);
    }

    [Fact]
    public async Task ValorSemColuna_PedeColuna()
    {
        var repo = new FakeVendasRepo();

        var resultado = await CriarSut(repo).ConsultarAsync(valor: "x");

        Assert.Equal("Indique uma coluna para contar ou filtrar.", resultado);
        Assert.Null(repo.UltimoMostrar);
    }

    [Fact]
    public async Task TotalMaiorQueLinhas_AvisaQueFoiCortado()
    {
        var repo = new FakeVendasRepo { Pagina = new(["NomeCliente"], [["Ana"], ["Bruno"]], 3) };

        var resultado = await CriarSut(repo).ConsultarAsync(limite: 2);

        Assert.EndsWith("# Mostrados 2 de 3. Filtre por valor ou use pagina=2,3... para ver o resto.\n", resultado);
    }

    // ---------- cruzar ----------

    [Fact]
    public async Task Listar_CruzadoComOutraView_AplicaOsDoisFiltros()
    {
        var repo = new FakeVendasRepo { Pagina = new(["NomeCliente"], [["Ana"]], 1) };

        await CriarSut(repo).ConsultarAsync(Campo.Zona, "Norte", cruzarCom: Campo.Pagamento, valorCruzado: "30 dias");

        Assert.Equal(new Dictionary<Campo, string> { [Campo.Zona] = "Norte", [Campo.Pagamento] = "30 dias" },
            repo.UltimosFiltros);
    }

    [Fact]
    public async Task Contar_CruzadoComOutraView_AgrupaPelaColunaEFiltraPelaCruzada()
    {
        var repo = new FakeVendasRepo { Contagens = [new("Norte", 4, 4, 1)] };

        await CriarSut(repo).ConsultarAsync(Campo.Zona, contar: true, cruzarCom: Campo.Pagamento, valorCruzado: "30 dias");

        Assert.Equal(Campo.Zona, repo.UltimoAgrupar);
        var filtro = Assert.Single(repo.UltimosFiltros!);
        Assert.Equal(Campo.Pagamento, filtro.Key);
        Assert.Equal("30 dias", filtro.Value);
    }

    [Fact]
    public async Task Listar_Cruzado_MostraOsDoisFiltrosNoTexto()
    {
        var repo = new FakeVendasRepo { Pagina = new(["NomeCliente", "Actividade"], [["Ana", "Oficina Independente"]], 1) };

        var resultado = await CriarSut(repo).ConsultarAsync(
            Campo.Actividade, "Oficina Independente", cruzarCom: Campo.Distrito, valorCruzado: "Setúbal");

        Assert.Equal(
            "Clientes com Actividade = Oficina Independente e Distrito = Setúbal: 1 clientes.\n" +
            "NomeCliente,Actividade\nAna,Oficina Independente\n# Lista completa: 1 clientes.\n", resultado);
    }

    [Fact]
    public async Task Contar_Cruzado_MostraOFiltroCruzadoNoTitulo()
    {
        var repo = new FakeVendasRepo { Contagens = [new("Norte", 4, 4, 1)] };

        var resultado = await CriarSut(repo).ConsultarAsync(
            Campo.Zona, contar: true, cruzarCom: Campo.Pagamento, valorCruzado: "30 dias");

        Assert.StartsWith("Clientes por Zona, com Pagamento = 30 dias: 4 clientes em 1 grupos.", resultado);
    }

    [Fact]
    public async Task CruzarComAMesmaColuna_Recusa()
    {
        var repo = new FakeVendasRepo();

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Zona, contar: true, cruzarCom: Campo.Zona, valorCruzado: "Norte");

        Assert.Equal("Escolha uma coluna diferente para cruzar.", resultado);
        Assert.Null(repo.UltimoAgrupar);
    }

    [Theory]
    [InlineData(Campo.Pagamento, null)]
    [InlineData(null, "30 dias")]
    public async Task CruzarComSemValorOuValorSemCruzarCom_Recusa(Campo? cruzarCom, string? valorCruzado)
    {
        var repo = new FakeVendasRepo();

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Zona, contar: true, cruzarCom: cruzarCom, valorCruzado: valorCruzado);

        Assert.Equal("cruzarCom e valorCruzado têm de ser usados juntos.", resultado);
        Assert.Null(repo.UltimoAgrupar);
    }

    // ---------- contar ----------

    [Fact]
    public async Task Contar_SemColuna_PedeColuna()
    {
        var repo = new FakeVendasRepo();

        var resultado = await CriarSut(repo).ConsultarAsync(contar: true);

        Assert.Equal("Indique uma coluna para contar ou filtrar.", resultado);
        Assert.Null(repo.UltimoAgrupar);
    }

    [Fact]
    public async Task Contar_ComValor_ContaOsClientesComEsseValor()
    {
        var repo = new FakeVendasRepo { Contagens = [new("60 dias", 7, 7, 1)] };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Pagamento, "60 dias", contar: true);

        Assert.Equal(Campo.Pagamento, repo.UltimoAgrupar);
        Assert.Equal("60 dias", repo.UltimosFiltros![Campo.Pagamento]);
        Assert.Contains("7 clientes", resultado);
    }

    [Fact]
    public async Task Contar_ComValorSemResultados_DevolveOsValoresExistentes()
    {
        var repo = new FakeVendasRepo
        {
            ContagensPor = SoValoresSemFiltro([new("30 Dias", 12, 19, 2), new("60 Dias", 7, 19, 2)]),
        };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Pagamento, "xyz", contar: true);

        Assert.Equal(
            "Nenhuma correspondência. O valor tem de ser um dos existentes (entre parênteses, nº de clientes):\n" +
            "Pagamento: 30 Dias (12); 60 Dias (7)\n", resultado);
        var (agrupar, filtros, limite) = repo.ChamadasContar[^1]; // 2.ª chamada: valores existentes
        Assert.Equal(Campo.Pagamento, agrupar);
        Assert.Empty(filtros);
        Assert.Equal(30, limite);
    }

    [Fact]
    public async Task Listar_ComValorSemResultados_DevolveOsValoresExistentes()
    {
        var repo = new FakeVendasRepo
        {
            ContagensPor = SoValoresSemFiltro([new("Lisboa", 40, 55, 2), new("Porto", 15, 55, 2)]),
        };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Distrito, "Xyz");

        Assert.Contains("Nenhuma correspondência", resultado);
        Assert.Contains("Distrito: Lisboa (40); Porto (15)", resultado);
    }

    // Dois valores que existem mas não têm clientes em comum não são "valor inexistente".
    [Fact]
    public async Task SemCorrespondencia_DoisFiltros_NaoDizQueOValorNaoExiste()
    {
        var zonas = new List<ContagemCliente> { new("Norte", 120, 300, 3) };
        var pagamentos = new List<ContagemCliente> { new("90 Dias", 15, 300, 4) };
        var repo = new FakeVendasRepo
        {
            ContagensPor = (campo, filtros) => filtros.Count > 0 ? [] : campo == Campo.Zona ? zonas : pagamentos,
        };

        var resultado = await CriarSut(repo).ConsultarAsync(
            Campo.Zona, "Norte", cruzarCom: Campo.Pagamento, valorCruzado: "90 dias");

        Assert.Contains("combinação sem clientes", resultado);
        Assert.Contains("a resposta é 0 clientes", resultado);
        Assert.DoesNotContain("tem de ser um dos existentes", resultado);
        Assert.Contains("Zona: Norte (120)", resultado);
        Assert.Contains("Pagamento: 90 Dias (15)", resultado);
    }

    [Fact]
    public async Task SemCorrespondencia_MaisValoresQueOMaximo_AvisaQueAListaEstaCortada()
    {
        var existentes = Enumerable.Range(1, 30).Select(i => new ContagemCliente($"L{i}", 1, 45, 45)).ToList();
        var repo = new FakeVendasRepo { ContagensPor = SoValoresSemFiltro(existentes) };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Localidade, "xyz");

        Assert.Contains("(mostrados 30 de 45)", resultado);
    }

    // Uma página além do fim não é "valor inexistente": não vai buscar valores.
    [Fact]
    public async Task PaginaAlemDoFim_NaoDizSemCorrespondencia()
    {
        var repo = new FakeVendasRepo();

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Distrito, "Lisboa", pagina: 2);

        Assert.Equal("Nenhuma linha encontrada com esse filtro.", resultado);
        Assert.Empty(repo.ChamadasContar);
    }

    [Fact]
    public async Task SemCorrespondencia_FalhaNaConsultaDosValores_DevolveMensagemAmigavel()
    {
        var repo = new FakeVendasRepo
        {
            ContagensPor = (_, filtros) => filtros.Count == 0 ? throw new InvalidOperationException("falha de bd") : [],
        };

        Assert.Equal(ErroNeutro, await CriarSut(repo).ConsultarAsync(Campo.Pagamento, "xyz", contar: true));
    }

    // Com filtros não há resultados; sem filtros (a consulta dos valores existentes) devolve os valores dados.
    private static Func<Campo, IReadOnlyDictionary<Campo, string>, IReadOnlyList<ContagemCliente>> SoValoresSemFiltro(
        IReadOnlyList<ContagemCliente> valores) => (_, filtros) => filtros.Count == 0 ? valores : [];

    [Fact]
    public async Task Contar_SemDados_DevolveMensagemDeVazio()
    {
        Assert.Equal("Sem dados para esta coluna.",
            await CriarSut(new FakeVendasRepo()).ConsultarAsync(Campo.Distrito, contar: true));
    }

    [Fact]
    public async Task Contar_DevolveCsvComTotaisEPercentagens()
    {
        var repo = new FakeVendasRepo
        {
            Contagens = [new("Norte", 10, 30, 3), new("Sul", 15, 30, 3), new("Centro", 5, 30, 3)],
        };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Zona, contar: true, limite: 20);

        Assert.Equal(NL(
            "Clientes por Zona: 30 clientes em 3 grupos.",
            "Zona,clientes,percentagem",
            "Norte,10,33.3",
            "Sul,15,50.0",
            "Centro,5,16.7"), resultado);
    }

    [Fact]
    public async Task Contar_ValorComVirgula_EEscapadoEntreAspas()
    {
        var repo = new FakeVendasRepo { Contagens = [new("Porto, Norte", 5, 5, 1)] };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Zona, contar: true);

        Assert.Contains("\"Porto, Norte\",5,100.0", resultado);
    }

    [Fact]
    public async Task Contar_MaisGruposQueLinhas_AvisaQueFoiCortado()
    {
        var repo = new FakeVendasRepo { Contagens = [new("Norte", 10, 30, 3), new("Sul", 15, 30, 3)] };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Zona, contar: true, limite: 2);

        Assert.EndsWith(NL("# Mostrados 2 de 3 grupos. Aumente o limite para ver os restantes."), resultado);
    }

    // ---------- limite, erros ----------

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-7, 1)]
    [InlineData(9999, 100)]
    public async Task Limite_ForaDosLimites_EClampeado(int pedido, int esperado)
    {
        var repo = new FakeVendasRepo();

        await CriarSut(repo).ConsultarAsync(limite: pedido);
        Assert.Equal(esperado, repo.UltimoLimite);

        await CriarSut(repo).ConsultarAsync(Campo.Pagamento, contar: true, limite: pedido);
        Assert.Equal(esperado, repo.UltimoLimite);
    }

    [Fact]
    public async Task QuandoRepositorioFalha_DevolveMensagemAmigavel()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new InvalidOperationException("falha de bd") };

        Assert.Equal(ErroNeutro, await CriarSut(repo).ConsultarAsync());
        Assert.Equal(ErroNeutro, await CriarSut(repo).ConsultarAsync(Campo.Pagamento, contar: true));
    }

    [Fact]
    public async Task QuandoCancelado_PropagaAExcecao()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(() => CriarSut(repo).ConsultarAsync());
    }
}

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

        Assert.Equal("NomeCliente,Distrito\nAna,Lisboa\n\"Bento, Filhos\",Porto\n", resultado);
        var filtro = Assert.Single(repo.UltimosFiltros!);
        Assert.Equal(Campo.Distrito, filtro.Key);
        Assert.Equal("Lisboa", filtro.Value);
    }

    [Fact]
    public async Task SoColuna_ChamaORepositorioSemFiltros()
    {
        var repo = new FakeVendasRepo { Pagina = new(["NomeCliente", "Zona"], [["Ana", "Norte"]], 1) };

        var resultado = await CriarSut(repo).ConsultarAsync(Campo.Zona);

        Assert.Equal("NomeCliente,Zona\nAna,Norte\n", resultado);
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
        var repo = new FakeVendasRepo();

        await CriarSut(repo).ConsultarAsync(Campo.Zona, "Norte", cruzarCom: Campo.Pagamento, valorCruzado: "30 dias");

        Assert.Equal(new Dictionary<Campo, string> { [Campo.Zona] = "Norte", [Campo.Pagamento] = "30 dias" },
            repo.UltimosFiltros);
    }

    [Fact]
    public async Task Contar_CruzadoComOutraView_AgrupaPelaColunaEFiltraPelaCruzada()
    {
        var repo = new FakeVendasRepo();

        await CriarSut(repo).ConsultarAsync(Campo.Zona, contar: true, cruzarCom: Campo.Pagamento, valorCruzado: "30 dias");

        Assert.Equal(Campo.Zona, repo.UltimoAgrupar);
        var filtro = Assert.Single(repo.UltimosFiltros!);
        Assert.Equal(Campo.Pagamento, filtro.Key);
        Assert.Equal("30 dias", filtro.Value);
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

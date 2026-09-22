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

    // ---------- cada tool lê as suas colunas ----------

    [Fact]
    public async Task ClientesAsync_MostraAsColunasDeCliente()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ClientesAsync();
        Assert.Equal(
            [Campo.Zona, Campo.Vendedor, Campo.TipoCliente, Campo.Actividade, Campo.Distrito],
            repo.UltimoMostrar);
    }

    [Fact]
    public async Task FaturacaoAsync_MostraAsColunasDeFaturacao()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).FaturacaoAsync();
        Assert.Equal(
            [Campo.Pagamento, Campo.Cobranca, Campo.Expedicao, Campo.SituacaoFinanceira,
             Campo.EscalaoPlafond, Campo.EscalaoVolumeVendas],
            repo.UltimoMostrar);
    }

    // ---------- dados ----------

    [Fact]
    public async Task ClientesAsync_SemLinhas_DevolveMensagemDeVazio()
    {
        Assert.Equal("Nenhuma linha encontrada com esse filtro.",
            await CriarSut(new FakeVendasRepo()).ClientesAsync());
    }

    [Fact]
    public async Task ClientesAsync_ComColunaEValor_DevolveCsvEPassaOFiltro()
    {
        var repo = new FakeVendasRepo
        {
            Pagina = new(["NomeCliente", "Distrito"], [["Ana", "Lisboa"], ["Bento, Filhos", "Porto"]], 2),
        };

        var resultado = await CriarSut(repo).ClientesAsync(ColunaCliente.Distrito, "Lisboa");

        Assert.Equal("NomeCliente,Distrito\nAna,Lisboa\n\"Bento, Filhos\",Porto\n", resultado);
        Assert.Equal("Lisboa", Assert.Single(repo.UltimosFiltros!)!.Value);
        Assert.Equal(Campo.Distrito, Assert.Single(repo.UltimosFiltros!).Key);
    }

    [Fact]
    public async Task ClientesAsync_SoColuna_ChamaORepositorioSemValor()
    {
        var repo = new FakeVendasRepo { Pagina = new(["NomeCliente", "Zona"], [["Ana", "Norte"]], 1) };

        var resultado = await CriarSut(repo).ClientesAsync(ColunaCliente.Zona);

        Assert.Equal("NomeCliente,Zona\nAna,Norte\n", resultado);
        Assert.Equal([Campo.Zona], repo.UltimoMostrar);
        Assert.Empty(repo.UltimosFiltros!);
    }

    [Fact]
    public async Task FaturacaoAsync_ValorSemColuna_PedeColuna()
    {
        var repo = new FakeVendasRepo();

        var resultado = await CriarSut(repo).FaturacaoAsync(valor: "x");

        Assert.Equal("Indique uma coluna para contar ou filtrar.", resultado);
        Assert.Null(repo.UltimoMostrar);
    }

    [Fact]
    public async Task ClientesAsync_TotalMaiorQueLinhas_AvisaQueFoiCortado()
    {
        var repo = new FakeVendasRepo { Pagina = new(["NomeCliente"], [["Ana"], ["Bruno"]], 3) };

        var resultado = await CriarSut(repo).ClientesAsync(limite: 2);

        Assert.EndsWith("#Mostrados 2 de 3.Filtre por valor ou aumente o limite.\n", resultado);
    }

    // ---------- contar ----------

    [Fact]
    public async Task ClientesAsync_Contar_SemColuna_PedeColuna()
    {
        var repo = new FakeVendasRepo();

        var resultado = await CriarSut(repo).ClientesAsync(contar: true);

        Assert.Equal("Indique uma coluna para contar ou filtrar.", resultado);
        Assert.Null(repo.UltimoAgrupar);
    }

    [Fact]
    public async Task ClientesAsync_Contar_SemDados_DevolveMensagemDeVazio()
    {
        Assert.Equal("Sem dados para esta coluna",
            await CriarSut(new FakeVendasRepo()).ClientesAsync(ColunaCliente.Distrito, contar: true));
    }

    [Fact]
    public async Task ClientesAsync_Contar_DevolveCsvComTotaisEPercentagens()
    {
        var repo = new FakeVendasRepo
        {
            Contagens = [new("Norte", 10, 30, 3), new("Sul", 15, 30, 3), new("Centro", 5, 30, 3)],
        };

        var resultado = await CriarSut(repo).ClientesAsync(ColunaCliente.Zona, contar: true, limite: 20);

        Assert.Equal(NL(
            "Clientes por Zona:30 clientes em 3 grupos.",
            "Zona, clientes, percentagem",
            "Norte, 10,33.3",
            "Sul, 15,50.0",
            "Centro, 5,16.7"), resultado);
    }

    [Fact]
    public async Task FaturacaoAsync_Contar_UsaAColunaPedida()
    {
        var repo = new FakeVendasRepo();

        await CriarSut(repo).FaturacaoAsync(ColunaFaturacao.Pagamento, contar: true);

        Assert.Equal(Campo.Pagamento, repo.UltimoAgrupar);
    }

    [Fact]
    public async Task ClientesAsync_Contar_ValorComVirgula_EEscapadoEntreAspas()
    {
        var repo = new FakeVendasRepo { Contagens = [new("Porto, Norte", 5, 5, 1)] };

        var resultado = await CriarSut(repo).ClientesAsync(ColunaCliente.Zona, contar: true);

        Assert.Contains("\"Porto, Norte\", 5,100.0", resultado);
    }

    [Fact]
    public async Task ClientesAsync_Contar_MaisGruposQueLinhas_AvisaQueFoiCortado()
    {
        var repo = new FakeVendasRepo { Contagens = [new("Norte", 10, 30, 3), new("Sul", 15, 30, 3)] };

        var resultado = await CriarSut(repo).ClientesAsync(ColunaCliente.Zona, contar: true, limite: 2);

        Assert.EndsWith(
            NL(" #mostrados 2 , 3 de grupos, aumente o limite para ver os restantes."), resultado);
    }

    // ---------- limite, erros ----------

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-7, 1)]
    [InlineData(9999, 100)]
    public async Task Limite_ForaDosLimites_EClampeado(int pedido, int esperado)
    {
        var repo = new FakeVendasRepo();

        await CriarSut(repo).ClientesAsync(limite: pedido);
        Assert.Equal(esperado, repo.UltimoLimite);

        await CriarSut(repo).FaturacaoAsync(ColunaFaturacao.Pagamento, contar: true, limite: pedido);
        Assert.Equal(esperado, repo.UltimoLimite);
    }

    [Fact]
    public async Task QuandoRepositorioFalha_DevolveMensagemAmigavel()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new InvalidOperationException("falha de bd") };

        Assert.Equal(ErroNeutro, await CriarSut(repo).ClientesAsync());
        Assert.Equal(ErroNeutro, await CriarSut(repo).FaturacaoAsync(ColunaFaturacao.Pagamento, contar: true));
    }

    [Fact]
    public async Task QuandoCancelado_PropagaAExcecao()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(() => CriarSut(repo).ClientesAsync());
    }
}

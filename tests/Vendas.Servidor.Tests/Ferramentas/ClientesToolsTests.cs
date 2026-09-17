using Microsoft.Extensions.Logging.Abstractions;
using Vendas.Servidor.Ferramentas;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Tests.Dados;

namespace Vendas.Servidor.Tests.Ferramentas;

public class ClientesToolsTests
{
    private static ClientesTools CriarSut(FakeVendasRepo repo) =>
        new(repo, NullLogger<ClientesTools>.Instance);

    // ---------- NomesAsync ----------

    [Fact]
    public async Task NomesAsync_SemClientes_DevolveMensagemDeVazio()
    {
        var repo = new FakeVendasRepo { Nomes = [] };
        var sut = CriarSut(repo);

        var resultado = await sut.NomesAsync();

        Assert.Equal("Sem clientes registados.", resultado);
    }

    [Fact]
    public async Task NomesAsync_PedeUmAMaisQueOLimiteAoRepositorio()
    {
        var repo = new FakeVendasRepo { Nomes = ["Ana"] };
        var sut = CriarSut(repo);

        await sut.NomesAsync(limite: 5);

        Assert.Equal(6, repo.UltimoLimiteNomes);
    }

    [Fact]
    public async Task NomesAsync_DentroDoLimite_DevolveTodosSemAviso()
    {
        var repo = new FakeVendasRepo { Nomes = ["Ana", "Bruno", "Carlos"] };
        var sut = CriarSut(repo);

        var resultado = await sut.NomesAsync(limite: 200);

        Assert.Equal("Ana\nBruno\nCarlos", resultado);
    }

    [Fact]
    public async Task NomesAsync_AcimaDoLimite_TruncaEAvisaQueHaMais()
    {
        var repo = new FakeVendasRepo { Nomes = ["Ana", "Bruno", "Carlos"] };
        var sut = CriarSut(repo);

        var resultado = await sut.NomesAsync(limite: 2);

        Assert.Equal("Ana\nBruno\n# mostrados os primeiros 2 nomes; existem mais.", resultado);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(-10, 2)]
    [InlineData(1000, 501)]
    public async Task NomesAsync_LimiteForaDosLimites_EClampeadoAntesDeChamarORepositorio(
        int limitePedido, int limiteEsperadoNoRepo)
    {
        var repo = new FakeVendasRepo { Nomes = [] };
        var sut = CriarSut(repo);

        await sut.NomesAsync(limite: limitePedido);

        Assert.Equal(limiteEsperadoNoRepo, repo.UltimoLimiteNomes);
    }

    [Fact]
    public async Task NomesAsync_QuandoRepositorioFalha_DevolveMensagemAmigavel()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new InvalidOperationException("falha de bd") };
        var sut = CriarSut(repo);

        var resultado = await sut.NomesAsync();

        Assert.Equal("Não foi possível obter os clientes neste momento.", resultado);
    }

    [Fact]
    public async Task NomesAsync_QuandoCancelado_PropagaAExcecao()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new OperationCanceledException() };
        var sut = CriarSut(repo);

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.NomesAsync());
    }

    // ---------- ClientesPorAsync ----------

    [Fact]
    public async Task ClientesPorAsync_SemDados_DevolveMensagemComNomeDaDimensao()
    {
        var repo = new FakeVendasRepo { Contagens = [] };
        var sut = CriarSut(repo);

        var resultado = await sut.ClientesPorAsync(DimensaoCliente.Distrito);

        Assert.Equal("Não há clientes com Distrito preenchido.", resultado);
    }

    [Fact]
    public async Task ClientesPorAsync_ComDados_DevolveCsvComTotaisEPercentagens()
    {
        var repo = new FakeVendasRepo
        {
            Contagens =
            [
                new ContagemCliente("Norte", 10, 30, 3),
                new ContagemCliente("Sul", 15, 30, 3),
                new ContagemCliente("Centro", 5, 30, 3),
            ],
        };
        var sut = CriarSut(repo);

        var resultado = await sut.ClientesPorAsync(DimensaoCliente.Zona, limite: 20);

        var esperado =
            $"Clientes por zona: 30 clientes em 3 grupos.{Environment.NewLine}" +
            $"zona,clientes,%{Environment.NewLine}" +
            $"Norte,10,33.3{Environment.NewLine}" +
            $"Sul,15,50.0{Environment.NewLine}" +
            $"Centro,5,16.7{Environment.NewLine}";
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public async Task ClientesPorAsync_ValorComVirgula_EEscapadoEntreAspas()
    {
        var repo = new FakeVendasRepo
        {
            Contagens = [new ContagemCliente("Porto, Norte", 5, 5, 1)],
        };
        var sut = CriarSut(repo);

        var resultado = await sut.ClientesPorAsync(DimensaoCliente.Zona);

        Assert.Contains("\"Porto, Norte\",5,100.0", resultado);
    }

    [Fact]
    public async Task ClientesPorAsync_MaisLinhasQueOLimite_TruncaEAvisa()
    {
        var repo = new FakeVendasRepo
        {
            Contagens =
            [
                new ContagemCliente("Norte", 10, 30, 3),
                new ContagemCliente("Sul", 15, 30, 3),
                new ContagemCliente("Centro", 5, 30, 3),
            ],
        };
        var sut = CriarSut(repo);

        var resultado = await sut.ClientesPorAsync(DimensaoCliente.Zona, limite: 2);

        var esperado =
            $"Clientes por zona: 30 clientes em 3 grupos.{Environment.NewLine}" +
            $"zona,clientes,%{Environment.NewLine}" +
            $"Norte,10,33.3{Environment.NewLine}" +
            $"Sul,15,50.0{Environment.NewLine}" +
            $"# existem mais valores para além destes{Environment.NewLine}";
        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(100, 51)]
    public async Task ClientesPorAsync_LimiteForaDosLimites_EClampeadoAntesDeChamarORepositorio(
        int limitePedido, int limiteEsperadoNoRepo)
    {
        var repo = new FakeVendasRepo { Contagens = [] };
        var sut = CriarSut(repo);

        await sut.ClientesPorAsync(DimensaoCliente.Zona, limite: limitePedido);

        Assert.Equal(limiteEsperadoNoRepo, repo.UltimoLimiteContagem);
    }

    [Fact]
    public async Task ClientesPorAsync_QuandoRepositorioFalha_DevolveMensagemAmigavel()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new InvalidOperationException("falha de bd") };
        var sut = CriarSut(repo);

        var resultado = await sut.ClientesPorAsync(DimensaoCliente.Zona);

        Assert.Equal("Não foi possivel obter os dados de clientes neste momento", resultado);
    }

    [Fact]
    public async Task ClientesPorAsync_QuandoCancelado_PropagaAExcecao()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new OperationCanceledException() };
        var sut = CriarSut(repo);

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ClientesPorAsync(DimensaoCliente.Zona));
    }

    // ---------- FaturacaoCondicoesAsync ----------

    [Fact]
    public async Task FaturacaoCondicoesAsync_SemDados_DevolveMensagemDeVazio()
    {
        var repo = new FakeVendasRepo { Contagens = [] };
        var sut = CriarSut(repo);

        var resultado = await sut.FaturacaoCondicoesAsync(DimensaoFaturacao.Pagamento);

        Assert.Equal("Sem dados de condições de faturação.", resultado);
    }

    [Fact]
    public async Task FaturacaoCondicoesAsync_ComDados_DevolveCsvComTotaisEPercentagens()
    {
        var repo = new FakeVendasRepo
        {
            Contagens =
            [
                new ContagemCliente("Pronto", 20, 50, 2),
                new ContagemCliente("A 30 dias", 30, 50, 2),
            ],
        };
        var sut = CriarSut(repo);

        var resultado = await sut.FaturacaoCondicoesAsync(DimensaoFaturacao.Pagamento, limite: 20);

        var esperado =
            $"Clientes por condicao pagamento:50 clientes em 2 grupos.{Environment.NewLine}" +
            $"condicao_pagamento, clientes, % total{Environment.NewLine}" +
            $"Pronto, 20,40.0{Environment.NewLine}" +
            $"A 30 dias, 30,60.0{Environment.NewLine}";
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public async Task FaturacaoCondicoesAsync_MaisLinhasQueOLimite_TruncaEAvisa()
    {
        var repo = new FakeVendasRepo
        {
            Contagens =
            [
                new ContagemCliente("Pronto", 20, 50, 2),
                new ContagemCliente("A 30 dias", 30, 50, 2),
            ],
        };
        var sut = CriarSut(repo);

        var resultado = await sut.FaturacaoCondicoesAsync(DimensaoFaturacao.Pagamento, limite: 1);

        var esperado =
            $"Clientes por condicao pagamento:50 clientes em 2 grupos.{Environment.NewLine}" +
            $"condicao_pagamento, clientes, % total{Environment.NewLine}" +
            $"Pronto, 20,40.0{Environment.NewLine}" +
            $" #mostra 1 , 2 de grupos, aumente o limite para ver os restantes.{Environment.NewLine}";
        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(100, 51)]
    public async Task FaturacaoCondicoesAsync_LimiteForaDosLimites_EClampeadoAntesDeChamarORepositorio(
        int limitePedido, int limiteEsperadoNoRepo)
    {
        var repo = new FakeVendasRepo { Contagens = [] };
        var sut = CriarSut(repo);

        await sut.FaturacaoCondicoesAsync(DimensaoFaturacao.Pagamento, limite: limitePedido);

        Assert.Equal(limiteEsperadoNoRepo, repo.UltimoLimiteContagem);
    }

    [Fact]
    public async Task FaturacaoCondicoesAsync_QuandoRepositorioFalha_DevolveMensagemAmigavel()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new InvalidOperationException("falha de bd") };
        var sut = CriarSut(repo);

        var resultado = await sut.FaturacaoCondicoesAsync(DimensaoFaturacao.Pagamento);

        Assert.Equal("Não foi possivel obter as condicões de faturação neste momento.", resultado);
    }

    [Fact]
    public async Task FaturacaoCondicoesAsync_QuandoCancelado_PropagaAExcecao()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new OperationCanceledException() };
        var sut = CriarSut(repo);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.FaturacaoCondicoesAsync(DimensaoFaturacao.Pagamento));
    }
}

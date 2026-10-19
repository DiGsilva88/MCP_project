using Microsoft.Extensions.Logging.Abstractions;
using Vendas.Servidor.Ferramentas;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Tests.Dados;

namespace Vendas.Servidor.Tests.Ferramentas;

public class VendasToolsTests
{
    private static VendasTools CriarSut(FakeVendasRepo repo) =>
        new(repo, NullLogger<VendasTools>.Instance);

    [Fact]
    public async Task Vendas_PorArtigo_DevolveRankingComPercentagemEQuantidadeECorte()
    {
        var repo = new FakeVendasRepo
        {
            Valores = [new("Parafuso, 5mm", 750m, 30m, 4, 1000m, 3), new("Porca", 250m, 10.5m, 2, 1000m, 3)]
        };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Artigo, Periodo.AnoAtual, limite: 2);

        Assert.Equal(DimensaoVenda.Artigo, repo.UltimaDimensao);
        Assert.Equal(2, repo.UltimoLimite);
        var linhas = r.Split(Environment.NewLine);
        Assert.Equal("posicao,artigo,valor,percentagem,documentos,quantidade", linhas[1]);
        Assert.Equal("1,\"Parafuso, 5mm\",750.00,75.0,4,30", linhas[2]);
        Assert.Equal("2,Porca,250.00,25.0,2,10.5", linhas[3]);
        Assert.Equal("# Mostrados 2 de 3 artigos.", linhas[4]);
    }

    [Fact]
    public async Task Vendas_PorCliente_UsaTopClientesESemQuantidade()
    {
        var repo = new FakeVendasRepo { Valores = [new("Ana", 100m, 0m, 1, 100m, 1)] };
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Cliente, Periodo.AnoAtual);

        Assert.Null(repo.UltimaDimensao);
        Assert.Contains("posicao,cliente,valor,percentagem,documentos" + Environment.NewLine + "1,Ana,100.00,100.0,1", r);
    }

    [Fact]
    public async Task Vendas_ErroNoRepo_DevolveErroNeutro()
    {
        var repo = new FakeVendasRepo { LancarExcecao = new InvalidOperationException("SQL partido") };
        var r = await CriarSut(repo).ConsultarVendasAsync();

        Assert.Equal("Não foi possivel consultar os dados neste momento", r);
    }
}

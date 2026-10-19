using Microsoft.Extensions.Logging.Abstractions;
using Vendas.Servidor.Ferramentas;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Tests.Dados;

namespace Vendas.Servidor.Tests.Ferramentas;

public class VendasToolsTests
{


    private static VendasTools CriarSut(FakeVendasRepo repo) => new(repo, NullLogger<VendasTools>.Instance);

    [Fact]
    public async Task Cliente_ChamaTopClientes()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Cliente, limite: 5);

        Assert.Null(repo.UltimaDimensao); // não foi ao TopArtigos
        Assert.Equal(5, repo.UltimoLimite);
    }

    [Fact]
    public async Task Marca_ChamaTopArtigosComADimensao()
    {
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Marca);

        Assert.Equal(DimensaoVenda.Marca, repo.UltimaDimensao);
    }

    [Fact]
    public async Task Limite_AcimaDe100_FicaEm100()
    {
    
        var repo = new FakeVendasRepo();
        await CriarSut(repo).ConsultarVendasAsync(limite:500);
        Assert.Equal(100, repo.UltimoLimite);
    }


    [Fact]
    public async Task Falha_DevolveErroNeutro()
    {
    
        var repo = new FakeVendasRepo{LancarExcecao = new InvalidOperationException("SQL secreto")};
        var r = await CriarSut(repo).ConsultarVendasAsync();
        
        Assert.Equal(VistasTools.ErroNeutro, r);
    }

    [Fact]
    public async Task Csv_ProtegeVirgulasUsaPontoEAvisaCorte()
    {
    
        var repo = new FakeVendasRepo{Valores = [new("Bento, Filhos", 1234.5m,0,3,2469m,2, 1371.67m, -137.17m, 283.94m)]};
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Cliente);
        Assert.Contains("posicao,cliente,valor,percentagem,documentos,bruto,desconto,iva" + Environment.NewLine, r);
        Assert.Contains("1,\"Bento, Filhos\",1234.50,50.0,3,1371.67,-137.17,283.94",r);
        Assert.Contains("# Mostrados 1 de 2 clientes",r);

    }

    [Fact]
    public async Task Cliente_PassaAoRepoEApareceNoCabecalho()
    {
        var repo = new FakeVendasRepo{Valores = [new("2024-12", 30.6m,1,1,30.6m,1)]};
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, cliente: " 1003 ");

        Assert.Equal(" 1003 ", repo.UltimoCliente);
        Assert.Equal(DimensaoVenda.Mes, repo.UltimaDimensao);
        Assert.Contains("cliente \"1003\"", r);
        Assert.Contains("posicao,mes,valor,percentagem,documentos,quantidade,bruto,desconto,iva", r);
    }

    [Fact]
    public async Task TotalZero_NaoDividePorZero()
    {
    
        var repo = new FakeVendasRepo{Valores = [new("Ana", 0m,2m,1,0m,1)]};
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Artigo);
        
        Assert.Contains("1,Ana,0.00,-,1,2",r);
    }
    
    }


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
        Assert.Contains("posicao,cliente,valor,percentagem,documentos,media,bruto,desconto,iva" + Environment.NewLine, r);
        Assert.Contains("1,\"Bento, Filhos\",1234.50,50.0,3,411.50,1371.67,-137.17,283.94",r);
        Assert.Contains("# Mostrados 1 de 2 clientes",r);

    }

    [Fact]
    public async Task Cliente_PassaAoRepoEApareceNoCabecalho()
    {
        var repo = new FakeVendasRepo{Valores = [new("2024-12", 30.6m,1,1,30.6m,1)]};
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Mes, cliente: " 1003 ");

        Assert.Equal("1003", repo.UltimoCliente);
        Assert.Equal(DimensaoVenda.Mes, repo.UltimaDimensao);
        Assert.Contains("cliente \"Cliente 1003 (1003)\"", r);
        Assert.Contains("posicao,mes,valor,percentagem,documentos,media,quantidade,bruto,desconto,iva", r);
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
    
        var repo = new FakeVendasRepo{Valores = [new("Ana", 0m,2m,1,0m,1)]};
        var r = await CriarSut(repo).ConsultarVendasAsync(DimensaoVenda.Artigo);
        
        Assert.Contains("1,Ana,0.00,-,1,0.00,2",r);
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
        Assert.Contains("2 documentos, média 5.00 por documento", r);
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
    
    }


using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Tests.Dados;

// RepoMemoria gera os dados uma vez (campo static, seed fixa) a partir da data de hoje.
// Os testes não assumem valores exatos (dependem do dia em que correm), só as garantias
// que o algoritmo tem de cumprir sempre: distinção, ordenação e limites.
public class RepoMemoriaTests
{
    private static readonly string[] ClientesConhecidos =
    [
        "Padaria Central", "Supermercado Bom Preço", "Restaurante Saboroso", "Loja de Roupas Fashion",
        "Farmácia Saúde", "Cafeteria Aroma", "Mercado Verde", "Pizzaria Delicatium", "Livraria Cultura",
        "Academia Fitness"
    ];

    private static readonly string[] ProdutosConhecidos =
    [
        "Pão integral", "Leite Integral", "Arroz Branco", "Frango Congelado", "Café Torrado",
        "Macarrão Espaguete", "Queijo", "Chocolate", "Refrigerante", "Suco Natural"
    ];

    [Fact]
    public async Task ObterNomesClientesAsync_DevolveNomesDistintosOrdenadosDaListaConhecida()
    {
        var repo = new RepoMemoria();

        var nomes = await repo.ObterNomesClientesAsync(100);

        Assert.NotEmpty(nomes);
        Assert.True(nomes.Count <= 100);
        Assert.Equal(nomes.Count, nomes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(nomes, n => Assert.Contains(n, ClientesConhecidos));
        Assert.Equal(nomes, nomes.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList());
    }

    [Fact]
    public async Task ObterNomesClientesAsync_RespeitaOLimite()
    {
        var repo = new RepoMemoria();
        var todos = await repo.ObterNomesClientesAsync(100);

        var primeiros3 = await repo.ObterNomesClientesAsync(3);

        Assert.True(primeiros3.Count <= 3);
        Assert.Equal(todos.Take(primeiros3.Count), primeiros3);
    }

    [Fact]
    public async Task ObterInativosAsync_ComZeroDias_DevolveTodosOrdenadosPorUltimaCompra()
    {
        var repo = new RepoMemoria();

        var inativos = await repo.ObterInativosAsync(0);

        Assert.NotEmpty(inativos);
        Assert.All(inativos, c => Assert.Contains(c.Cliente, ClientesConhecidos));
        Assert.All(inativos, c => Assert.True(c.DiasSemComprar >= 0));
        Assert.Equal(inativos, inativos.OrderBy(c => c.UltimaCompra).ToList());
    }

    [Fact]
    public async Task ObterInativosAsync_ComLimiarAcimaDoHistorico_DevolveVazio()
    {
        // O histórico gerado cobre apenas os últimos 24 meses (~730 dias);
        // um limiar muito maior nunca pode devolver clientes.
        var repo = new RepoMemoria();

        var inativos = await repo.ObterInativosAsync(3650);

        Assert.Empty(inativos);
    }

    [Fact]
    public async Task ObterTopClientesAsync_DevolveOrdenadoDescendentePorTotalVendidoELimitado()
    {
        var repo = new RepoMemoria();

        var top = await repo.ObterTopClientesAsync(3, 365);

        Assert.True(top.Count <= 3);
        Assert.All(top, v => Assert.Contains(v.Cliente, ClientesConhecidos));
        Assert.All(top, v => Assert.True(v.NumeroVendas > 0 && v.TotalVendido > 0));
        Assert.Equal(top, top.OrderByDescending(v => v.TotalVendido).ToList());
    }

    [Fact]
    public async Task ObterTopProdutosAsync_DevolveOrdenadoDescendentePorTotalVendidoELimitado()
    {
        var repo = new RepoMemoria();

        var top = await repo.ObterTopProdutosAsync(3, 365);

        Assert.True(top.Count <= 3);
        Assert.All(top, v => Assert.Contains(v.Produto, ProdutosConhecidos));
        Assert.All(top, v => Assert.True(v.NumeroVendas > 0 && v.TotalVendido > 0));
        Assert.Equal(top, top.OrderByDescending(v => v.TotalVendido).ToList());
    }

    [Theory]
    [InlineData(DimensaoCliente.Zona)]
    [InlineData(DimensaoCliente.Vendendor)]
    [InlineData(DimensaoCliente.Distrito)]
    public async Task ContarClientesAsync_DevolveSempreListaVazia(DimensaoCliente dimensao)
    {
        var repo = new RepoMemoria();

        var resultado = await repo.ContarClientesAsync(dimensao, 20);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ContarFaturacaoAsync_LancaNotImplementedException()
    {
        var repo = new RepoMemoria();

        var ex = await Assert.ThrowsAsync<NotImplementedException>(
            () => repo.ContarFaturacaoAsync(DimensaoFaturacao.Pagamento, 20));

        Assert.Equal("Condicoes de faturacao só existe no SQL server.", ex.Message);
    }
}

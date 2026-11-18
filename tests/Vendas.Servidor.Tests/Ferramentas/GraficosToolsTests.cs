using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using Vendas.Servidor.Ferramentas;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Tests.Dados;

namespace Vendas.Servidor.Tests.Ferramentas;

public class GraficosToolsTests
{
    private static GraficosTools CriarSut(FakeVendasRepo repo) => new(repo, NullLogger<GraficosTools>.Instance);

    private static ValorAgrupado Mes(string nome, decimal valor) => new(nome, valor, 0, 1, valor, 2);

    [Fact]
    public async Task GraficoVendasMes_ComVendas_DevolveImagemPngETexto()
    {
        var repo = new FakeVendasRepo { Valores = [Mes("2026-01", 100m), Mes("2026-02", 250m)] };

        var r = await CriarSut(repo).GraficoVendasMesAsync();

        var imagem = Assert.Single(r.Content.OfType<ImageContentBlock>());
        Assert.Equal("image/png", imagem.MimeType);
        Assert.Equal(0x89, imagem.DecodedData.Span[0]);
        Assert.Single(r.Content.OfType<TextContentBlock>());
        Assert.Equal(DimensaoVenda.Mes, repo.UltimaDimensao);
    }

    [Fact]
    public async Task GraficoVendasMes_SemVendas_DevolveSoTexto()
    {
        var r = await CriarSut(new FakeVendasRepo()).GraficoVendasMesAsync();

        Assert.Empty(r.Content.OfType<ImageContentBlock>());
        Assert.Contains("Sem vendas", Assert.Single(r.Content.OfType<TextContentBlock>()).Text);
    }

    [Fact]
    public async Task GraficoVendasMes_VendedorDoPerfil_ViraFiltroDeVendedor()
    {
        var repo = new FakeVendasRepo { Valores = [Mes("2026-01", 1m)] };

        await CriarSut(repo).GraficoVendasMesAsync(vendedor: "Ana");

        Assert.Equal("Ana", repo.UltimosFiltros![Campo.Vendedor]);
    }
}

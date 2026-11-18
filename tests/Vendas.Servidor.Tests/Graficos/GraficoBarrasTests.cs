using Vendas.Servidor.Graficos;

namespace Vendas.Servidor.Tests.Graficos;

public class GraficoBarrasTests
{
    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public void Png_ComValores_DevolveImagemPng()
    {
        var png = GraficoBarras.Png("Vendas por mês", ["Jan", "Fev", "Mar"], [100, 250.5, 80]);

        Assert.True(png.Take(4).SequenceEqual(AssinaturaPng));
    }

    [Fact]
    public void Png_SemValores_Falha()
    {
        Assert.Throws<ArgumentException>(() => GraficoBarras.Png("Vazio", [], []));
    }

    [Fact]
    public void Png_RotulosEValoresDeTamanhosDiferentes_Falha()
    {
        Assert.Throws<ArgumentException>(() => GraficoBarras.Png("x", ["Jan"], [1, 2]));
    }
}

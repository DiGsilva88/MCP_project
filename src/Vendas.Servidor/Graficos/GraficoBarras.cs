using ScottPlot;

namespace Vendas.Servidor.Graficos;

// Gráfico de barras em PNG, gerado no servidor para o chat.
public static class GraficoBarras
{
    public static byte[] Png(string titulo, IReadOnlyList<string> rotulos, IReadOnlyList<double> valores)
    {
        if (valores.Count == 0) throw new ArgumentException("Sem valores para desenhar.", nameof(valores));
        if (rotulos.Count != valores.Count) throw new ArgumentException("Rótulos e valores têm tamanhos diferentes.", nameof(rotulos));

        var grafico = new Plot();
        grafico.Add.Bars(valores.ToArray());
        grafico.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            rotulos.Select((r, i) => new Tick(i, r)).ToArray());
        grafico.Title(titulo);
        grafico.Axes.Margins(bottom: 0);
        return grafico.GetImageBytes(800, 400, ImageFormat.Png);
    }
}

using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Vendas.Servidor.Graficos;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Ferramentas;

// Gráficos em PNG para o chat. O agente entrega a imagem à página sem a mostrar ao modelo.
[McpServerToolType]
public sealed class GraficosTools(IVendasRepo repo, ILogger<GraficosTools> log, TimeProvider? fonteTempo = null)
{
    private readonly TimeProvider tempo = fonteTempo ?? TimeProvider.System;

    [McpServerTool(Name = "grafico_vendas_mes")]
    [Description("Gráfico de barras com a faturação de cada mês (valor líquido sem IVA). Para pedidos de gráfico da evolução das vendas por mês.")]
    public async Task<CallToolResult> GraficoVendasMesAsync(
        [Description("Ultimos12Meses, AnoAtual ou AnoAnterior.")] Periodo periodo = Periodo.AnoAtual,
        [Description("Opcional: ano com 4 dígitos.")] int? ano = null,
        [Description("Opcional: ID do cliente ou início do nome.")] string? cliente = null,
        [Description("Só do painel, não usar.")] string? vendedor = null,
        [Description("Só do painel, não usar.")] string[]? zonas = null,
        [Description("Só do painel, não usar.")] string[]? marcas = null,
        [Description("Só do painel, não usar.")] string[]? tiposDoc = null,
        CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(tempo.GetLocalNow().DateTime);
        var (intervalo, erroPeriodo) = Periodos.Resolver(periodo, null, ano, null, null, hoje, null);
        if (intervalo is not { } janela) return Texto(erroPeriodo!);

        var (filtros, erroFiltros) = FiltrosVenda.Criar(zonas, marcas, tiposDoc);
        if (filtros is null) return Texto(erroFiltros!);
        if (!string.IsNullOrWhiteSpace(vendedor)) filtros[Campo.Vendedor] = vendedor.Trim();

        byte[]? png = null;
        var texto = await Respostas.ExecutarAsync(log, "grafico_vendas_mes", periodo, async () =>
        {
            var (id, rotulo, erro) = await Respostas.ResolverClienteAsync(repo, cliente, filtros, ct);
            if (erro is not null) return erro;

            var meses = await repo.RankingPorDimensaoAsync(DimensaoVenda.Mes, janela.Desde, janela.Ate, Respostas.Maxlinhas, id, filtros, cancellationToken: ct);
            if (meses.Count == 0) return "Sem vendas no período.";

            var titulo = rotulo is null ? "Vendas por mês" : $"Vendas por mês - {rotulo}";
            png = GraficoBarras.Png(titulo, meses.Select(m => m.Nome).ToList(), meses.Select(m => (double)m.Valor).ToList());
            return $"{titulo}: {meses.Count} meses, total {meses.Sum(m => m.Valor):N2} euros.";
        });

        return png is null ? Texto(texto) : new CallToolResult
        {
            Content = [ImageContentBlock.FromBytes(png, "image/png"), new TextContentBlock { Text = texto }],
        };
    }

    private static CallToolResult Texto(string texto) => new() { Content = [new TextContentBlock { Text = texto }] };
}

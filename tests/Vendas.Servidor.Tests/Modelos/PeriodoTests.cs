using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Tests.Modelos;

public class PeriodoTests
{

    //ate é exclusivo: o dia anterior ao ultimo incluido

    [Theory]
    [InlineData(3, 2026, "2026-03-01", "2026-04-01")]
    [InlineData(null, 2025, "2025-01-01", "2026-01-01")]
    [InlineData(9, null, "2026-09-01", "2026-10-01")] // já passou
    [InlineData(11, null, "2025-11-01", "2025-12-01")] //ainda não chegou ano anterior

    public void IntervaloMes_DevolveDesdeEAte(int? mes, int? ano, string desde, string ate)
    {
        var (d, a, _) = Periodos.IntervaloMes(mes, ano, new DateOnly(2026, 10, 15));
        Assert.Equal((DateOnly.Parse(desde), DateOnly.Parse(ate)), (d, a));
    }

    [Theory]
    [InlineData(Periodo.MesAnterior, "2026-01-15", "2025-12-01", "2026-01-01")] //muda o ano
    [InlineData(Periodo.AnoAnterior, "2026-10-15", "2025-01-01", "2026-01-01")]
    [InlineData(Periodo.AnoAtual, "2026-10-15", "2026-01-01", "2026-10-16")]
    [InlineData(Periodo.Ultimos7Dias, "2026-10-15", "2026-10-09", "2026-10-16")]
    public void Intervalo_DevolveDesdeEAte(Periodo periodo, string hoje, string desde, string ate)
        => Assert.Equal((DateOnly.Parse(desde), DateOnly.Parse(ate)), Periodos.Intervalo(periodo, DateOnly.Parse(hoje)));

    private static readonly DateOnly Hoje = new(2026, 10, 15);

    // Prioridade: datas > mes/ano > periodo
    [Fact]
    public void Resolver_DatasGanhamAMesEAno_EOFimFicaIncluido()
    {
        var (intervalo, erro) = Periodos.Resolver(Periodo.AnoAtual, 3, 2025, "01-08-2026", "31-08-2026", Hoje);

        Assert.Null(erro);
        Assert.Equal(new IntervaloVendas(new(2026, 8, 1), new(2026, 9, 1), "período indicado"), intervalo);
    }
    [Fact]
    public void Resolver_SoPeriodo_UsaADescricaoDoPeriodo()
    {
        var (intervalo, _) = Periodos.Resolver(Periodo.MesAnterior, null, null, null, null, Hoje);

        Assert.Equal(new IntervaloVendas(new(2026, 9, 1), new(2026, 10, 1), "mês anterior"), intervalo);
    }

    [Theory]
    [InlineData(13, null, null, null, "mes tem de ser de 1 a 12.")]
    [InlineData(null, 1999, null, null, "ano tem de ser de 2000 a 2026.")]
    [InlineData(null, 2027, null, null, "ano tem de ser de 2000 a 2026.")]
    [InlineData(null, null, "01-08-2026", null, "dataInicio e dataFim têm de ser usados juntos.")]
    [InlineData(null, null, "2026-08-01", "31-08-2026", "Datas no formato dd-MM-aaaa, ex.: 01-08-2026.")]
    [InlineData(null, null, "31-08-2026", "01-08-2026", "dataFim tem de ser igual ou depois de dataInicio.")]
    public void Resolver_PedidoInvalido_DevolveUmaLinhaDeErro(int? mes, int? ano, string? inicio, string? fim, string esperado)
    {
        var (intervalo, erro) = Periodos.Resolver(Periodo.AnoAtual, mes, ano, inicio, fim, Hoje);

        Assert.Null(intervalo);
        Assert.Equal(esperado, erro);
    }

}
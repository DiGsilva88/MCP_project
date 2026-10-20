using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Tests.Modelos;

public class PeriodoTests
{
    
    //ate é exclusivo: o dia anterior ao ultimo incluido

    [Theory]
    [InlineData(3,    2026, "2026-03-01", "2026-04-01")]
    [InlineData(null, 2025, "2025-01-01", "2026-01-01")]
    [InlineData(9,    null, "2026-09-01", "2026-10-01")] // já passou
    [InlineData(11,   null, "2025-11-01", "2025-12-01")] //ainda não chegou ano anterior

    public void IntervaloMes_DevolveDesdeEAte(int? mes, int? ano, string desde, string ate)
    {
        var (d, a, _) = Periodos.IntervaloMes(mes, ano, new DateOnly(2026, 10, 15));
        Assert.Equal((DateOnly.Parse(desde), DateOnly.Parse(ate)), (d, a));
    }

    [Theory]    
    [InlineData(Periodo.MesAnterior, "2026-01-15", "2025-12-01", "2026-01-01")] //muda o ano
    [InlineData(Periodo.AnoAnterior, "2026-10-15", "2025-01-01", "2026-01-01")]
    [InlineData(Periodo.AnoAtual,    "2026-10-15", "2026-01-01", "2026-10-16")]
    [InlineData(Periodo.Ultimos7Dias,"2026-10-15", "2026-10-09", "2026-10-16")]
    public void Intervalo_DevolveDesdeEAte(Periodo periodo, string hoje, string desde,string ate)
        => Assert.Equal((DateOnly.Parse(desde), DateOnly.Parse(ate)), Periodos.Intervalo(periodo, DateOnly.Parse(hoje)));
}
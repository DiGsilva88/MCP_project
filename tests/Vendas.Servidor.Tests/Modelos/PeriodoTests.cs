using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Tests.Modelos;

public class PeriodoTests
{
    
    //ate é exclusivo: o dia anterior ao ultimo incluido

    [Theory]
    [InlineData(Periodo.MesAnterior, "2026-01-15", "2025-12-01", "2026-01-01")] //muda o ano
    [InlineData(Periodo.AnoAnterior, "2026-10-15", "2025-01-01", "2026-01-01")]
    [InlineData(Periodo.AnoAtual,    "2026-10-15", "2026-01-01", "2026-10-16")]
    [InlineData(Periodo.Ultimos7Dias,"2026-10-15", "2026-10-09", "2026-10-16")]
    public void Intervalo_DevolveDesdeEAte(Periodo periodo, string hoje, string desde,string ate)
        => Assert.Equal((DateOnly.Parse(desde), DateOnly.Parse(ate)), Periodos.Intervalo(periodo, DateOnly.Parse(hoje)));
}
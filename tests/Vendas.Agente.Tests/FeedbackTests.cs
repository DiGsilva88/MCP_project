namespace Vendas.Agente.Tests;

public class FeedbackTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 4, 9, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("""{"avaliacao":"sim","motivo":""}""", "2026-11-04 09:30\tsim\t")]
    [InlineData("""{"avaliacao":"sim"}""", "2026-11-04 09:30\tsim\t")]
    [InlineData("""{"avaliacao":"nao","motivo":"Números errados"}""", "2026-11-04 09:30\tnao\tNúmeros errados")]
    public void Linha_AceitaOQueAPaginaEnvia(string corpo, string esperado)
        => Assert.Equal(esperado, Feedback.Linha(corpo, Agora));

    [Theory]
    [InlineData("""{"avaliacao":"nao","motivo":"texto livre com o nome de um cliente"}""")]
    [InlineData("""{"avaliacao":"nao"}""")]                       // "Não" sem motivo
    [InlineData("""{"avaliacao":"sim","motivo":"Difícil de ler"}""")]
    [InlineData("""{"avaliacao":"talvez"}""")]
    [InlineData("""{"avaliacao":1}""")]
    [InlineData("""{}""")]
    [InlineData("""[]""")]
    [InlineData("não é json")]
    public void Linha_RecusaOResto(string corpo) => Assert.Null(Feedback.Linha(corpo, Agora));
}

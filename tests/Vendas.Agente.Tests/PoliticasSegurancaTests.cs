using System.Text.Json;

namespace Vendas.Agente.Tests;

public class PoliticasSegurancaTests
{
    [Theory]
    [InlineData("Há 42 clientes em Lisboa.")]
    [InlineData("Clientes:\n- Ana\n- Rui")]
    public void RespostaSemFonte_DadosSemChamadas_EInventada(string texto)
    {
        Assert.True(PoliticasSeguranca.RespostaSemFonte(texto, 0));
    }

    [Fact]
    public void RespostaSemFonte_ComChamadaComDados_Passa()
    {
        Assert.False(PoliticasSeguranca.RespostaSemFonte("Há 42 clientes em Lisboa.", 1));
    }

    [Fact]
    public void RespostaSemFonte_CumprimentoSemNumeros_Passa()
    {
        Assert.False(PoliticasSeguranca.RespostaSemFonte("Olá! Em que posso ajudar?", 0));
    }

    // Frases copiadas de VistasTools: se o servidor as mudar, estes testes avisam.
    [Theory]
    [InlineData("Nome,Localidade\nAna,Lisboa\n")]
    [InlineData("0 clientes com Localidade = Lisboa e Distrito = Porto: os valores existem, mas nenhum cliente tem os dois.")]
    [InlineData("Nenhuma linha encontrada com esse filtro.")]
    public void ResultadoComDados_RespostasReais_ContamComoFonte(string texto)
    {
        Assert.True(PoliticasSeguranca.ResultadoComDados(texto));
    }

    [Theory]
    [InlineData("Não foi possivel consultar os dados neste momento")]
    [InlineData("coluna e valor têm de ser usados juntos.")]
    [InlineData("Não há mais páginas: o máximo são 5. Filtre por um valor para ver outros clientes.")]
    [InlineData("")]
    public void ResultadoComDados_ErrosERecusas_NaoContam(string texto)
    {
        Assert.False(PoliticasSeguranca.ResultadoComDados(texto));
    }

    [Fact]
    public void TextoDoResultado_CallToolResult_JuntaOsTextos()
    {
        var resultado = JsonSerializer.Deserialize<JsonElement>(
            """{"content":[{"type":"text","text":"a\n"},{"type":"text","text":"b"}],"isError":false}""");

        Assert.Equal("a\nb", PoliticasSeguranca.TextoDoResultado(resultado));
    }

    [Fact]
    public void TextoDoResultado_SemContent_UsaToString()
    {
        Assert.Equal("texto simples", PoliticasSeguranca.TextoDoResultado("texto simples"));
        Assert.Equal("", PoliticasSeguranca.TextoDoResultado(null));
    }
}

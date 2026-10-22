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

    [Fact]
    public void RespostaSemFonte_ForaDoAmbito_Passa() =>
        Assert.False(PoliticasSeguranca.RespostaSemFonte(PoliticasSeguranca.ForaDoAmbito,0));

    // Frases copiadas de VistasTools e VendasTools: se o servidor as mudar, estes testes avisam.
    [Theory]
    [InlineData("Nome,Localidade\nAna,Lisboa\n")]
    [InlineData("0 clientes com Localidade = Lisboa e Distrito = Porto: os valores existem, mas nenhum cliente tem os dois.")]
    [InlineData("0 clientes sem vendas: ano atual (01-01-2026 a 21-10-2026). Todos os clientes da ficha compraram neste período.")]
    [InlineData("Nenhuma linha encontrada com esse filtro.")]
    [InlineData("Sem vendas: ano atual (01-01-2026 a 31-12-2026).")]
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

    // Esquema mínimo no formato que o McpClientTool expõe (o servidor gera-o a partir dos enums).
    private static readonly JsonElement Esquema = JsonSerializer.Deserialize<JsonElement>(
        """{"type":"object","properties":{"agrupar":{"type":"string","enum":["Cliente","Localidade"]},"coluna":{"type":["string","null"],"enum":["Zona",null]},"limite":{"type":"integer"}}}""");

    private static string? Validar(params (string Nome, object? Valor)[] argumentos)
        => PoliticasSeguranca.ArgumentoInvalido(Esquema, argumentos.Select(a => KeyValuePair.Create(a.Nome, a.Valor)));

    [Fact]
    public void ArgumentoInvalido_ValorForaDoEnum_DizOsPossiveisENaoContaComoDados()
    {
        var r = Validar(("agrupar", JsonSerializer.Deserialize<JsonElement>("\"Localidad\"")));

        Assert.Equal("agrupar=Localidad não existe. Valores possíveis: Cliente, Localidade. Chama de novo com um destes.", r);
        Assert.False(PoliticasSeguranca.ResultadoComDados(r!));
    }

    [Fact]
    public void ArgumentoInvalido_ValoresValidos_Passam()
    {
        Assert.Null(Validar(("agrupar", "localidade"))); // o servidor aceita sem distinguir maiúsculas
        Assert.Null(Validar(("coluna", null), ("limite", 5)));
        Assert.Null(Validar(("coluna", JsonSerializer.Deserialize<JsonElement>("null"))));
    }

    [Fact]
    public void ArgumentoInvalido_ParametroInexistente_DizOsPossiveis()
    {
        var r = Validar(("ordem", "Quantidade"));

        Assert.StartsWith("O parâmetro ordem não existe. Parâmetros possíveis: agrupar, coluna, limite.", r);
        Assert.False(PoliticasSeguranca.ResultadoComDados(r!));
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

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
        Assert.False(PoliticasSeguranca.RespostaSemFonte(PoliticasSeguranca.ForaDoAmbito, 0));

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
    [InlineData("Não foi possível consultar os dados neste momento")]
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

    private static Dictionary<string, object?> Args(string nome, string valor) => new() { [nome] = valor };

    [Theory]
    [InlineData("Vendas em Fernão Ferro este ano", "valor", "FERNAO FERRO")] // sem acentos nem maiúsculas
    [InlineData("Clientes da Charneca por actividade", "valorCruzado", "Charneca, Amora, SXL")] // basta uma palavra
    [InlineData("O que comprou o cliente 1003?", "cliente", "1003")]
    [InlineData("Clientes por zona sem localidade", "valor", "sem dados")] // valor especial da tool
    public void ValorInventado_ValorDaPergunta_Passa(string pergunta, string nome, string valor)
        => Assert.Null(PoliticasSeguranca.ValorInventado(pergunta, "", Args(nome, valor)));

    [Fact]
    public void ValorInventado_FiltroQueNinguemPediu_Recusa()
    {
        var r = PoliticasSeguranca.ValorInventado(
            "Faça uma analise dos valores faturados este ano com os do ano passado?", "", Args("valor", "Seixal"));

        Assert.Equal("valor=Seixal não está na pergunta. Chama de novo sem esse filtro.", r);
        Assert.False(PoliticasSeguranca.ResultadoComDados(r!)); // uma linha: não conta como fonte
    }

    [Fact]
    public void ValorInventado_NomeDaColunaComoValor_Recusa()
        => Assert.StartsWith("valorCruzado=Zona é o nome de uma coluna",
            PoliticasSeguranca.ValorInventado("Como está distribuído o pagamento por zona?", "", Args("valorCruzado", "Zona")));

    [Theory]
    [InlineData("Quais foram as localidades com menos vendas?", true)]
    [InlineData("Que zona fatura mais?", true)]
    [InlineData("Quantos clientes tem cada vendedor?", false)] // "vendedor" não é vendas
    [InlineData("Quantos clientes há por zona?", false)]
    public void ContarEmVezDeVendas_SoRecusaPerguntasDeVendas(string pergunta, bool recusa)
    {
        var r = PoliticasSeguranca.ContarEmVezDeVendas("consultar", pergunta,
            new Dictionary<string, object?> { ["coluna"] = "Localidade", ["contar"] = true });
        Assert.Equal(recusa, r is not null);
    }

    [Fact]
    public void PeriodoEmFalta_HaNoventaDiasSemPeriodo_DevolveOPeriodo()
    {
        Assert.Equal("Ultimos90Dias", PoliticasSeguranca.PeriodoEmFalta("consultar_vendas", "Clientes que não compram há 90 dias?",
            new Dictionary<string, object?> { ["semVendas"] = true }));
        Assert.Equal("Ultimos12Meses", PoliticasSeguranca.PeriodoEmFalta("consultar_vendas", "Vendas nos últimos 12 meses", Args("agrupar", "Mes")));
    }

    [Fact]
    public void PeriodoEmFalta_ComPeriodoOuSemPrazo_Segue()
    {
        Assert.Null(PoliticasSeguranca.PeriodoEmFalta("consultar_vendas", "Clientes que não compram há 90 dias?",
            Args("periodo", "Ultimos90Dias")));
        Assert.Null(PoliticasSeguranca.PeriodoEmFalta("consultar_vendas", "Melhores clientes este ano", Args("agrupar", "Cliente")));
    }

    [Fact]
    public void ValorInventado_CorrigidoPorUmResultadoAnterior_Passa()
        => Assert.Null(PoliticasSeguranca.ValorInventado(
            "Vendas em Seixall", "Nenhuma correspondência. O valor tem de ser um dos existentes:\nLocalidade: Seixal (5)",
            Args("valor", "Seixal")));

    [Fact]
    public void ValorInventado_ParametrosQueNaoSaoTexto_Ignora()
        => Assert.Null(PoliticasSeguranca.ValorInventado("Top clientes", "", new Dictionary<string, object?>
            { ["agrupar"] = "Cliente", ["limite"] = 5, ["coluna"] = "Localidade" }));

    // O modelo copia os exemplos das regras (filtrava por Seixal sem ninguém pedir).
    [Theory]
    [InlineData("Seixal")]
    [InlineData("Azeitão")]
    [InlineData("Corroios")]
    [InlineData("Lisboa")]
    public void Regras_SemValoresReaisDeExemplo(string valorReal)
        => Assert.DoesNotContain(valorReal, PoliticasSeguranca.Regras);

    // Os inícios de frase que o VendasTools usa: se lá mudarem, este teste lembra de mudar o agente.
    [Theory]
    [InlineData("Vendas por mês, ano atual (01-01-2026 a 26-10-2026): total 10,00 € em 1 meses")]
    [InlineData("Melhor artigo em cada distrito, ano atual (01-01-2026 a 26-10-2026): total 10,00 €")]
    [InlineData("Sem vendas: mês atual (01-10-2026 a 26-10-2026).")]
    [InlineData("Clientes sem vendas, últimos 90 dias (29-07-2026 a 26-10-2026): 7 clientes.")]
    [InlineData("0 clientes sem vendas: ano atual (01-01-2026 a 26-10-2026).")]
    public void MostrarDireto_RankingsDeVendas(string texto)
        => Assert.True(PoliticasSeguranca.MostrarDireto("consultar_vendas", texto));

    [Theory]
    [InlineData("consultar_vendas", "emCada tem de ser diferente de agrupar.")] // erro: o modelo corrige
    [InlineData("consultar_vendas", "Nenhuma correspondência. O valor tem de ser um dos existentes:\nLocalidade: Seixal (5)")]
    [InlineData("consultar_vendas", "Vários clientes começam por \"Casa\". Indique o ID de um deles:\nid,cliente\n1,Casa A")]
    [InlineData("consultar", "Vendas por zona")] // CSV das outras tools: o modelo formata
    public void MostrarDireto_ErrosEOutrasTools_VaoAoModelo(string tool, string texto)
        => Assert.False(PoliticasSeguranca.MostrarDireto(tool, texto));

    [Fact]
    public void TextoDoResultado_SemContent_UsaToString()
    {
        Assert.Equal("texto simples", PoliticasSeguranca.TextoDoResultado("texto simples"));
        Assert.Equal("", PoliticasSeguranca.TextoDoResultado(null));
    }
}

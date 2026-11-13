namespace Vendas.Agente.Tests;

public class PaginaChatTests
{
    private static readonly string Html = PaginaChat.Para("DOM\\teste");

    [Fact]
    public void Html_TemBotaoDaConversaEPainelLateral()
    {
        Assert.DoesNotContain("{{UTILIZADOR}}", Html);       // o servidor põe o nome de quem tem a sessão
        Assert.Contains("id=\"mostrar-conversa\"", Html);   // avatar Flow que abre a conversa
        Assert.Contains("id=\"procurar\"", Html);           // pesquisa no painel lateral
        Assert.Contains("id=\"nav\"", Html);                // vistas do Resumo (em vez de tudo numa página)
        Assert.Contains("content=\"light\"", Html);         // só tons claros
    }

    [Fact]
    public void Css_NaoTemComentariosComBarrasDuplas()
    {
        // "//" não é comentário em CSS: anulava o :root dos tokens (--marca-texto, --erro, --serie-1..6 ficavam por definir)
        var inicio = Html.IndexOf("<style>");
        var css = Html[inicio..Html.IndexOf("</style>", inicio)];
        Assert.DoesNotMatch(@"(?m)^\s*//", css);
        Assert.Contains("--marca-texto: #ffffff;", css);
    }

    [Fact]
    public void GraficoMensal_MostraADiferencaDeCadaMesFaceAoAnoAnterior()
    {
        Assert.Contains("'flow-dif'", Html);              // faixa por baixo da área, no ecrã
        Assert.Contains(".flow-dif {", Html);
        Assert.Contains("const comDif = !!s.anoAnt", Html); // e no PNG do "Guardar imagem"
    }

    [Fact]
    public void Html_TemPainelDoDocumentoELigaAsRotasApi()
    {
        Assert.Contains("id=\"dlg-doc\"", Html);
        Assert.Contains("'/api/creditos?'", Html);
        Assert.Contains("/api/clientes/", Html);
        Assert.Contains("Pré-visualização", Html);   // nunca pode parecer o documento fiscal
    }

    [Fact]
    public void EcraDeEntrada_TemOAvatarEmbebido()
    {
        Assert.Contains("Como o Flow responde", Html);
        Assert.Contains("'flow-ini-dica'", Html);
        Assert.Contains("/imagens/avatar-flow.png", Html);
        using var imagem = typeof(PaginaChat).Assembly.GetManifestResourceStream("imagens/avatar-flow.png");
        Assert.NotNull(imagem);   // sem o recurso, a rota dava 404 e o ecrã ficava sem avatar
        Assert.Contains("/imagens/avatar-assistente-vendas.png", Html);
        using var icone = typeof(PaginaChat).Assembly.GetManifestResourceStream("imagens/avatar-assistente-vendas.png");
        Assert.NotNull(icone);    // o ícone da conversa, separado do logótipo
    }

    [Fact]
    public void Html_FaturacaoTemTotalDeDocumentosEmVezDoGraficoPorMes()
    {
        Assert.Contains("Total de documentos", Html);
        Assert.DoesNotContain("Documentos emitidos por mês", Html);
    }

    [Fact]
    public void Html_ExportarTemEmailComRascunhoEOpcoesSemNegrito()
    {
        Assert.Contains("name=\"formato\" value=\"email\"", Html);
        Assert.Contains("X-Unsent: 1", Html);          // sem isto o Outlook abre como email recebido
        Assert.Contains(".dlg-opcao b { font-weight: 400; }", Html);
    }

    [Theory]
    [InlineData("DOMINIO\\Ana.Costa", "Ana Costa")]
    [InlineData("Ana_Costa", "Ana Costa")]
    public void Para_MostraONomeDeQuemPediuSemODominio(string login, string nome)
    {
        Assert.Contains(nome, PaginaChat.Para(login));
        Assert.DoesNotContain("DOMINIO", PaginaChat.Para(login));
    }

 
}

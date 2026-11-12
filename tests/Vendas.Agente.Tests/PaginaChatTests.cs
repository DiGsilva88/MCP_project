namespace Vendas.Agente.Tests;

public class PaginaChatTests
{
    [Fact]
    public void Html_TemBotaoDaConversaEPainelLateral()
    {
        Assert.DoesNotContain("{{UTILIZADOR}}", PaginaChat.Html);       // o servidor põe o nome de quem tem a sessão
        Assert.Contains("id=\"mostrar-conversa\"", PaginaChat.Html);   // avatar Flow que abre a conversa
        Assert.Contains("id=\"procurar\"", PaginaChat.Html);           // pesquisa no painel lateral
        Assert.Contains("id=\"nav\"", PaginaChat.Html);                // vistas do Resumo (em vez de tudo numa página)
        Assert.Contains("content=\"light\"", PaginaChat.Html);         // só tons claros
    }

    [Fact]
    public void Html_TemPainelDoDocumentoELigaAsRotasApi()
    {
        Assert.Contains("id=\"dlg-doc\"", PaginaChat.Html);
        Assert.Contains("'/api/creditos?'", PaginaChat.Html);
        Assert.Contains("/api/clientes/", PaginaChat.Html);
        Assert.Contains("Pré-visualização", PaginaChat.Html);   // nunca pode parecer o documento fiscal
    }

    [Fact]
    public void EcraDeEntrada_TemOAvatarEmbebido()
    {
        Assert.Contains("Como o Flow responde", PaginaChat.Html);
        Assert.Contains("'flow-ini-dica'", PaginaChat.Html);
        Assert.Contains("/imagens/avatar-flow.png", PaginaChat.Html);
        using var imagem = typeof(PaginaChat).Assembly.GetManifestResourceStream("imagens/avatar-flow.png");
        Assert.NotNull(imagem);   // sem o recurso, a rota dava 404 e o ecrã ficava sem avatar
        Assert.Contains("/imagens/avatar-assistente-vendas.png", PaginaChat.Html);
        using var icone = typeof(PaginaChat).Assembly.GetManifestResourceStream("imagens/avatar-assistente-vendas.png");
        Assert.NotNull(icone);    // o ícone da conversa, separado do logótipo
    }

    [Fact]
    public void Html_FaturacaoTemTotalDeDocumentosEmVezDoGraficoPorMes()
    {
        Assert.Contains("Total de documentos", PaginaChat.Html);
        Assert.DoesNotContain("Documentos emitidos por mês", PaginaChat.Html);
    }

    [Fact]
    public void Html_ExportarTemEmailComRascunhoEOpcoesSemNegrito()
    {
        Assert.Contains("name=\"formato\" value=\"email\"", PaginaChat.Html);
        Assert.Contains("X-Unsent: 1", PaginaChat.Html);          // sem isto o Outlook abre como email recebido
        Assert.Contains(".dlg-opcao b { font-weight: 400; }", PaginaChat.Html);
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

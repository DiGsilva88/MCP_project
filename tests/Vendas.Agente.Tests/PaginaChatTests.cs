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
}

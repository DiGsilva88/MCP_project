namespace Vendas.Agente.Tests;

public class PaginaChatTests
{
    [Fact]
    public void Html_TemBotaoDaConversaEPainelLateral()
    {
        Assert.DoesNotContain("{{UTILIZADOR}}", PaginaChat.Html);       // o servidor põe o nome de quem tem a sessão
        Assert.Contains("id=\"mostrar-conversa\"", PaginaChat.Html);   // avatar Flow que abre a conversa
        Assert.Contains("id=\"lateral-resumo\"", PaginaChat.Html);     // resumo guardado no painel lateral
    }
}

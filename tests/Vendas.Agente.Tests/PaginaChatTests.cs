namespace Vendas.Agente.Tests;

public class PaginaChatTests
{
    [Fact]
    public void Html_TemAvatarFlowEPainelLateral()
    {
        Assert.DoesNotContain("{{AVATAR}}", PaginaChat.Html);
        Assert.Contains("id=\"mostrar-conversa\"", PaginaChat.Html);   // avatar Flow que abre a conversa
        Assert.Contains("id=\"lateral-resumo\"", PaginaChat.Html);     // resumo guardado no painel lateral
    }
}

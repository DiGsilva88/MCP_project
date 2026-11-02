namespace Vendas.Agente.Tests;

public class PaginaChatTests
{
    [Fact]
    public void Html_TemAvatarEmbebido()
    {
        Assert.DoesNotContain("{{AVATAR}}", PaginaChat.Html);
        Assert.Contains("id=\"avatar\" src=\"data:image/png;base64,", PaginaChat.Html);
    }
}

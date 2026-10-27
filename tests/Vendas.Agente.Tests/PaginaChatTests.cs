namespace Vendas.Agente.Tests;

public class PaginaChatTests
{
    [Fact]
    public void Html_TemLogoEmbebido()
    {
        Assert.DoesNotContain("{{LOGO}}", PaginaChat.Html);
        Assert.Contains("src=\"data:image/png;base64,", PaginaChat.Html);
    }

    [Fact]
    public void Html_TemAvatarEmbebido()
    {
        Assert.DoesNotContain("{{AVATAR}}", PaginaChat.Html);
        Assert.Contains("src=\"data:image/svg+xml;base64,", PaginaChat.Html);
    }
}

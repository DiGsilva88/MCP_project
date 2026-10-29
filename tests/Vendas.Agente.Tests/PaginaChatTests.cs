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
        // Logo e avatar são ambos PNG: procura-se pelo id para não passar só com o logo.
        Assert.Contains("id=\"avatar\" src=\"data:image/png;base64,", PaginaChat.Html);
    }
}

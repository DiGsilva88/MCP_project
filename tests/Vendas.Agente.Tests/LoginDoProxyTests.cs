namespace Vendas.Agente.Tests;

public class LoginDoProxyTests
{
    [Fact]
    public void SegredoCertoEmLoopback_DevolveLogin()
    {
        Assert.Equal(@"DOM\ana", Perfis.LoginDoProxy(@" DOM\ana ", "s3", "s3", true));
    }

    [Theory]
    [InlineData(@"DOM\ana", "errado", "s3", true)]
    [InlineData(@"DOM\ana", null, "s3", true)]
    [InlineData(@"DOM\ana", "s3", "s3", false)]
    [InlineData("", "s3", "s3", true)]
    [InlineData(null, "s3", "s3", true)]
    [InlineData(@"DOM\ana", "", "", true)]
    [InlineData(@"DOM\ana", "s3", null, true)]
    public void CasosInvalidos_DevolvemNull(string? utilizador, string? cabecalho, string? segredo, bool loopback)
    {
        Assert.Null(Perfis.LoginDoProxy(utilizador, cabecalho, segredo, loopback));
    }
}

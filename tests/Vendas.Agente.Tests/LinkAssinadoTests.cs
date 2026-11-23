namespace Vendas.Agente.Tests;

public class LinkAssinadoTests
{
    const string Segredo = "0123456789abcdef0123456789abcdef";
    static readonly DateTimeOffset Agora = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);
    static readonly TimeSpan Dez = TimeSpan.FromMinutes(10);

    // Assinatura como o Visual a calcula: HMAC-SHA256(segredo, "login|validade") em hex minúsculo.
    static string Sig(string login, long exp) => Convert.ToHexStringLower(System.Security.Cryptography.HMACSHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(Segredo), System.Text.Encoding.UTF8.GetBytes($"{login}|{exp}")));

    [Fact]
    public void LinkValido_DevolveLogin()
    {
        var exp = Agora.AddMinutes(2).ToUnixTimeSeconds();
        Assert.Equal(@"cli\ana.silva", Perfis.LoginValido(@"cli\ana.silva", exp.ToString(), Sig(@"cli\ana.silva", exp), Segredo, Agora, Dez));
    }

    [Fact]
    public void Expirado_Adulterado_OuValidadeLonga_DevolveNull()
    {
        var exp = Agora.AddMinutes(2).ToUnixTimeSeconds();
        var longa = Agora.AddHours(5).ToUnixTimeSeconds();
        Assert.Null(Perfis.LoginValido("ana", (exp - 600).ToString(), Sig("ana", exp - 600), Segredo, Agora, Dez));   // expirou
        Assert.Null(Perfis.LoginValido("outro", exp.ToString(), Sig("ana", exp), Segredo, Agora, Dez));                // login trocado
        Assert.Null(Perfis.LoginValido("ana", exp.ToString(), Sig("ana", exp), "outro-segredo-----------------------", Agora, Dez));
        Assert.Null(Perfis.LoginValido("ana", longa.ToString(), Sig("ana", longa), Segredo, Agora, Dez));              // o Visual não emite links de horas
        Assert.Null(Perfis.LoginValido("ana", exp.ToString(), Sig("ana", exp), "", Agora, Dez));                       // modo desligado
        Assert.Null(Perfis.LoginValido("ana", "x", "x", Segredo, Agora, Dez));
    }

    [Fact]
    public void Sessao_ViaCookie_FuncionaEExpira()
    {
        var cookie = Perfis.CriarSessao(@"cli\ana.silva", Agora.AddHours(8), Segredo);
        Assert.Equal(@"cli\ana.silva", Perfis.LoginDaSessao(cookie, Segredo, Agora.AddHours(1), TimeSpan.FromHours(8)));
        Assert.Null(Perfis.LoginDaSessao(cookie, Segredo, Agora.AddHours(9), TimeSpan.FromHours(8)));
        Assert.Null(Perfis.LoginDaSessao(cookie.Replace("ana", "rui"), Segredo, Agora, TimeSpan.FromHours(8)));
        Assert.Null(Perfis.LoginDaSessao(null, Segredo, Agora, TimeSpan.FromHours(8)));
    }
}

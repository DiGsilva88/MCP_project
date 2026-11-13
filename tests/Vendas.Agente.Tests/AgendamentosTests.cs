namespace Vendas.Agente.Tests;

public class AgendamentosTests
{
    [Theory]
    [InlineData("""{"freq":"semanal","email":"diana.silva@inforap.pt"}""", "semanal", "diana.silva@inforap.pt")]
    [InlineData("""{"freq":"mensal","email":"  Ana@INFORAP.PT "}""", "mensal", "Ana@INFORAP.PT")]
    public void DoPedido_AceitaEnderecosDaEmpresa(string corpo, string freq, string email)
        => Assert.Equal((new Agendamento(freq, email), (string?)null), Agendamentos.DoPedido(corpo));

    [Theory]
    [InlineData("""{"freq":"semanal","email":"alguem@gmail.com"}""")]
    [InlineData("""{"freq":"semanal","email":"x@inforap.pt.evil.com"}""")]
    [InlineData("""{"freq":"semanal","email":"x@sub.inforap.pt"}""")]
    [InlineData("""{"freq":"semanal","email":"Nome <x@inforap.pt>"}""")]
    [InlineData("""{"freq":"semanal","email":"x@inforap.pt, y@gmail.com"}""")]
    [InlineData("""{"freq":"semanal"}""")]
    [InlineData("""{"freq":"diario","email":"x@inforap.pt"}""")]
    [InlineData("""{"email":"x@inforap.pt"}""")]
    [InlineData("não é json")]
    [InlineData("null")]
    public void DoPedido_RecusaOResto(string corpo)
    {
        var (agendamento, erro) = Agendamentos.DoPedido(corpo);
        Assert.Null(agendamento);
        Assert.NotNull(erro);
    }

    [Theory]
    [InlineData("semanal", "2026-11-02", true)]   // segunda
    [InlineData("semanal", "2026-11-09", true)]   // segunda
    [InlineData("semanal", "2026-11-04", false)]  // quarta
    [InlineData("mensal", "2026-11-02", true)]    // 1.ª segunda de novembro
    [InlineData("mensal", "2026-11-09", false)]   // 2.ª segunda
    [InlineData("mensal", "2026-12-07", true)]    // 1.ª segunda de dezembro é dia 7
    [InlineData("mensal", "2026-12-01", false)]   // terça
    public void Devido(string freq, string hoje, bool esperado)
        => Assert.Equal(esperado, Agendamentos.Devido(freq, DateOnly.Parse(hoje)));

    [Theory]
    [InlineData("semanal", "2026-11-02", "2026-10-26", "2026-11-02")] // segunda: a semana de 26-10 a 01-11
    [InlineData("semanal", "2026-11-04", "2026-10-26", "2026-11-02")] // corrido à mão a meio da semana: a mesma
    [InlineData("semanal", "2026-11-08", "2026-10-26", "2026-11-02")] // domingo: ainda a mesma
    [InlineData("mensal", "2026-11-02", "2026-10-01", "2026-11-01")]
    [InlineData("mensal", "2026-01-05", "2025-12-01", "2026-01-01")]  // muda o ano
    public void Janela(string freq, string hoje, string desde, string ate)
        => Assert.Equal((DateOnly.Parse(desde), DateOnly.Parse(ate)), Agendamentos.Janela(freq, DateOnly.Parse(hoje)));

    [Fact]
    public void Guardar_UmPorEndereco()
    {
        var ficheiro = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "agendamentos.json");
        try
        {
            Agendamentos.Guardar(ficheiro, new("semanal", "a@inforap.pt"));
            Agendamentos.Guardar(ficheiro, new("semanal", "b@inforap.pt"));
            Agendamentos.Guardar(ficheiro, new("mensal", "A@inforap.pt"));   // troca a frequência do primeiro

            Assert.Equal([new("semanal", "b@inforap.pt"), new("mensal", "A@inforap.pt")], Agendamentos.Ler(ficheiro));

            Agendamentos.Guardar(ficheiro, new(Agendamentos.Cancelar, "a@inforap.pt")); // deixa de receber
            Assert.Equal([new("semanal", "b@inforap.pt")], Agendamentos.Ler(ficheiro));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(ficheiro)!, true);
        }
    }

    [Fact]
    public void DoPedido_CriadorNuncaVemDoBrowser() =>
        Assert.Null(Agendamentos.DoPedido("""{"freq":"semanal","email":"a@inforap.pt","criador":"DOM\\outro"}""").Agendamento!.Criador);

    [Fact]
    public void Guardar_EmParalelo_NaoPerdeNenhum()
    {
        var ficheiro = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "agendamentos.json");
        try
        {
            Parallel.For(0, 20, i => Agendamentos.Guardar(ficheiro, new("semanal", $"p{i}@inforap.pt", "DOM\\ana")));

            Assert.Equal(20, Agendamentos.Ler(ficheiro).Count);
            Assert.All(Agendamentos.Ler(ficheiro), a => Assert.Equal("DOM\\ana", a.Criador));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(ficheiro)!, true);
        }
    }

    [Fact]
    public void Ler_FicheiroCorrompido_DizQualE()
    {
        var ficheiro = Path.GetTempFileName();
        try
        {
            File.WriteAllText(ficheiro, "[{\"freq\":");
            var erro = Assert.Throws<InvalidDataException>(() => Agendamentos.Ler(ficheiro));
            Assert.Contains(ficheiro, erro.Message);
        }
        finally { File.Delete(ficheiro); }
    }

    [Fact]
    public void CorpoHtml_CodificaOsTextos()
    {
        var html = Agendamentos.CorpoHtml("Resumo", [("Clientes", "1. <script>alert(1)</script> & Filhos")]);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; Filhos", html);
    }
}

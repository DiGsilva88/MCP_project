namespace Vendas.Agente.Tests;

public class PreferenciasTests
{
    [Fact]
    public void Ficheiro_UmPorLogin_SemOLoginNoNome()
    {
        var pasta = Path.GetTempPath();
        var ana = Preferencias.Ficheiro(pasta, "DOM\\ana");

        Assert.Equal(ana, Preferencias.Ficheiro(pasta, "dom\\ANA"));          // o Windows não distingue maiúsculas
        Assert.NotEqual(ana, Preferencias.Ficheiro(pasta, "DOM\\rui"));
        Assert.DoesNotContain("ana", Path.GetFileName(ana));
        Assert.Equal(Path.Combine(pasta, "utilizadores"), Path.GetDirectoryName(ana));
    }

    [Theory]
    [InlineData("""{"guardados":[],"alertas":[],"filtros":{}}""", true)]
    [InlineData("""{"guardados":[{"nome":"x"}]}""", true)]
    [InlineData("""{"outra":[]}""", false)]
    [InlineData("""{"guardados":"texto"}""", false)]
    [InlineData("""[]""", false)]
    [InlineData("""não é json""", false)]
    public void Validar_SoAceitaAsChavesConhecidas(string corpo, bool aceite) =>
        Assert.Equal(aceite, Preferencias.Validar(corpo) is null);

    [Fact]
    public void Validar_RecusaCorposGrandes() =>
        Assert.NotNull(Preferencias.Validar("{\"guardados\":[\"" + new string('a', Preferencias.MaxCaracteres) + "\"]}"));

    [Fact]
    public void GuardarELer_DevolveOMesmo_ESemFicheiroDaObjetoVazio()
    {
        var ficheiro = Preferencias.Ficheiro(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), "DOM\\ana");
        Assert.Equal("{}", Preferencias.Ler(ficheiro));

        Preferencias.Guardar(ficheiro, """{"guardados":[]}""");

        Assert.Equal("""{"guardados":[]}""", Preferencias.Ler(ficheiro));
        Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(ficheiro))!, true);
    }
}

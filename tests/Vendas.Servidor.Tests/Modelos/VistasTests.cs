using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Tests.Modelos;

public class VistasTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(sem distrito)")]
    [InlineData("(sem zona)")]
    [InlineData("0 - sem plafond")]
    [InlineData("0 - sem volume declarado")]
    public void Limpar_SemValor_DevolveSemDados(string? entrada)
    {
        Assert.Equal("sem dados", Campos.Limpar(entrada));
    }

    [Theory]
    [InlineData("Lisboa", "Lisboa")]
    [InlineData("  Porto ", "Porto")]
    [InlineData("1 - até 50 mil", "1 - até 50 mil")]
    public void Limpar_ComValor_MantemOValor(string entrada, string esperado)
    {
        Assert.Equal(esperado, Campos.Limpar(entrada));
    }
}

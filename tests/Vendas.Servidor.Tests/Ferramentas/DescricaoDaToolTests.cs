using System.ComponentModel;
using System.Reflection;
using Vendas.Servidor.Ferramentas;
using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Tests.Ferramentas;

public class DescricaoDaToolTests
{
    // A descrição é o que o modelo lê. Se alguém acrescenta uma coluna ao enum e esquece
    // a descrição, o modelo nunca saberá que ela existe: este teste apanha o esquecimento.
    [Theory]
    [MemberData(nameof(NomesDasColunas))]
    public void Descricao_MencionaTodasAsColunasDoEnum(string coluna)
    {
        var descricao = typeof(VistasTools).GetMethod(nameof(VistasTools.ConsultarAsync))!
            .GetCustomAttribute<DescriptionAttribute>()!.Description;

        Assert.Contains(coluna, descricao);
    }

    [Theory]

    [MemberData(nameof(NomesDasDimensoes))]
    public void DescricaoVendas_MencionaTodasAsDimensoes(string dimensao)
    {
        var descricao = typeof(VendasTools).GetMethod(nameof(VendasTools.ConsultarVendasAsync))!
            .GetCustomAttribute<DescriptionAttribute>()!.Description;

        Assert.Contains(dimensao, descricao);
    }

    // O modelo pequeno copia os exemplos: "valor=Seixal" na descrição fazia-o filtrar por Seixal sem ninguém pedir.
    // Verifica as descrições das tools e de todos os parâmetros.
    [Theory]
    [InlineData("Seixal")]
    [InlineData("Azeitão")]
    [InlineData("Corroios")]
    [InlineData("Lisboa")]
    [InlineData("Setúbal")]
    public void Descricoes_SemValoresReaisDeExemplo(string valorReal)
    {
        var metodos = new[] { typeof(VistasTools), typeof(VendasTools) }
            .SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttribute<DescriptionAttribute>() is not null);

        foreach (var m in metodos)
        {
            Assert.DoesNotContain(valorReal, m.GetCustomAttribute<DescriptionAttribute>()!.Description);
            foreach (var p in m.GetParameters())
                Assert.DoesNotContain(valorReal, p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "");
        }
    }

    public static TheoryData<string> NomesDasDimensoes() => new(Enum.GetNames<DimensaoVenda>());

    public static TheoryData<string> NomesDasColunas() => new(Enum.GetNames<Campo>());
}

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

    public static TheoryData<string> NomesDasColunas() => new(Enum.GetNames<Campo>());
}

using System.Reflection;
using ModelContextProtocol.Server;
using Vendas.Servidor.Ferramentas;

namespace Vendas.Servidor.Tests.Ferramentas;

// WithToolsFromAssembly só regista classes com [McpServerToolType]. Sem o atributo a tool
// desaparece da lista sem erro nenhum e o modelo improvisa com as outras.
public class RegistoToolsTests
{
    private static readonly Type[] ClassesComTools = typeof(VistasTools).Assembly.GetTypes()
        .Where(t => t.GetMethods().Any(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null))
        .ToArray();

    [Fact]
    public void ClassesComTools_TemTodasMcpServerToolType()
    {
        var semAtributo = ClassesComTools
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
            .Select(t => t.Name);

        Assert.Empty(semAtributo);
    }

    [Fact]
    public void Tools_ExistemTodasAsEsperadas()
    {
        var nomes = ClassesComTools
            .SelectMany(t => t.GetMethods())
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToHashSet();

        Assert.Superset(new HashSet<string> { "consultar", "consultar_vendas", "consultar_sensivel" }, nomes);
    }
}

using Microsoft.Data.SqlClient;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Tests.Dados;

public class ConsultaVendasTests
{
    private static SqlParameterCollection Aplicar(Action<SqlParameterCollection> aplicar)
    {
        var comando = new SqlCommand();
        aplicar(comando.Parameters);
        return comando.Parameters;
    }

    private static readonly DateOnly Desde = new(2024, 1, 1), Ate = new(2025, 1, 1);

    [Fact]
    public void TopClientes_SemCliente_NaoFiltraNemTemParametro()
    {
        var (sql, aplicar) = ConsultaVendas.TopClientes(Desde, Ate);

        Assert.Contains("CAST(0 AS decimal(18,2))", sql);
        Assert.DoesNotContain("@cliente", sql);
        Assert.False(Aplicar(aplicar).Contains("@cliente"));
    }

    [Fact]
    public void TopArtigos_ComCliente_FiltraSoPeloIdExatoEParametriza()
    {
        var (sql, aplicar) = ConsultaVendas.TopArtigos(DimensaoVenda.Artigo, Desde, Ate, " 1003 ");

        Assert.Contains("AND CAST([ClienteID] AS nvarchar(50)) = @cliente", sql);
        Assert.DoesNotContain("LIKE", sql); // a procura por nome é feita antes, na ProcurarClientes
        Assert.Equal("1003", Aplicar(aplicar)["@cliente"].Value);
    }

    [Fact]
    public void ProcurarClientes_IdOuInicioDoNomeSemWildcards()
    {
        var (sql, aplicar) = ConsultaVendas.ProcurarClientes(" Casa% ");

        Assert.Contains("REPLACE(REPLACE(REPLACE(@cliente", sql); // o % do modelo não é wildcard
        Assert.Contains("ORDER BY CASE WHEN CAST([ClienteID] AS nvarchar(50)) = @cliente THEN 0", sql); // ID exato primeiro
        Assert.Equal("Casa%", Aplicar(aplicar)["@cliente"].Value);
    }

    [Fact]
    public void SomasSemValores_DaoZeroENaoNull()
    {
        // Um SUM só de NULL dá NULL e o GetDecimal rebentava com a consulta toda.
        foreach (var (sql, _) in new[] { ConsultaVendas.TopClientes(Desde, Ate), ConsultaVendas.TopArtigos(DimensaoVenda.Marca, Desde, Ate) })
            Assert.DoesNotMatch(@"(?<!COALESCE\()SUM\(\[", sql);
    }

    [Fact]
    public void TopArtigos_Mes_OrdenaPorMesMaisRecente()
    {
        var (sql, _) = ConsultaVendas.TopArtigos(DimensaoVenda.Mes, Desde, Ate);

        Assert.Contains("GROUP BY CONVERT(char(7), [DataDoc], 120)", sql);
        Assert.Contains("ORDER BY Nome DESC;", sql);
    }
}

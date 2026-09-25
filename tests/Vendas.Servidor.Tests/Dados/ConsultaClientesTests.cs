using Microsoft.Data.SqlClient;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Tests.Dados;

public class ConsultaClientesTests
{
    private static SqlParameterCollection Aplicar(Action<SqlParameterCollection> aplicar)
    {
        var comando = new SqlCommand();
        aplicar(comando.Parameters);
        return comando.Parameters;
    }

    [Fact]
    public void Listar_SemFiltros_NaoTemWhereEPaginaComOffset()
    {
        var (sql, aplicarParametros) = ConsultaClientes.Listar(
            [Campo.Zona], new Dictionary<Campo, string>(), nome: null, deslocamento: 20);

        Assert.DoesNotContain("AND", sql);
        Assert.Contains("OFFSET @deslocamento ROWS FETCH NEXT @limite ROWS ONLY", sql);

        var parametros = Aplicar(aplicarParametros);
        Assert.Equal(DBNull.Value, parametros["@nome"].Value);
        Assert.Equal(20, parametros["@deslocamento"].Value);
    }

    [Fact]
    public void Listar_ComFiltro_UsaAColunaCorretaEParametrizaOValor()
    {
        var filtros = new Dictionary<Campo, string> { [Campo.Zona] = "Lisboa" };
        var (sql, aplicarParametros) = ConsultaClientes.Listar([Campo.Zona], filtros, nome: null, deslocamento: 0);

        Assert.Contains("AND c.[Zona] = @filtro0", sql);
        Assert.Equal("Lisboa", Aplicar(aplicarParametros)["@filtro0"].Value);
    }

    [Fact]
    public void Listar_FiltroSemDados_GeraCondicaoDeNuloOuMarcadorSemParametro()
    {
        var filtros = new Dictionary<Campo, string> { [Campo.Distrito] = Campos.SemDados };
        var (sql, aplicarParametros) = ConsultaClientes.Listar([Campo.Distrito], filtros, nome: null, deslocamento: 0);

        Assert.Contains("c.[Distrito] IS NULL", sql);
        Assert.Contains("c.[Distrito] = ''", sql);
        Assert.Contains("c.[Distrito] LIKE '(sem %'", sql);
        Assert.Contains("c.[Distrito] LIKE '0 - sem %'", sql);

        // "sem dados" não é um valor real da BD: não deve virar parâmetro de igualdade.
        Assert.False(Aplicar(aplicarParametros).Contains("@filtro0"));
    }

    [Fact]
    public void Contar_SemFiltros_AgrupaPelaColunaPedidaSemWhere()
    {
        var (sql, _) = ConsultaClientes.Contar(Campo.Pagamento, new Dictionary<Campo, string>());

        Assert.Contains("GROUP BY f.[Pagamento]", sql);
        Assert.DoesNotContain("WHERE", sql);
    }

    [Fact]
    public void Contar_ComFiltroNoutraColuna_AplicaWhereEParametrizaOValor()
    {
        var filtros = new Dictionary<Campo, string> { [Campo.Cobranca] = "Débito Direto" };
        var (sql, aplicarParametros) = ConsultaClientes.Contar(Campo.Pagamento, filtros);

        Assert.Contains("WHERE f.[Cobranca] = @filtro0", sql);
        Assert.Equal("Débito Direto", Aplicar(aplicarParametros)["@filtro0"].Value);
    }
}

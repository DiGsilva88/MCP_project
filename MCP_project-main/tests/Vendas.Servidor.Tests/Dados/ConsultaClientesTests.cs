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
            [Campo.Zona], new Dictionary<Campo, string>(), deslocamento: 20);

        Assert.DoesNotContain("WHERE", sql);
        Assert.Contains("OFFSET @deslocamento ROWS FETCH NEXT @limite ROWS ONLY", sql);
        Assert.Equal(20, Aplicar(aplicarParametros)["@deslocamento"].Value);
    }

    [Fact]
    public void Listar_ComFiltro_UsaAColunaCorretaEParametrizaOValor()
    {
        var filtros = new Dictionary<Campo, string> { [Campo.Zona] = "Lisboa" };
        var (sql, aplicarParametros) = ConsultaClientes.Listar([Campo.Zona], filtros, deslocamento: 0);

        Assert.Contains("WHERE (c.[Zona] COLLATE Latin1_General_CI_AI = @filtro0", sql);
        Assert.Equal("Lisboa", Aplicar(aplicarParametros)["@filtro0"].Value);
    }

    [Fact]
    public void Contar_CruzadoComColunaDaOutraView_AgrupaNumaEFiltraNaOutra()
    {
        var filtros = new Dictionary<Campo, string> { [Campo.Pagamento] = "30 dias" };
        var (sql, aplicarParametros) = ConsultaClientes.Contar(Campo.Zona, filtros);

        Assert.Contains("GROUP BY c.[Zona]", sql);
        Assert.Contains("WHERE (f.[Pagamento] COLLATE Latin1_General_CI_AI = @filtro0", sql);
        Assert.Equal("30 dias", Aplicar(aplicarParametros)["@filtro0"].Value);
    }

    [Fact]
    public void Listar_FiltroSemDados_GeraCondicaoDeNuloOuMarcadorSemParametro()
    {
        var filtros = new Dictionary<Campo, string> { [Campo.Distrito] = Campos.SemDados };
        var (sql, aplicarParametros) = ConsultaClientes.Listar([Campo.Distrito], filtros, deslocamento: 0);

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

        Assert.Contains("WHERE (f.[Cobranca] COLLATE Latin1_General_CI_AI = @filtro0", sql);
        Assert.Equal("Débito Direto", Aplicar(aplicarParametros)["@filtro0"].Value);
    }

    [Fact]
    public void Listar_Filtro_AceitaInicioDoValorEEscapaWildcards()
    {
        var filtros = new Dictionary<Campo, string> { [Campo.Pagamento] = "30 dias" };
        var (sql, aplicarParametros) = ConsultaClientes.Listar([Campo.Pagamento], filtros, deslocamento: 0);

        // "30 dias" tem de apanhar "30 Dias Fim do Mês": prefixo + espaço, com [ % _ escapados.
        Assert.Contains("f.[Pagamento] COLLATE Latin1_General_CI_AI LIKE REPLACE(REPLACE(REPLACE(@filtro0, '[', '[[]'), '%', '[%]'), '_', '[_]') + ' %'", sql);
        Assert.Equal("30 dias", Aplicar(aplicarParametros)["@filtro0"].Value);
    }
}

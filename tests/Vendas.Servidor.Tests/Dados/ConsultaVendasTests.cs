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

    private static FiltrosVenda Filtros(string[]? zonas = null, string[]? marcas = null, string[]? tipos = null) =>
        FiltrosVenda.Criar(zonas, marcas, tipos).Filtros!;

    [Fact]
    public void FiltrosDoPainel_VariasZonasMarcasETipos_ChegamAoSqlComoParametros()
    {
        var f = Filtros(["Braga", "Porto'; DROP"], ["Bosch"], ["F", "V"]);
        f[Campo.Vendedor] = "Ana";

        var (sql, aplicar) = ConsultaVendas.DocumentosPorTipo(Desde, Ate, null, f);
        var p = Aplicar(aplicar);

        Assert.Contains("c.[Distrito] COLLATE Latin1_General_CI_AI IN (@zona0, @zona1)", sql);
        Assert.Contains("[Marca] COLLATE Latin1_General_CI_AI IN (@marca0)", sql);
        Assert.Contains("[TipoDocID] IN (@tipodoc0, @tipodoc1)", sql);
        Assert.DoesNotContain("DROP", sql);                          // os valores só vão como parâmetro
        Assert.Equal("Porto'; DROP", p["@zona1"].Value);
        Assert.Equal("V", p["@tipodoc1"].Value);
        Assert.Equal("Ana", p["@filtro0"].Value);                    // o vendedor do perfil continua a valer
    }

    [Fact]
    public void FiltrosDoPainel_ZonaSemDados_SaoOsClientesSemDistrito()
    {
        var (sql, aplicar) = ConsultaVendas.RankingPorCliente(Desde, Ate, null, Filtros(["sem dados"]));

        Assert.Contains("c.[Distrito] IS NULL", sql);
        Assert.DoesNotContain("@zona0", sql);
        Assert.False(Aplicar(aplicar).Contains("@zona0"));
    }

    [Fact]
    public void FiltrosDoPainel_NaProcuraDeClientes_SoAsZonas()
    {
        // A ficha não tem Marca nem TipoDocID: com eles, a procura do cliente dava erro de SQL
        var (sql, _) = ConsultaVendas.ProcurarClientes("Casa", Filtros(["Braga"], ["Bosch"], ["F"]));

        Assert.Contains("@zona0", sql);
        Assert.DoesNotContain("[Marca]", sql);
        Assert.DoesNotContain("@tipodoc0", sql);
    }

    [Fact]
    public void FiltrosDoPainel_ClientesSemVendas_RespeitamAsZonas()
    {
        var (sql, aplicar) = ConsultaVendas.ClientesSemVendas(Desde, Ate, Filtros(["Braga"]));

        Assert.Contains("c.ClienteID IN (SELECT c.ClienteID", sql);
        Assert.Equal("Braga", Aplicar(aplicar)["@zona0"].Value);
    }

    [Theory]
    [InlineData(new[] { "F'" }, null)]                // tipo só com letras e números
    [InlineData(null, 31)]                             // no máximo 30 valores
    public void FiltrosVenda_RecusaValoresEstranhos(string[]? tipos, int? zonas) =>
        Assert.Null(FiltrosVenda.Criar(zonas is { } n ? Enumerable.Range(0, n).Select(i => "Z" + i).ToArray() : null, null, tipos).Filtros);

    [Fact]
    public void PaginaCreditos_PaginaNoSqlEParametrizaAPesquisa()
    {
        var (sql, aplicar) = ConsultaVendas.PaginaCreditos(Desde, Ate, "N", "50%_[x", 30, null, null);
        var p = Aplicar(aplicar);

        Assert.Contains("OFFSET @deslocamento ROWS FETCH NEXT @limite ROWS ONLY", sql);
        Assert.Contains("[TipoDocID] IN ('N', 'C')", sql);
        Assert.DoesNotContain("50%", sql);                     // o texto só vai como parâmetro
        Assert.Equal(@"%50\%\_\[x%", p["@pesquisa"].Value);     // %, _ e [ não funcionam como padrões
        Assert.Equal("N", p["@tipo"].Value);
        Assert.Equal(30, p["@deslocamento"].Value);
    }

    [Fact]
    public void PaginaCreditos_SemPesquisaNemTipo_NaoTemEssesFiltros()
    {
        var (sql, aplicar) = ConsultaVendas.PaginaCreditos(Desde, Ate, null, " ", 0, null, null);

        Assert.DoesNotContain("@pesquisa", sql);
        Assert.DoesNotContain("@tipo", sql);
        Assert.False(Aplicar(aplicar).Contains("@pesquisa"));
    }

    [Fact]
    public void Creditos_FiltroDeVendedor_ChegaAPaginaEAosContadores()
    {
        var filtros = new Dictionary<Campo, string> { [Campo.Vendedor] = "Ana" };

        var (sqlPagina, _) = ConsultaVendas.PaginaCreditos(Desde, Ate, null, null, 0, null, filtros);
        var (sqlContagem, _) = ConsultaVendas.ContagemCreditos(Desde, Ate, null, null, filtros);

        Assert.Contains("[ClienteID] IN (SELECT c.ClienteID", sqlPagina);
        Assert.Contains("[ClienteID] IN (SELECT c.ClienteID", sqlContagem);
    }

    [Fact]
    public void DetalheDocumento_FiltraPeloAnoDoNumero()
    {
        var (sql, aplicar) = ConsultaVendas.DetalheDocumento("N 10/2026/5", "N", 2026, null);
        var p = Aplicar(aplicar);

        Assert.Contains("[DocumentoID] = @documento", sql);
        Assert.Equal(new DateTime(2026, 1, 1), p["@desde"].Value);
        Assert.Equal(new DateTime(2027, 1, 1), p["@ate"].Value);
    }

    [Fact]
    public void RankingPorCliente_SemCliente_NaoFiltraNemTemParametro()
    {
        var (sql, aplicar) = ConsultaVendas.RankingPorCliente(Desde, Ate);

        Assert.Contains("CAST(0 AS decimal(18,2))", sql);
        Assert.DoesNotContain("@cliente", sql);
        Assert.False(Aplicar(aplicar).Contains("@cliente"));
    }

    [Fact]
    public void RankingPorDimensao_ComCliente_FiltraSoPeloIdExatoEParametriza()
    {
        var (sql, aplicar) = ConsultaVendas.RankingPorDimensao(DimensaoVenda.Artigo, Desde, Ate, " 1003 ");

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
    public void ProcurarClientes_ComVendedor_SoProcuraNosClientesDele()
    {
        var (sql, aplicar) = ConsultaVendas.ProcurarClientes("A", new Dictionary<Campo, string> { [Campo.Vendedor] = "Ana" });

        Assert.Contains("[ClienteID] IN (SELECT c.ClienteID", sql);
        Assert.Equal("Ana", Aplicar(aplicar)["@filtro0"].Value);
    }

    [Fact]
    public void CarteiraClientes_ComVendedor_NaoLeOHistoricoDeTodos()
    {
        var (sql, aplicar) = ConsultaVendas.CarteiraClientes(new Dictionary<Campo, string> { [Campo.Vendedor] = "Ana" });

        Assert.Contains("[ClienteID] IN (SELECT c.ClienteID", sql);
        Assert.Equal("Ana", Aplicar(aplicar)["@filtro0"].Value);
    }

    [Fact]
    public void SomasSemValores_DaoZeroENaoNull()
    {
        // Um SUM só de NULL dá NULL e o GetDecimal rebentava com a consulta toda.
        foreach (var (sql, _) in new[] { ConsultaVendas.RankingPorCliente(Desde, Ate), ConsultaVendas.RankingPorDimensao(DimensaoVenda.Marca, Desde, Ate) })
            Assert.DoesNotMatch(@"(?<!COALESCE\()SUM\(\[", sql);
    }

    [Fact]
    public void RankingPorDimensao_Mes_OrdenaDoMaisAntigoAoMaisRecente()
    {
        var (sql, _) = ConsultaVendas.RankingPorDimensao(DimensaoVenda.Mes, Desde, Ate);

        Assert.Contains("GROUP BY CONVERT(char(7), [DataDoc], 120)", sql);
        Assert.Contains("ORDER BY Nome;", sql);
    }

    [Fact]
    public void RankingPorDimensao_Trimestre_AgrupaPorAnoETrimestre()
    {
        var (sql, _) = ConsultaVendas.RankingPorDimensao(DimensaoVenda.Trimestre, Desde, Ate);

        Assert.Contains("CONCAT(YEAR([DataDoc]), '-T', DATEPART(QUARTER, [DataDoc])) AS Nome", sql);
        Assert.Contains("GROUP BY YEAR([DataDoc]), DATEPART(QUARTER, [DataDoc])", sql);
        Assert.Contains("ORDER BY Nome;", sql);
    }

    [Fact]
    public void RankingPorDimensao_EmCada_FicaComO1DeCadaGrupo()
    {
        var (sql, aplicar) = ConsultaVendas.RankingPorDimensao(
            DimensaoVenda.Artigo, Desde, Ate, emCada: DimensaoVenda.Distrito,
            filtros: new Dictionary<Campo, string> { [Campo.Zona] = "Norte" });

        Assert.Contains("PARTITION BY ficha.ValorFicha COLLATE Latin1_General_CI_AI ORDER BY COALESCE(SUM([ValorLiquido]), 0) DESC", sql);
        Assert.Contains("GROUP BY ficha.ValorFicha COLLATE Latin1_General_CI_AI, [ArtigoID], [Artigo]", sql);
        Assert.Contains("OUTER APPLY (SELECT c.[Distrito] AS ValorFicha", sql); // a ficha vem do emCada
        Assert.Contains("WHERE Posicao = 1", sql);
        Assert.Contains("ORDER BY TotalGrupo DESC, Grupo;", sql);
        Assert.DoesNotContain("Norte", sql); // o valor do filtro vai como parâmetro
        Assert.Equal("Norte", Aplicar(aplicar)["@filtro0"].Value);
    }

    [Fact]
    public void RankingPorDimensao_EmCadaMesPorQuantidade_OrdenaGruposNoTempo()
    {
        var sql = ConsultaVendas.RankingPorDimensao(DimensaoVenda.Localidade, Desde, Ate, porQuantidade: true, emCada: DimensaoVenda.Mes).Sql;

        Assert.Contains("ORDER BY COALESCE(SUM([Quantidade]), 0) DESC", sql);
        Assert.Contains("ORDER BY Grupo;", sql);
    }

    [Fact]
    public void RankingPorDimensao_Distrito_AgrupaPelaFichaSemPerderVendasSemFicha()
    {
        var (sql, _) = ConsultaVendas.RankingPorDimensao(DimensaoVenda.Distrito, Desde, Ate);

        Assert.Contains("OUTER APPLY (SELECT c.[Distrito] AS ValorFicha", sql); // não é JOIN: clientes sem ficha contam
        Assert.Contains("WHERE c.ClienteID = v.[ClienteID]", sql);
        Assert.Contains("GROUP BY ficha.ValorFicha COLLATE Latin1_General_CI_AI", sql);
        Assert.Contains("'(sem dados)')", sql); // o nome nunca chega NULL
        Assert.Contains("ORDER BY Valor DESC, Nome;", sql);
    }

    [Fact]
    public void RankingPorDimensao_PorQuantidade_OrdenaPelasUnidades()
        => Assert.Contains("ORDER BY Quantidade DESC, Nome;",
            ConsultaVendas.RankingPorDimensao(DimensaoVenda.Artigo, Desde, Ate, porQuantidade: true).Sql);

    [Fact]
    public void RankingPorDimensao_Menores_OrdenaDoMenorParaOMaior()
        => Assert.Contains("ORDER BY Valor ASC, Nome;",
            ConsultaVendas.RankingPorDimensao(DimensaoVenda.Localidade, Desde, Ate, menores: true).Sql);

    [Fact]
    public void RankingPorDimensao_MesPorQuantidade_ContinuaCronologico()
        => Assert.Contains("ORDER BY Nome;",
            ConsultaVendas.RankingPorDimensao(DimensaoVenda.Mes, Desde, Ate, porQuantidade: true).Sql);

    [Fact]
    public void Rankings_TrazemAUltimaDataDoPeriodo()
    {
        foreach (var (sql, _) in new[] { ConsultaVendas.RankingPorCliente(Desde, Ate), ConsultaVendas.RankingPorDimensao(DimensaoVenda.Marca, Desde, Ate) })
            Assert.Contains("MAX([DataDoc]) AS UltimaData", sql);
    }

    [Fact]
    public void ClientesSemVendas_SemFiltros_NaoExisteVendaNoPeriodo()
    {
        var (sql, aplicar) = ConsultaVendas.ClientesSemVendas(Desde, Ate);

        Assert.Contains("WHERE NOT EXISTS (", sql);
        Assert.Contains("v.[DataDoc] >= @desde AND v.[DataDoc] < @ate", sql);
        Assert.Contains("ORDER BY u.UltimaData DESC, Nome;", sql);
        Assert.False(Aplicar(aplicar).Contains("@cliente"));
    }

    [Fact]
    public void ClientesSemVendas_ComFiltro_JuntaComAnd()
    {
        var (sql, aplicar) = ConsultaVendas.ClientesSemVendas(Desde, Ate, new Dictionary<Campo, string> { [Campo.Localidade] = "Seixal" });

        Assert.Contains(" AND NOT EXISTS (", sql);
        Assert.Equal("Seixal", Aplicar(aplicar)["@filtro0"].Value);
    }

    [Fact]
    public void RankingPorDimensao_Marca_NaoVaiAFicha()
        => Assert.DoesNotContain("OUTER APPLY", ConsultaVendas.RankingPorDimensao(DimensaoVenda.Marca, Desde, Ate).Sql);
}

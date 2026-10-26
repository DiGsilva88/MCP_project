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

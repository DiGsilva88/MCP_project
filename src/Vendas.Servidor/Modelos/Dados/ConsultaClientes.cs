using System.Data;
using Microsoft.Data.SqlClient;

namespace Vendas.Servidor.Modelos.Dados;

// Constrói o SQL e os parâmetros das consultas a clientes.
// As colunas vêm sempre de Campos.Coluna (lista branca), nunca de texto do modelo.
internal static class ConsultaClientes
{
    private const string Origem =
        "[dbo].[ViewMCP_cliente] AS c JOIN [dbo].[ViewMCP_cliente_faturacao] AS f ON c.ClienteID = f.ClienteID";

    public static (string Sql, Action<SqlParameterCollection> Parametros) Contar(
        Campo agrupar, IReadOnlyDictionary<Campo, string> filtros)
    {
        var coluna = Campos.Coluna(agrupar);
        var (whereSql, aplicarFiltros) = FiltrosWhere(filtros);

        var sql = $"""
            SELECT TOP (@limite)
                   {coluna} AS Valor,
                   COUNT(*) AS Clientes,
                   SUM(COUNT(*)) OVER () AS Total,
                   COUNT(*) OVER () AS Grupos
            FROM   {Origem}
            {whereSql}
            GROUP BY {coluna}
            ORDER BY Clientes DESC, Valor;
            """;

        return (sql, aplicarFiltros);
    }

    public static (string Sql, Action<SqlParameterCollection> Parametros) Listar(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, string? nome, int deslocamento)
    {
        var colunas = string.Join(", ", mostrar.Select(Campos.Coluna));
        var (filtrosWhere, aplicarFiltros) = FiltrosWhere(filtros, prefixo: "AND ");

        var sql = $"""
            SELECT c.NomeCliente, {colunas},
                   COUNT(*) OVER () AS Total
            FROM   {Origem}
            WHERE  (@nome IS NULL OR c.NomeCliente LIKE @nome)
            {filtrosWhere}
            ORDER BY c.NomeCliente
            OFFSET @deslocamento ROWS FETCH NEXT @limite ROWS ONLY;
            """;

        void Parametros(SqlParameterCollection p)
        {
            p.Add("@nome", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(nome) ? DBNull.Value : $"%{nome.Trim()}%";
            p.Add("@deslocamento", SqlDbType.Int).Value = deslocamento;
            aplicarFiltros(p);
        }

        return (sql, Parametros);
    }

    // WHERE parametrizado a partir dos filtros (colunas de lista branca, valores como parâmetro).
    // valor "sem dados" não existe como texto na BD (a view guarda "(sem ...)" ou "0 - sem ...",
    // conforme a coluna) por isso vira NULL/vazio/marcador em vez de comparação exata.
    private static (string Sql, Action<SqlParameterCollection> Parametros) FiltrosWhere(
        IReadOnlyDictionary<Campo, string> filtros, string prefixo = "WHERE ")
    {
        if (filtros.Count == 0)
            return ("", _ => { });

        var pares = filtros.ToArray();
        var condicoes = pares.Select((par, i) => EhSemDados(par.Value)
            ? CondicaoSemDados(par.Key)
            : $"{Campos.Coluna(par.Key)} = @filtro{i}");
        var sql = prefixo + string.Join(" AND ", condicoes);

        void Parametros(SqlParameterCollection p)
        {
            for (var i = 0; i < pares.Length; i++)
                if (!EhSemDados(pares[i].Value))
                    p.Add($"@filtro{i}", SqlDbType.NVarChar, 100).Value = pares[i].Value;
        }

        return (sql, Parametros);
    }

    private static bool EhSemDados(string valor) =>
        valor.Equals(Campos.SemDados, StringComparison.OrdinalIgnoreCase);

    private static string CondicaoSemDados(Campo campo)
    {
        var coluna = Campos.Coluna(campo);
        return $"({coluna} IS NULL OR {coluna} = '' OR {coluna} LIKE '(sem %' OR {coluna} LIKE '0 - sem %')";
    }
}

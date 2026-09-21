using System.Data;
using Microsoft.Data.SqlClient;

namespace Vendas.Servidor.Modelos.Dados;

// Implementação real: lê as views MCP no SQL Server.
// "ligação" é a connection string que vem da variável de ambiente VENDAS_SQL
public sealed class RepoSql(string ligação) : IVendasRepo
{
    // view e colunas vêm sempre de Vistas, nunca do texto do modelo (impede SQL injection).
    public async Task<PaginaVista> ConsultarAsync(
        Vista vista, string? colunaFiltro, string? valor, int limite, CancellationToken cancellationToken = default)
    {
        // Com coluna: só NomeCliente + essa coluna. Sem coluna: todas.
        IReadOnlyList<string> colunas = colunaFiltro is null
            ? Vistas.Colunas(vista)
            : colunaFiltro == "NomeCliente" ? ["NomeCliente"] : ["NomeCliente", colunaFiltro];
        var total = 0;

        var linhas = await LerAsync($"""
            SELECT TOP (@limite) {string.Join(", ", colunas.Select(c => $"[{c}]"))},
                   COUNT(*) OVER () AS Total
            FROM   [dbo].[{Vistas.Nome(vista)}]
            WHERE  (@valor IS NULL OR [{colunaFiltro ?? "NomeCliente"}] = @valor)
            ORDER BY [NomeCliente];
            """,
            p =>
            {
                p.Add("@limite", SqlDbType.Int).Value = limite;
                // O SQL não aceita null de C#:tem de ser DBNull
                p.Add("@valor", SqlDbType.NVarChar, 100).Value =
                    colunaFiltro is null || string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();
            },
            l =>
            {
                total = l.GetInt32(colunas.Count); //O Total é igual em todas as linhas
                return colunas.Select((_, i) => Texto(l, i)).ToArray();
            },
            cancellationToken);

        return new PaginaVista(colunas, linhas, total);
    }

    public async Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        Vista vista, string coluna, int limite, CancellationToken cancellationToken = default)
        => await LerAsync($"""
            SELECT TOP (@limite)
                   [{coluna}]                  AS Valor,
                   COUNT(*)                    AS Clientes,
                   SUM(COUNT(*)) OVER ()       AS TOTAL,
                   COUNT(*) OVER ()            AS Grupos
            FROM   [dbo].[{Vistas.Nome(vista)}]
            GROUP BY [{coluna}]
            ORDER BY Clientes DESC, Valor;
            """,
            p => p.Add("@limite", SqlDbType.Int).Value = limite,
            l => new ContagemCliente(Texto(l, 0), l.GetInt32(1), l.GetInt32(2), l.GetInt32(3)),
            cancellationToken);

    // Faz o trabalho repetitivo, igual em todas as consultas.
    // "await using" fecha a ligação sozinho, mesmo que haja erro.
    private async Task<List<T>> LerAsync<T>(
        string sql,
        Action<SqlParameterCollection> parametros,
        Func<SqlDataReader, T> mapear,
        CancellationToken cancellationToken)
    {
        await using var ligacaoSql = new SqlConnection(ligação);
        await using var cmd = new SqlCommand(sql, ligacaoSql);
        parametros(cmd.Parameters);

        await ligacaoSql.OpenAsync(cancellationToken);
        await using var leitor = await cmd.ExecuteReaderAsync(cancellationToken);

        var linhas = new List<T>();
        while (await leitor.ReadAsync(cancellationToken))
            linhas.Add(mapear(leitor));

        return linhas;
    }

    // Lê qualquer coluna como texto. Sem valor devolve "sem dados".
    private static string Texto(SqlDataReader leitor, int i)
        => Vistas.Limpar(leitor.IsDBNull(i) ? null : Convert.ToString(leitor.GetValue(i)));
}

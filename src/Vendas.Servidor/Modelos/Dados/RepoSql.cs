using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Vendas.Servidor.Modelos.Dados;

// Implementação real: lê as views MCP no SQL Server.
// "ligação" é a connection string que vem da variável de ambiente VENDAS_SQL
public sealed class RepoSql(string ligacao) : IVendasRepo
{
    public async Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        Campo agrupar, IReadOnlyDictionary<Campo, string> filtros, int limite,
        CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaClientes.Contar(agrupar, filtros);

        return await LerAsync(sql, parametros, limite,
            leitor => new ContagemCliente(Texto(leitor, 0), leitor.GetInt32(1), leitor.GetInt32(2), leitor.GetInt32(3)),
            cancellationToken);
    }

    public async Task<PaginaClientes> ListarAsync(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, int deslocamento, int limite,
        CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaClientes.Listar(mostrar, filtros, deslocamento);
        var colunas = new[] { "NomeCliente" }.Concat(mostrar.Select(c => c.ToString())).ToArray();
        var total = 0;

        var linhas = await LerAsync(sql, parametros, limite,
            leitor =>
            {
                total = leitor.GetInt32(colunas.Length); // Total é igual em todas as linhas
                return colunas.Select((_, i) => Texto(leitor, i)).ToArray();
            },
            cancellationToken);

        return new PaginaClientes(colunas, linhas, total);
    }

    public async Task<PaginaClientes> ListarSensivelAsync(
        CampoSensivel mostrar, IReadOnlyDictionary<Campo, string> filtros, bool maiores, int deslocamento, int limite,
        CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaClientes.ListarSensivel(mostrar, filtros, maiores, deslocamento);
        var total = 0;

        var linhas = await LerAsync(sql, parametros, limite,
            leitor =>
            {
                total = leitor.GetInt32(2); // Total é igual em todas as linhas
                return new[] { Texto(leitor, 0), Texto(leitor, 1) };
            },
            cancellationToken);

        return new PaginaClientes(["NomeCliente", mostrar.ToString()], linhas, total);
    }

    // Faz o trabalho repetitivo, igual em todas as consultas.
    // "await using" fecha a ligação sozinha, mesmo que haja erro.
    private async Task<List<T>> LerAsync<T>(
        string sql,
        Action<SqlParameterCollection> parametros,
        int limite,
        Func<SqlDataReader, T> mapear,
        CancellationToken cancellationToken)
    {
        await using var ligacaoSql = new SqlConnection(ligacao);
        await using var cmd = new SqlCommand(sql, ligacaoSql);
        parametros(cmd.Parameters);
        cmd.Parameters.Add("@limite", SqlDbType.Int).Value = limite;

        await ligacaoSql.OpenAsync(cancellationToken);
        await using var leitor = await cmd.ExecuteReaderAsync(cancellationToken);

        var linhas = new List<T>();
        while (await leitor.ReadAsync(cancellationToken))
            linhas.Add(mapear(leitor));

        return linhas;
    }

    // Lê qualquer coluna como texto. Sem valor devolve "sem dados".
    private static string Texto(SqlDataReader leitor, int i)
        => Campos.Limpar(leitor.IsDBNull(i) ? null : Convert.ToString(leitor.GetValue(i), CultureInfo.InvariantCulture));
}

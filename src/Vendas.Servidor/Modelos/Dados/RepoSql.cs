
using Vendas.Servidor.Modelos;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace Vendas.Servidor.Dados;

public sealed class Repoql(string ligação)

{
    public async Task<IReadOnlyList<ContagemCliente>> 
    ContarPorAsync(
    DimensaoCliente dimensao, int limite, CancellationToken cancellationToken)
    {
        //lista em branco: o nome da coluna nunca vem do texto do modelo
        var coluna = Dimensoes.Coluna(dimensao);

        await using var ligacaoSql = new SqlConnection(ligação);
        await using var cmd = new SqlCommand($"""
            SELECT TOP (@limite) {coluna} AS Valor, COUNT(*) 
            AS Clientes
            FROM [dbo].[ViewMCP_cliente]
            GROUP BY {coluna}
            ORDER BY COUNT(*) DESC, {coluna};
            """, ligacaoSql);
            cmd.Parameters.AddWithValue("@limite", limite);

        await ligacaoSql.OpenAsync(cancellationToken);
        await using var leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        var linhas = new List<ContagemCliente>();
        while (await leitor. ReadAsync(cancellationToken))
        {
            linhas.Add(new ContagemCliente(
                leitor.IsDBNull(0) ? "(sem valor)" : leitor.GetString(0),
                leitor.GetInt32(1)));

        }
            return linhas;
        }
    }


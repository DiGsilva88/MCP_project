using Vendas.Servidor.Modelos;
using Microsoft.Data.SqlClient;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Dados;

public sealed class RepoSql(string ligação):IVendasRepo

{
    public Task<IReadOnlyList<ContagemCliente>> ContarClientesAsync(
        DimensaoCliente agrupar, int limite, CancellationToken cancellationToken = default)
        => ContarAsync("ViewMCP_cliente", Dimensoes.Coluna(agrupar), limite, cancellationToken);

    public Task<IReadOnlyList<ContagemCliente>> ContarFaturacaoAsync(
        DimensaoFaturacao agrupar, int limite, CancellationToken cancellationToken = default)
        => ContarAsync("ViewMCP_cliente_faturacao", Dimensoes.Coluna(agrupar), limite, cancellationToken);

    // view e coluna vêm sempre de constantes e do switch, nunca do texto do modelo
    private async Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        string view, string coluna, int limite, CancellationToken cancellationToken)
    {
        await using var ligacaoSql = new SqlConnection(ligação);
        await using var cmd = new SqlCommand($"""
            SELECT TOP (@limite)
                   [{coluna}]            AS Valor,
                   COUNT(*)              AS Clientes,
                   SUM(COUNT(*)) OVER () AS Total,
                   COUNT(*)      OVER () AS Grupos
            FROM   [dbo].[{view}]
            GROUP BY [{coluna}]
            ORDER BY Clientes DESC, Valor;
            """, ligacaoSql);
        cmd.Parameters.AddWithValue("@limite", limite);

        await ligacaoSql.OpenAsync(cancellationToken);
        await using var leitor = await cmd.ExecuteReaderAsync(cancellationToken);

        var linhas = new List<ContagemCliente>();
        while (await leitor.ReadAsync(cancellationToken))
            linhas.Add(new ContagemCliente(
                leitor.IsDBNull(0) ? "(sem valor)" : leitor.GetString(0).Trim(),
                leitor.GetInt32(1),
                leitor.GetInt32(2),
                leitor.GetInt32(3)));
        return linhas;
    }
    
    

    //Ainda não existe View para vendas no SQL server
    //quando existir trocar cada metodo por uma query real

    public Task<IReadOnlyList<VendaPorCliente>> ObterTopClientesAsync(
        int limite, int dias, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
            "ObterTopClientesAsync : ainda não existe/view de vendas configuradas no SQL server.");

    public Task<IReadOnlyList<VendaPorProduto>> ObterTopProdutosAsync(
        int limite, int dias, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
            "ObterTopProdutosAsync : ainda não existe/view de vendas configuradas no SQL server.");

    public Task<IReadOnlyList<ClienteInativo>> ObterInativosAsync(
        int dias, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
            "ObterInativosAsync : ainda não existe/view de vendas configuradas no SQL server.");

    public async Task<IReadOnlyList<string>> ObterNomesClientesAsync(
        int limite, CancellationToken cancellationToken = default)
    {
        
        await using var ligacaoSql = new SqlConnection(ligação);
        await using var cmd = new SqlCommand("""
            SELECT DISTINCT TOP(@limite) LTRIM(RTRIM([NomeCliente]))
            AS Nome
            FROM [dbo].[ViewMCP_cliente]
            WHERE NULLIF(LTRIM(RTRIM([NomeCliente])),'') IS NOT NULL
            ORDER BY Nome;
            """, ligacaoSql);
        cmd.Parameters.AddWithValue("@limite", limite);
          

        await ligacaoSql.OpenAsync(cancellationToken);
        await using var leitor = await cmd.ExecuteReaderAsync(cancellationToken);
//
        var nomes = new List<string>();
        while (await leitor.ReadAsync(cancellationToken))
            nomes.Add(leitor.GetString(0).Trim());
        return nomes;
    }
    }




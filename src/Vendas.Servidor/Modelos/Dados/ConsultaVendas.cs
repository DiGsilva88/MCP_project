using System.Data;
using Microsoft.Data.SqlClient;

namespace Vendas.Servidor.Modelos.Dados;


// O SQL das ferramentas de vendas. Mesmo molde da ConsultaClientes (também internal):
// cada método devolve o texto SQL e uma função que preenche os parâmetros.
// O @limite NÃO se preenche aqui: o RepoSql.LerAsync acrescenta-o sozinho.

internal static class ConsultaVendas

{
    // Nomes das views num só sítio. Todas as views do MCP vivem no schema mcp.
    private const string Vendas   = "[mcp].[ViewMCP_vendas]";
    private const string Clientes = "[mcp].[ViewMCP_cliente]";

    public static (string Sql, Action<SqlParameterCollection> Parametros) TopClientes(
        DateOnly desde, DateOnly ate)
    {
        //1o soma por ClienteId dentro da view de vendas (o CTE "por cliente");
        //2º vai buscar o nome a View_cliente o nome vive lá

        var sql = $"""
            WITH porCliente AS (
                SELECT v.[ClienteID],
                       SUM(v.[ValorLiquido])           AS Valor,
                       COUNT(DISTINCT v.[DocumentoID]) AS Documentos
                FROM   {Vendas} AS v
                WHERE  v.[DataDoc] >= @desde
                  AND  v.[DataDoc] <  @ate
                GROUP BY v.[ClienteID] 
                )
            SELECT TOP (@limite)
                COALESCE(c.[NomeCliente], '(cliente sem ficha)') AS Nome,
                p.Valor,
                CAST(0 decimal(18,2)) AS Quantidade,
                p.Documentos,
                SUM(p.Valor) OVER () AS Total,
                COUNT(*) OVER() AS Grupos
            FROM porCliente AS p
            LEFT JOIN {Clientes} AS c ON c.[ClienteID] = p.[ClienteID]
            ORDER BY p.Valor DESC, Nome;
            """;

            return(sql, parametros => PreencherPeriodo(paramentros, desde, ate));
         }
            public static (string Sql, Action<SqlParameterCollection> Parametros) TopArtigos(
            DimensaoArtigo dimensao,DateOnly desde, DateOnly ate)
    {
        var(nome, agrupar) = Colunas(dimensao);

        var sql = $"""
            SELECT TOP (@limite)
            {nome}  AS Nome,
                   SUM([ValorLiquido])              AS Valor,
                   SUM([Quantidade])                AS Quantidade,
                   COUNT(DISTINCT [DocumentoID])    AS Documentos,
                   SUM(SUM([ValorLiquido])) OVER () AS Total,
                   COUNT(*)                 OVER () AS Grupos
            FROM   {Vendas}
            WHERE  [DataDoc] >= @desde
              AND  [DataDoc] <  @ate
            GROUP BY {agrupar}
            ORDER BY Valor DESC, Nome;
            """;

        return (sql, parametros => PreencherPeriodo(parametros, desde, ate));

    }

    //O que se mostra e por onde se agrupa, para cada dimensão. Switch fechado:
    //o modelo escolhe um valor do enum, nunca escreve SQL.
private static(string Nome, string Agrupar) Colunas(DimensaoArtigo dimensao) => dimensao switch
{

        DimensaoArtigo.Artigo  => ("CONCAT([Artigo], ' (', [ArtigoID], ')')", "[ArtigoID], [Artigo]"),
        DimensaoArtigo.Familia => ("[Familia]", "[Familia]"),
        DimensaoArtigo.Marca   => ("[Marca]",   "[Marca]"),
        _ => throw new ArgumentOutOfRangeException(nameof(dimensao))

};

//dateOnly - DateTime a meia noite é o formato que o SqlCliente envia para uma coluna "date"
private static void PreencherPeriodo(SqlParameterCollection parametros,DateOnly desde, DateOnly ate)

    {
        
        parametros.Add("@desde", SqlDbType.Date).Value =desde.ToDateTime(TimeOnly.MinValue);
        parametros.Add("@ate", SqlDbType.Date).Value =ate.ToDateTime(TimeOnly.MinValue);
    }
}


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
        DateOnly desde, DateOnly ate, string? cliente = null)
    {
        // 1º soma por ClienteID dentro da view de vendas; 2º vai buscar o nome à ViewMCP_cliente.
        // O ID vai junto ao nome para o modelo poder cruzar com as outras consultas.
        var sql = $"""
            SELECT TOP (@limite)
                   CONCAT(COALESCE(c.[NomeCliente], '(cliente sem ficha)'), ' (', p.[ClienteID], ')') AS Nome,
                   p.Valor,
                   CAST(0 AS decimal(18,2)) AS Quantidade,
                   p.Documentos,
                   SUM(p.Valor) OVER () AS Total,
                   COUNT(*)     OVER () AS Grupos,
                   p.Bruto, p.Desconto, p.Iva
            FROM (
                SELECT [ClienteID],
                       COALESCE(SUM([ValorLiquido]), 0)  AS Valor,
                       COUNT(DISTINCT [DocumentoID])     AS Documentos,
                       COALESCE(SUM([ValorIliquido]), 0) AS Bruto,
                       COALESCE(SUM([ValorDesconto]), 0) AS Desconto,
                       COALESCE(SUM([ValorIva]), 0)      AS Iva
                FROM   {Vendas}
                WHERE  [DataDoc] >= @desde
                  AND  [DataDoc] <  @ate
                  {FiltroCliente(cliente)}
                GROUP BY [ClienteID]
            ) AS p
            LEFT JOIN {Clientes} AS c ON c.[ClienteID] = p.[ClienteID]
            ORDER BY p.Valor DESC, Nome;
            """;

        return (sql, parametros => Preencher(parametros, desde, ate, cliente));
    }

    public static (string Sql, Action<SqlParameterCollection> Parametros) TopArtigos(
        DimensaoVenda dimensao, DateOnly desde, DateOnly ate, string? cliente = null)
    {
        var (nome, agrupar) = Colunas(dimensao);
        // Meses por ordem cronológica (mais recente primeiro); o resto do maior para o menor valor
        var ordem = dimensao == DimensaoVenda.Mes ? "Nome DESC" : "Valor DESC, Nome";

        var sql = $"""
            SELECT TOP (@limite)
                   {nome} AS Nome,
                   COALESCE(SUM([ValorLiquido]), 0) AS Valor,
                   COALESCE(SUM([Quantidade]), 0)   AS Quantidade,
                   COUNT(DISTINCT [DocumentoID]) AS Documentos,
                   SUM(COALESCE(SUM([ValorLiquido]), 0)) OVER () AS Total,
                   COUNT(*)                              OVER () AS Grupos,
                   COALESCE(SUM([ValorIliquido]), 0) AS Bruto,
                   COALESCE(SUM([ValorDesconto]), 0) AS Desconto,
                   COALESCE(SUM([ValorIva]), 0)      AS Iva
            FROM   {Vendas}
            WHERE  [DataDoc] >= @desde
              AND  [DataDoc] <  @ate
              {FiltroCliente(cliente)}
            GROUP BY {agrupar}
            ORDER BY {ordem};
            """;

        return (sql, parametros => Preencher(parametros, desde, ate, cliente));
    }

    // Clientes com esse ID exato ou cujo nome começa pelo texto (sem acentos/maiúsculas); o ID exato vem primeiro.
    // A tool usa isto para saber se o cliente existe e se é só um, antes de somar vendas.
    // Os REPLACE escapam [ % _ para o texto do modelo nunca funcionar como wildcard (igual à ConsultaClientes).
    public static (string Sql, Action<SqlParameterCollection> Parametros) ProcurarClientes(string cliente)
    {
        var sql = $"""
            SELECT TOP (@limite)
                   CAST([ClienteID] AS nvarchar(50)) AS Id,
                   COALESCE([NomeCliente], '(sem nome)') AS Nome
            FROM   {Clientes}
            WHERE  CAST([ClienteID] AS nvarchar(50)) = @cliente
               OR  [NomeCliente] COLLATE Latin1_General_CI_AI LIKE
                   REPLACE(REPLACE(REPLACE(@cliente, '[', '[[]'), '%', '[%]'), '_', '[_]') + '%'
            ORDER BY CASE WHEN CAST([ClienteID] AS nvarchar(50)) = @cliente THEN 0 ELSE 1 END, [NomeCliente];
            """;

        return (sql, p => p.Add("@cliente", SqlDbType.NVarChar, 100).Value = cliente.Trim());
    }

    // Só as vendas de um cliente, pelo ID exato (a tool já o resolveu com ProcurarClientes).
    private static string FiltroCliente(string? cliente) =>
        string.IsNullOrWhiteSpace(cliente) ? "" : "AND CAST([ClienteID] AS nvarchar(50)) = @cliente";

    //O que se mostra e por onde se agrupa, para cada dimensão. Switch fechado:
    //o modelo escolhe um valor do enum, nunca escreve SQL.
    private static (string Nome, string Agrupar) Colunas(DimensaoVenda dimensao) => dimensao switch
    {
        DimensaoVenda.Artigo  => ("CONCAT([Artigo], ' (', [ArtigoID], ')')", "[ArtigoID], [Artigo]"),
        DimensaoVenda.Familia => ("[Familia]", "[Familia]"),
        DimensaoVenda.Marca   => ("[Marca]",   "[Marca]"),
        // estilo 120 = "aaaa-mm-dd": os 7 primeiros caracteres são o mês
        DimensaoVenda.Mes     => ("CONVERT(char(7), [DataDoc], 120)", "CONVERT(char(7), [DataDoc], 120)"),
        _ => throw new ArgumentOutOfRangeException(nameof(dimensao))
    };

    //DateOnly -> DateTime à meia-noite é o formato que o SqlClient envia para uma coluna "date"
    private static void Preencher(SqlParameterCollection parametros, DateOnly desde, DateOnly ate, string? cliente)
    {
        parametros.Add("@desde", SqlDbType.Date).Value = desde.ToDateTime(TimeOnly.MinValue);
        parametros.Add("@ate", SqlDbType.Date).Value = ate.ToDateTime(TimeOnly.MinValue);
        if (!string.IsNullOrWhiteSpace(cliente))
            parametros.Add("@cliente", SqlDbType.NVarChar, 100).Value = cliente.Trim();
    }
}

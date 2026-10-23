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

    public static (string Sql, Action<SqlParameterCollection> Parametros) RankingPorCliente(
        DateOnly desde, DateOnly ate, string? cliente = null, IReadOnlyDictionary<Campo, string>? filtros = null)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var onde = Onde(cliente, ficha);

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
                   p.Bruto, p.Desconto, p.Iva,
                   t.TotalDocumentos,
                   t.UltimaData
            FROM (
                SELECT [ClienteID],
                       COALESCE(SUM([ValorLiquido]), 0)  AS Valor,
                       COUNT(DISTINCT [DocumentoID])     AS Documentos,
                       COALESCE(SUM([ValorIliquido]), 0) AS Bruto,
                       COALESCE(SUM([ValorDesconto]), 0) AS Desconto,
                       COALESCE(SUM([ValorIva]), 0)      AS Iva
                FROM   {Vendas}
                {onde}
                GROUP BY [ClienteID]
            ) AS p
            {TotalDocumentos(onde)}
            LEFT JOIN {Clientes} AS c ON c.[ClienteID] = p.[ClienteID]
            ORDER BY p.Valor DESC, Nome;
            """;

        return (sql, p => { Preencher(p, desde, ate, cliente); aplicarFicha(p); });
    }

    public static (string Sql, Action<SqlParameterCollection> Parametros) RankingPorDimensao(
        DimensaoVenda dimensao, DateOnly desde, DateOnly ate, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, bool porQuantidade = false)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var onde = Onde(cliente, ficha);

        var (nome, agrupar) = Colunas(dimensao);
        // Meses por ordem cronológica (mais recente primeiro); o resto do maior para o menor valor (ou quantidade)
        var ordem = dimensao == DimensaoVenda.Mes ? "Nome DESC"
            : porQuantidade ? "Quantidade DESC, Nome"
            : "Valor DESC, Nome";

        // Dimensão da ficha (Localidade, Distrito...): cada linha de venda vai buscar esse valor ao seu cliente.
        // OUTER APPLY e não JOIN: vendas de clientes sem ficha continuam a contar no total.
        // Só expõe ValorFicha, por isso os [ClienteID] sem prefixo do WHERE continuam a ser os da venda.
        var aplicarFichaCliente = Dimensoes.CampoDaFicha(dimensao) is { } campo
            ? $"OUTER APPLY (SELECT {ConsultaClientes.Coluna(campo)} AS ValorFicha FROM {ConsultaClientes.Origem} WHERE c.ClienteID = v.[ClienteID]) AS ficha"
            : "";

        // MAX(t.TotalDocumentos): a query tem GROUP BY e o total é igual em todas as linhas
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
                   COALESCE(SUM([ValorIva]), 0)      AS Iva,
                   MAX(t.TotalDocumentos)            AS TotalDocumentos,
                   MAX(t.UltimaData)                 AS UltimaData
            FROM   {Vendas} AS v
            {TotalDocumentos(onde)}
            {aplicarFichaCliente}
            {onde}
            GROUP BY {agrupar}
            ORDER BY {ordem};
            """;

        return (sql, p => { Preencher(p, desde, ate, cliente); aplicarFicha(p); });
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

    // Período + cliente + filtro da ficha. É o mesmo WHERE no ranking e na contagem de documentos.
    private static string Onde(string? cliente, string ficha) =>
        $"WHERE [DataDoc] >= @desde AND [DataDoc] < @ate {FiltroCliente(cliente)} {ficha}";

    // Só clientes com essa Localidade/Distrito/Zona/... (mesmo filtro da tool consultar).
    private static (string Sql, Action<SqlParameterCollection> Parametros) FiltroFicha(
        IReadOnlyDictionary<Campo, string>? filtros)
    {
        if (filtros is not { Count: > 0 }) return ("", _ => { });
        var (where, aplicar) = ConsultaClientes.FiltrosWhere(filtros);
        return ($"AND [ClienteID] IN (SELECT c.ClienteID FROM {ConsultaClientes.Origem} {where})", aplicar);
    }

    // Clientes da ficha sem nenhuma venda em [desde, ate[, com a data da última compra antes disso
    // (NULL = nunca comprou). Os que deixaram de comprar há menos tempo vêm primeiro.
    public static (string Sql, Action<SqlParameterCollection> Parametros) ClientesSemVendas(
        DateOnly desde, DateOnly ate, IReadOnlyDictionary<Campo, string>? filtros = null)
    {
        var (where, aplicarFiltros) = ConsultaClientes.FiltrosWhere(filtros ?? new Dictionary<Campo, string>());
        var sql = $"""
            SELECT TOP (@limite)
                   CONCAT(COALESCE(c.[NomeCliente], '(sem nome)'), ' (', c.[ClienteID], ')') AS Nome,
                   u.UltimaData,
                   COUNT(*) OVER () AS Total
            FROM   {ConsultaClientes.Origem}
            OUTER APPLY (SELECT MAX(v.[DataDoc]) AS UltimaData FROM {Vendas} AS v
                         WHERE v.[ClienteID] = c.ClienteID AND v.[DataDoc] < @ate) AS u
            {(where.Length == 0 ? "WHERE" : where + " AND")} NOT EXISTS (
                SELECT 1 FROM {Vendas} AS v
                WHERE v.[ClienteID] = c.ClienteID AND v.[DataDoc] >= @desde AND v.[DataDoc] < @ate)
            ORDER BY u.UltimaData DESC, Nome;
            """;

        return (sql, p => { Preencher(p, desde, ate, null); aplicarFiltros(p); });
    }

    // Data da venda mais recente na view (sem período): diz até onde vão os dados quando um período vem vazio.
    public static (string Sql, Action<SqlParameterCollection> Parametros) UltimaVenda() =>
        ($"SELECT MAX([DataDoc]) FROM {Vendas};", _ => { });

    // Documentos distintos no período todo: um documento tem vários artigos, por isso não se somam os grupos.
    // UltimaData: a venda mais recente do período, para avisar quando os dados acabam antes do fim do período.
    private static string TotalDocumentos(string onde) =>
        $"CROSS JOIN (SELECT COUNT(DISTINCT [DocumentoID]) AS TotalDocumentos, MAX([DataDoc]) AS UltimaData FROM {Vendas} {onde}) AS t";

    //O que se mostra e por onde se agrupa, para cada dimensão. Switch fechado:
    //o modelo escolhe um valor do enum, nunca escreve SQL.
    private static (string Nome, string Agrupar) Colunas(DimensaoVenda dimensao) => dimensao switch
    {
        DimensaoVenda.Artigo  => ("CONCAT([Artigo], ' (', [ArtigoID], ')')", "[ArtigoID], [Artigo]"),
        DimensaoVenda.Familia => ("[Familia]", "[Familia]"),
        DimensaoVenda.Marca   => ("[Marca]",   "[Marca]"),
        // estilo 120 = "aaaa-mm-dd": os 7 primeiros caracteres são o mês
        DimensaoVenda.Mes     => ("CONVERT(char(7), [DataDoc], 120)", "CONVERT(char(7), [DataDoc], 120)"),
        // Agrupa sem acentos/maiúsculas como o ConsultaClientes.Contar ("Fernão Ferro" = "FERNAO FERRO").
        // Vazio ou cliente sem ficha -> "(sem dados)": o nome nunca chega NULL ao LerValorAgrupado.
        _ when Dimensoes.CampoDaFicha(dimensao) is not null =>
            ("COALESCE(MIN(NULLIF(ficha.ValorFicha, '')), '(sem dados)')", "ficha.ValorFicha COLLATE Latin1_General_CI_AI"),
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

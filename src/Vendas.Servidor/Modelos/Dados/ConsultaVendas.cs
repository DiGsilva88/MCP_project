using System.Data;
using Microsoft.Data.SqlClient;

namespace Vendas.Servidor.Modelos.Dados;


// O SQL das ferramentas de vendas. Mesmo molde da ConsultaClientes (também internal):
// cada método devolve o texto SQL e uma função que preenche os parâmetros.
// O @limite NÃO se preenche aqui: o RepoSql.LerAsync acrescenta-o sozinho.

internal static class ConsultaVendas

{
    // Nomes das views num só sítio. Todas as views do MCP vivem no schema mcp.
    private const string Vendas = "[mcp].[ViewMCP_vendas]";
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
        IReadOnlyDictionary<Campo, string>? filtros = null, bool porQuantidade = false, DimensaoVenda? emCada = null,
        bool menores = false)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var onde = Onde(cliente, ficha);

        var (nome, agrupar) = Colunas(dimensao);
        // Meses e trimestres por ordem cronológica (do mais antigo ao mais recente); o resto do maior para o menor
        // (menores: do menor para o maior, os que vendem menos)
        var sentido = menores ? "ASC" : "DESC";
        var ordem = Dimensoes.Cronologica(dimensao) ? "Nome"
            : porQuantidade ? $"Quantidade {sentido}, Nome"
            : $"Valor {sentido}, Nome";

        // Dimensão da ficha (Localidade, Distrito...): cada linha de venda vai buscar esse valor ao seu cliente.
        // OUTER APPLY e não JOIN: vendas de clientes sem ficha continuam a contar no total.
        // Só expõe ValorFicha, por isso os [ClienteID] sem prefixo do WHERE continuam a ser os da venda.
        // Com emCada, só uma das duas dimensões pode ser da ficha (a tool recusa as outras combinações).
        var aplicarFichaCliente = (Dimensoes.CampoDaFicha(dimensao) ?? (emCada is { } e ? Dimensoes.CampoDaFicha(e) : null)) is { } campo
            ? $"OUTER APPLY (SELECT {ConsultaClientes.Coluna(campo)} AS ValorFicha FROM {ConsultaClientes.Origem} WHERE c.ClienteID = v.[ClienteID]) AS ficha"
            : "";

        if (emCada is { } grupo)
            return (MelhorEmCada(dimensao, grupo, porQuantidade, onde, aplicarFichaCliente),
                    p => { Preencher(p, desde, ate, cliente); aplicarFicha(p); });

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

    // O 1.º "dimensao" em cada "grupo" (ex.: o artigo mais vendido em cada distrito): soma por (grupo, item),
    // numera dentro de cada grupo e fica só com o n.º 1. TotalGrupo dá a percentagem do item no seu grupo.
    // As colunas de fora têm a mesma ordem das do ranking normal (o LerValorAgrupado lê por posição).
    private static string MelhorEmCada(DimensaoVenda dimensao, DimensaoVenda grupo, bool porQuantidade, string onde, string aplicarFichaCliente)
    {
        var (nome, agrupar) = Colunas(dimensao);
        var (nomeGrupo, agruparGrupo) = Colunas(grupo);
        var medida = porQuantidade ? "[Quantidade]" : "[ValorLiquido]";
        var ordem = Dimensoes.Cronologica(grupo) ? "Grupo" : "TotalGrupo DESC, Grupo";

        return $"""
            SELECT TOP (@limite)
                   Nome, Valor, Quantidade, Documentos, Total,
                   COUNT(*) OVER () AS Grupos,
                   Bruto, Desconto, Iva, TotalDocumentos, UltimaData, TotalGrupo
            FROM (
                SELECT CONCAT({nomeGrupo}, ': ', {nome}) AS Nome,
                       {nomeGrupo} AS Grupo,
                       COALESCE(SUM([ValorLiquido]), 0) AS Valor,
                       COALESCE(SUM([Quantidade]), 0)   AS Quantidade,
                       COUNT(DISTINCT [DocumentoID]) AS Documentos,
                       SUM(COALESCE(SUM([ValorLiquido]), 0)) OVER () AS Total,
                       SUM(COALESCE(SUM([ValorLiquido]), 0)) OVER (PARTITION BY {agruparGrupo}) AS TotalGrupo,
                       ROW_NUMBER() OVER (PARTITION BY {agruparGrupo} ORDER BY COALESCE(SUM({medida}), 0) DESC, {nome}) AS Posicao,
                       COALESCE(SUM([ValorIliquido]), 0) AS Bruto,
                       COALESCE(SUM([ValorDesconto]), 0) AS Desconto,
                       COALESCE(SUM([ValorIva]), 0)      AS Iva,
                       MAX(t.TotalDocumentos)            AS TotalDocumentos,
                       MAX(t.UltimaData)                 AS UltimaData
                FROM   {Vendas} AS v
                {TotalDocumentos(onde)}
                {aplicarFichaCliente}
                {onde}
                GROUP BY {agruparGrupo}, {agrupar}
            ) AS r
            WHERE Posicao = 1
            ORDER BY {ordem};
            """;
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

    // Vista Faturação do painel: valor e n.º de documentos por tipo de documento (TipoDocID).
    public static (string Sql, Action<SqlParameterCollection> Parametros) DocumentosPorTipo(
        DateOnly desde, DateOnly ate, string? cliente, IReadOnlyDictionary<Campo, string>? filtros)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var sql = $"""
            SELECT [TipoDocID],
                   COALESCE(SUM([ValorLiquido]), 0) AS Valor,
                   COUNT(DISTINCT [DocumentoID])    AS Documentos
            FROM   {Vendas}
            {Onde(cliente, ficha)}
            GROUP BY [TipoDocID]
            ORDER BY Valor DESC;
            """;
        return (sql, p => { Preencher(p, desde, ate, cliente); aplicarFicha(p); });
    }

    // Valor e documentos de cada tipo em cada mês (DataDoc), do mais antigo ao mais recente.
    public static (string Sql, Action<SqlParameterCollection> Parametros) DocumentosPorMes(
        DateOnly desde, DateOnly ate, string? cliente, IReadOnlyDictionary<Campo, string>? filtros)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var sql = $"""
            SELECT CONVERT(char(7), [DataDoc], 120) AS Mes,
                   [TipoDocID],
                   COALESCE(SUM([ValorLiquido]), 0) AS Valor,
                   COUNT(DISTINCT [DocumentoID])    AS Documentos
            FROM   {Vendas}
            {Onde(cliente, ficha)}
            GROUP BY CONVERT(char(7), [DataDoc], 120), [TipoDocID]
            ORDER BY Mes;
            """;
        return (sql, p => { Preencher(p, desde, ate, cliente); aplicarFicha(p); });
    }

    // Os documentos mais recentes: uma linha por documento (a view tem uma por artigo).
    // Soma por dentro e só depois vai buscar o nome, como no RankingPorCliente: o WHERE usa [ClienteID] sem prefixo.
    // soCreditos: só notas de crédito (N) e créditos sobre faturas-recibo (C).
    public static (string Sql, Action<SqlParameterCollection> Parametros) UltimosDocumentos(
        DateOnly desde, DateOnly ate, string? cliente, IReadOnlyDictionary<Campo, string>? filtros, bool soCreditos = false)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        if (soCreditos) ficha += " AND [TipoDocID] IN ('N', 'C')";
        var sql = $"""
            SELECT TOP (@limite)
                   d.[DocumentoID], d.[TipoDocID], d.[DataDoc],
                   COALESCE(c.[NomeCliente], '(cliente sem ficha)') AS Cliente,
                   d.Total
            FROM (
                SELECT [DocumentoID], [TipoDocID], [DataDoc], [ClienteID],
                       COALESCE(SUM([ValorLiquido]), 0) AS Total
                FROM   {Vendas}
                {Onde(cliente, ficha)}
                GROUP BY [DocumentoID], [TipoDocID], [DataDoc], [ClienteID]
            ) AS d
            LEFT JOIN {Clientes} AS c ON c.[ClienteID] = d.[ClienteID]
            ORDER BY d.[DataDoc] DESC, d.[DocumentoID] DESC;
            """;
        return (sql, p => { Preencher(p, desde, ate, cliente); aplicarFicha(p); });
    }

    // Vista Clientes do painel: zona e última compra de sempre de cada cliente (a nota de crédito não é uma compra).
    // Uma só passagem pela view: um OUTER APPLY por cliente passava dos 30 s que o painel espera.
    public static string CarteiraClientes() => $"""
        SELECT CAST(u.[ClienteID] AS nvarchar(50)),
               COALESCE(c.[Zona], '(sem zona)'),
               u.Ultima
        FROM (
            SELECT [ClienteID], MAX(CASE WHEN [ValorLiquido] > 0 THEN [DataDoc] END) AS Ultima
            FROM   {Vendas}
            GROUP BY [ClienteID]
        ) AS u
        LEFT JOIN {Clientes} AS c ON c.[ClienteID] = u.[ClienteID];
        """;

    // Valor e documentos de cada cliente em cada mês do período. Um documento tem uma só data: somar os meses não repete nenhum.
    public static (string Sql, Action<SqlParameterCollection> Parametros) ComprasPorMes(
        DateOnly desde, DateOnly ate, IReadOnlyDictionary<Campo, string>? filtros)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var sql = $"""
            SELECT CAST([ClienteID] AS nvarchar(50)),
                   CONVERT(char(7), [DataDoc], 120),
                   COALESCE(SUM([ValorLiquido]), 0),
                   COUNT(DISTINCT [DocumentoID])
            FROM   {Vendas}
            {Onde(null, ficha)}
            GROUP BY [ClienteID], CONVERT(char(7), [DataDoc], 120);
            """;
        return (sql, p => { Preencher(p, desde, ate, null); aplicarFicha(p); });
    }

    // Data da venda mais recente na view (sem período): diz até onde vão os dados quando um período vem vazio.
    public static (string Sql, Action<SqlParameterCollection> Parametros) UltimaVenda() =>
        ($"SELECT MAX([DataDoc]) FROM {Vendas};", _ => { }
    );

    // Documentos distintos no período todo: um documento tem vários artigos, por isso não se somam os grupos.
    // UltimaData: a venda mais recente do período, para avisar quando os dados acabam antes do fim do período.
    private static string TotalDocumentos(string onde) =>
        $"CROSS JOIN (SELECT COUNT(DISTINCT [DocumentoID]) AS TotalDocumentos, MAX([DataDoc]) AS UltimaData FROM {Vendas} {onde}) AS t";

    //O que se mostra e por onde se agrupa, para cada dimensão. Switch fechado:
    //o modelo escolhe um valor do enum, nunca escreve SQL.
    private static (string Nome, string Agrupar) Colunas(DimensaoVenda dimensao) => dimensao switch
    {
        DimensaoVenda.Artigo => ("CONCAT([Artigo], ' (', [ArtigoID], ')')", "[ArtigoID], [Artigo]"),
        DimensaoVenda.Familia => ("[Familia]", "[Familia]"),
        DimensaoVenda.Marca => ("[Marca]", "[Marca]"),
        // estilo 120 = "aaaa-mm-dd": os 7 primeiros caracteres são o mês
        DimensaoVenda.Mes => ("CONVERT(char(7), [DataDoc], 120)", "CONVERT(char(7), [DataDoc], 120)"),
        // "2026-T1": ordena bem como texto, tal como o mês
        DimensaoVenda.Trimestre => ("CONCAT(YEAR([DataDoc]), '-T', DATEPART(QUARTER, [DataDoc]))",
                                    "YEAR([DataDoc]), DATEPART(QUARTER, [DataDoc])"),
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

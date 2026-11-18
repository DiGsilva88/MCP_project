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
    private const string EmAbertoView = "[mcp].[ViewMCP_resp_aberto_cli]";
    // Tipos de documento que a view de documentos por pagar traz com o saldo POSITIVO mas que são créditos (verificado na BD: as notas
    // de crédito "N" valem -46,12 nas vendas e aparecem como 46,11 aqui). Acrescentar o "R" (recibos, aplicação GTS) se se confirmar.
    private static readonly string[] TiposCredito = ["N"];

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
        bool menores = false, string? artigo = null)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var onde = Onde(cliente, ficha, artigo);

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
                    p => { Preencher(p, desde, ate, cliente, artigo); aplicarFicha(p); });

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

        return (sql, p => { Preencher(p, desde, ate, cliente, artigo); aplicarFicha(p); });
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
    // filtros: só procura entre esses clientes (ex.: o vendedor do perfil), para a lista "Vários clientes…" e o nome
    // devolvido nunca mostrarem clientes de outro vendedor.
    public static (string Sql, Action<SqlParameterCollection> Parametros) ProcurarClientes(
        string cliente, IReadOnlyDictionary<Campo, string>? filtros = null)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros, vendas: false);   // a ficha não tem Marca nem TipoDocID
        var sql = $"""
            SELECT TOP (@limite)
                   CAST([ClienteID] AS nvarchar(50)) AS Id,
                   COALESCE([NomeCliente], '(sem nome)') AS Nome
            FROM   {Clientes}
            WHERE  (CAST([ClienteID] AS nvarchar(50)) = @cliente
               OR   [NomeCliente] COLLATE Latin1_General_CI_AI LIKE
                    REPLACE(REPLACE(REPLACE(@cliente, '[', '[[]'), '%', '[%]'), '_', '[_]') + '%')
                   {ficha}
            ORDER BY CASE WHEN CAST([ClienteID] AS nvarchar(50)) = @cliente THEN 0 ELSE 1 END, [NomeCliente];
            """;

        return (sql, p => { p.Add("@cliente", SqlDbType.NVarChar, 100).Value = cliente.Trim(); aplicarFicha(p); });
    }

    // Só as vendas de um cliente, pelo ID exato (a tool já o resolveu com ProcurarClientes).
    private static string FiltroCliente(string? cliente) =>
        string.IsNullOrWhiteSpace(cliente) ? "" : "AND CAST([ClienteID] AS nvarchar(50)) = @cliente";

    // Só um artigo: pelo ID exato ou por parte do nome (sem acentos nem maiúsculas).
    // shortcut: se o nome apanha vários artigos, somam-se; separar só se for preciso.
    private static string FiltroArtigo(string? artigo) => string.IsNullOrWhiteSpace(artigo) ? "" :
        @"AND (CAST([ArtigoID] AS nvarchar(50)) = @artigo OR [Artigo] COLLATE Latin1_General_CI_AI LIKE @artigoPadrao ESCAPE '\')";

    // Período + cliente + artigo + filtro da ficha. É o mesmo WHERE no ranking e na contagem de documentos.
    private static string Onde(string? cliente, string ficha, string? artigo = null) =>
        $"WHERE [DataDoc] >= @desde AND [DataDoc] < @ate {FiltroCliente(cliente)} {FiltroArtigo(artigo)} {ficha}";

    // Só clientes com essa Localidade/Distrito/Zona/... (mesmo filtro da tool consultar).
    // Com FiltrosVenda (painel): também várias zonas e, nas consultas sobre as vendas (vendas=true), marcas e tipos de documento.
    private static (string Sql, Action<SqlParameterCollection> Parametros) FiltroFicha(
        IReadOnlyDictionary<Campo, string>? filtros, bool vendas = true)
    {
        var (sql, aplicar) = ("", (Action<SqlParameterCollection>)(_ => { }));
        if (filtros is { Count: > 0 })
        {
            var (where, a) = ConsultaClientes.FiltrosWhere(filtros);
            (sql, aplicar) = ($"AND [ClienteID] IN (SELECT c.ClienteID FROM {ConsultaClientes.Origem} {where})", a);
        }
        if (filtros is not FiltrosVenda { TemListas: true } f) return (sql, aplicar);

        var (zonas, aplicarZonas) = FiltroZonas(f, "[ClienteID]");
        sql += zonas;
        if (vendas && f.Marcas.Count > 0)
            sql += $" AND [Marca] COLLATE Latin1_General_CI_AI IN ({string.Join(", ", f.Marcas.Select((_, i) => $"@marca{i}"))})";
        if (vendas && f.Tipos.Count > 0)
            sql += $" AND [TipoDocID] IN ({string.Join(", ", f.Tipos.Select((_, i) => $"@tipodoc{i}"))})";
        return (sql, p =>
        {
            aplicar(p);
            aplicarZonas(p);
            if (!vendas) return;
            for (var i = 0; i < f.Marcas.Count; i++) p.Add($"@marca{i}", SqlDbType.NVarChar, FiltrosVenda.MaxTexto).Value = f.Marcas[i];
            for (var i = 0; i < f.Tipos.Count; i++) p.Add($"@tipodoc{i}", SqlDbType.NVarChar, 10).Value = f.Tipos[i];
        });
    }

    // Uma ou mais zonas (distritos da ficha). "sem dados" são os clientes sem distrito, como no filtro de um só valor.
    private static (string Sql, Action<SqlParameterCollection> Parametros) FiltroZonas(FiltrosVenda? f, string colunaCliente)
    {
        if (f is not { Zonas.Count: > 0 }) return ("", _ => { });
        var distrito = ConsultaClientes.Coluna(Campo.Distrito);
        var valores = f.Zonas.Where(z => !ConsultaClientes.EhSemDados(z)).ToList();
        var condicoes = new List<string>();
        if (valores.Count > 0) condicoes.Add($"{distrito} COLLATE Latin1_General_CI_AI IN ({string.Join(", ", valores.Select((_, i) => $"@zona{i}"))})");
        if (valores.Count < f.Zonas.Count) condicoes.Add(ConsultaClientes.CondicaoSemDados(Campo.Distrito));
        return ($" AND {colunaCliente} IN (SELECT c.ClienteID FROM {ConsultaClientes.Origem} WHERE {string.Join(" OR ", condicoes)})",
            p => { for (var i = 0; i < valores.Count; i++) p.Add($"@zona{i}", SqlDbType.NVarChar, FiltrosVenda.MaxTexto).Value = valores[i]; });
    }

    // Clientes da ficha sem nenhuma venda em [desde, ate[, com a data da última compra antes disso
    // (NULL = nunca comprou). Os que deixaram de comprar há menos tempo vêm primeiro.
    public static (string Sql, Action<SqlParameterCollection> Parametros) ClientesSemVendas(
        DateOnly desde, DateOnly ate, IReadOnlyDictionary<Campo, string>? filtros = null)
    {
        var (where, aplicarFiltros) = ConsultaClientes.FiltrosWhere(filtros ?? new Dictionary<Campo, string>());
        var (zonas, aplicarZonas) = FiltroZonas(filtros as FiltrosVenda, "c.ClienteID");
        if (zonas.Length > 0) where = (where.Length == 0 ? "WHERE 1 = 1" : where) + zonas;
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

        return (sql, p => { Preencher(p, desde, ate, null); aplicarFiltros(p); aplicarZonas(p); });
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

    // Um documento N ou C por linha, com o nome do cliente e a pesquisa. Termina nos CTE: quem chama acrescenta o SELECT.
    private static string CreditosBase(string? cliente, string ficha, bool comPesquisa) => $"""
        WITH d AS (
            SELECT [DocumentoID], [TipoDocID], [DataDoc], [ClienteID],
                   COALESCE(SUM([ValorLiquido]), 0) AS Total
            FROM   {Vendas}
            {Onde(cliente, ficha + " AND [TipoDocID] IN ('N', 'C')")}
            GROUP BY [DocumentoID], [TipoDocID], [DataDoc], [ClienteID]
        ), f AS (
            SELECT d.[DocumentoID], d.[TipoDocID], d.[DataDoc], d.[ClienteID], d.Total,
                   COALESCE(c.[NomeCliente], '(cliente sem ficha)') AS Cliente
            FROM   d
            LEFT JOIN {Clientes} AS c ON c.[ClienteID] = d.[ClienteID]
            {(comPesquisa ? @"WHERE d.[DocumentoID] COLLATE Latin1_General_CI_AI LIKE @pesquisa ESCAPE '\'
               OR c.[NomeCliente] COLLATE Latin1_General_CI_AI LIKE @pesquisa ESCAPE '\'" : "")}
        )
        """;

    // A pesquisa é texto livre: %, _ e [ valem por si, não como padrões do LIKE.
    private static string PadraoPesquisa(string pesquisa) =>
        "%" + pesquisa.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[") + "%";

    private static void PreencherCreditos(SqlParameterCollection p, DateOnly desde, DateOnly ate, string? cliente, string? pesquisa)
    {
        Preencher(p, desde, ate, cliente);
        if (!string.IsNullOrWhiteSpace(pesquisa))
            p.Add("@pesquisa", SqlDbType.NVarChar, 210).Value = PadraoPesquisa(pesquisa);
    }

    public static (string Sql, Action<SqlParameterCollection> Parametros) PaginaCreditos(
        DateOnly desde, DateOnly ate, string? tipo, string? pesquisa, int deslocamento, string? cliente, IReadOnlyDictionary<Campo, string>? filtros)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var sql = $"""
            {CreditosBase(cliente, ficha, !string.IsNullOrWhiteSpace(pesquisa))}
            SELECT [DocumentoID], [TipoDocID], [DataDoc], CAST([ClienteID] AS nvarchar(50)), Cliente, Total
            FROM   f
            {(tipo is null ? "" : "WHERE [TipoDocID] = @tipo")}
            ORDER BY [DataDoc] DESC, [DocumentoID] DESC
            OFFSET @deslocamento ROWS FETCH NEXT @limite ROWS ONLY;
            """;
        return (sql, p =>
        {
            PreencherCreditos(p, desde, ate, cliente, pesquisa);
            aplicarFicha(p);
            p.Add("@deslocamento", SqlDbType.Int).Value = deslocamento;
            if (tipo is not null) p.Add("@tipo", SqlDbType.NVarChar, 20).Value = tipo;
        });
    }

    // À parte da página: com um tipo sem resultados, os contadores do outro continuam certos.
    public static (string Sql, Action<SqlParameterCollection> Parametros) ContagemCreditos(
        DateOnly desde, DateOnly ate, string? pesquisa, string? cliente, IReadOnlyDictionary<Campo, string>? filtros)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var sql = $"""
            {CreditosBase(cliente, ficha, !string.IsNullOrWhiteSpace(pesquisa))}
            SELECT COUNT(CASE WHEN [TipoDocID] = 'N' THEN 1 END), COUNT(CASE WHEN [TipoDocID] = 'C' THEN 1 END)
            FROM   f;
            """;
        return (sql, p => { PreencherCreditos(p, desde, ate, cliente, pesquisa); aplicarFicha(p); });
    }

    // As linhas de um documento. O DocumentoID é calculado na view: filtrar antes por ano e tipo evita comparar o texto em toda a tabela.
    public static (string Sql, Action<SqlParameterCollection> Parametros) DetalheDocumento(
        string documento, string tipo, int ano, IReadOnlyDictionary<Campo, string>? filtros)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var sql = $"""
            SELECT v.[DocumentoID], v.[TipoDocID], v.[DataDoc], CAST(v.[ClienteID] AS nvarchar(50)),
                   COALESCE(c.[NomeCliente], '(cliente sem ficha)'),
                   v.[ArtigoID], v.[Artigo],
                   COALESCE(v.[Quantidade], 0), COALESCE(v.[ValorIliquido], 0), COALESCE(v.[ValorDesconto], 0),
                   COALESCE(v.[ValorLiquido], 0), COALESCE(v.[ValorIva], 0)
            FROM (
                SELECT * FROM {Vendas}
                {Onde(null, ficha + " AND [TipoDocID] = @tipo AND [DocumentoID] = @documento")}
            ) AS v
            LEFT JOIN {Clientes} AS c ON c.[ClienteID] = v.[ClienteID]
            ORDER BY v.[ArtigoID];
            """;
        return (sql, p =>
        {
            Preencher(p, new DateOnly(ano, 1, 1), new DateOnly(ano + 1, 1, 1), null);
            aplicarFicha(p);
            p.Add("@tipo", SqlDbType.NVarChar, 20).Value = tipo;
            p.Add("@documento", SqlDbType.NVarChar, 80).Value = documento;
        });
    }

    // Vista Clientes do painel: zona e última compra de sempre de cada cliente (a nota de crédito não é uma compra).
    // Uma só passagem pela view: um OUTER APPLY por cliente passava dos 30 s que o painel espera.
    // filtros: os mesmos das compras por mês. Um vendedor só lê os seus clientes, não o histórico de todos.
    public static (string Sql, Action<SqlParameterCollection> Parametros) CarteiraClientes(IReadOnlyDictionary<Campo, string>? filtros)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var sql = $"""
            SELECT CAST(u.[ClienteID] AS nvarchar(50)),
                   COALESCE(c.[Zona], '(sem zona)'),
                   u.Ultima
            FROM (
                SELECT [ClienteID], MAX(CASE WHEN [ValorLiquido] > 0 THEN [DataDoc] END) AS Ultima
                FROM   {Vendas}
                WHERE  1 = 1 {ficha}
                GROUP BY [ClienteID]
            ) AS u
            LEFT JOIN {Clientes} AS c ON c.[ClienteID] = u.[ClienteID];
            """;
        return (sql, aplicarFicha);
    }

    // Briefing do painel: o que um cliente comprou em [desde, ate[, por artigo (quantidade, em quantos meses, última compra, valor).
    // As notas de crédito não contam como compra. filtros: o vendedor do perfil só vê os seus clientes.
    public static (string Sql, Action<SqlParameterCollection> Parametros) ArtigosDoCliente(
        string cliente, DateOnly desde, DateOnly ate, IReadOnlyDictionary<Campo, string>? filtros)
    {
        var (ficha, aplicarFicha) = FiltroFicha(filtros);
        var sql = $"""
            SELECT TOP (@limite)
                   CAST([ArtigoID] AS nvarchar(50)), COALESCE(MAX([Artigo]), '(sem descrição)'),
                   COALESCE(SUM([Quantidade]), 0),
                   COUNT(DISTINCT CONVERT(char(7), [DataDoc], 120)),
                   MAX([DataDoc]),
                   COALESCE(SUM([ValorLiquido]), 0)
            FROM   {Vendas}
            WHERE  CAST([ClienteID] AS nvarchar(50)) = @cliente
              AND  [DataDoc] >= @desde AND [DataDoc] < @ate
              AND  [ValorLiquido] > 0 {ficha}
            GROUP BY [ArtigoID]
            ORDER BY SUM([ValorLiquido]) DESC;
            """;
        return (sql, p =>
        {
            p.Add("@cliente", SqlDbType.NVarChar, 100).Value = cliente.Trim();
            p.Add("@desde", SqlDbType.Date).Value = desde.ToDateTime(TimeOnly.MinValue);
            p.Add("@ate", SqlDbType.Date).Value = ate.ToDateTime(TimeOnly.MinValue);
            aplicarFicha(p);
        });
    }

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
    // Documentos por pagar. EntidadeID é o ClienteID da ficha (comparados como texto, como em ProcurarClientes).
    // Contagem, total e vencido com OVER (): contam todos os documentos, não só o TOP.
    public static (string Sql, Action<SqlParameterCollection> Parametros) EmAberto(
        DateOnly hoje, string? cliente = null, bool soVencidos = false)
    {
        // Os totais (Documentos, Total, Vencido) são só das faturas por pagar (saldo positivo). As notas de crédito por abater
        // vão à parte (DocumentosCredito, Creditos, negativo): somadas às faturas inflacionavam o que os clientes devem.
        var credito = string.Join(", ", TiposCredito.Select(t => $"'{t}'"));
        var sql = $"""
            SELECT TOP (@limite)
                   CONCAT(COALESCE(c.[NomeCliente], '(cliente sem ficha)'), ' (', r.[EntidadeID], ')') AS Cliente,
                   CONCAT(RTRIM(r.[TipoDocID]), ' ', r.[Ano], '/', r.[DocID]) AS Documento,
                   r.[DataEmissao], r.[DataVencimento], r.Valor,
                   COUNT(CASE WHEN r.Valor > 0 THEN 1 END) OVER () AS Documentos,
                   SUM(CASE WHEN r.Valor > 0 THEN r.Valor ELSE 0 END) OVER () AS Total,
                   SUM(CASE WHEN r.Valor > 0 AND r.[DataVencimento] < @hoje THEN r.Valor ELSE 0 END) OVER () AS Vencido,
                   COUNT(CASE WHEN r.Valor < 0 THEN 1 END) OVER () AS DocumentosCredito,
                   SUM(CASE WHEN r.Valor < 0 THEN r.Valor ELSE 0 END) OVER () AS Creditos
            FROM (SELECT *, CASE WHEN RTRIM([TipoDocID]) IN ({credito}) THEN -1 ELSE 1 END
                            * CAST(COALESCE([RemanMoedaAdop], 0) AS decimal(18,2)) AS Valor FROM {EmAbertoView}) AS r
            LEFT JOIN {Clientes} AS c ON CAST(c.[ClienteID] AS nvarchar(50)) = CAST(r.[EntidadeID] AS nvarchar(50))
            WHERE 1 = 1
              {(string.IsNullOrWhiteSpace(cliente) ? "" : "AND CAST(r.[EntidadeID] AS nvarchar(50)) = @cliente")}
              {(soVencidos ? "AND r.Valor > 0 AND r.[DataVencimento] < @hoje" : "")}
            ORDER BY r.[DataVencimento], Documento;
            """;

        return (sql, p =>
        {
            p.Add("@hoje", SqlDbType.Date).Value = hoje.ToDateTime(TimeOnly.MinValue);
            if (!string.IsNullOrWhiteSpace(cliente)) p.Add("@cliente", SqlDbType.NVarChar, 100).Value = cliente.Trim();
        });
    }

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
    private static void Preencher(SqlParameterCollection parametros, DateOnly desde, DateOnly ate, string? cliente, string? artigo = null)
    {
        parametros.Add("@desde", SqlDbType.Date).Value = desde.ToDateTime(TimeOnly.MinValue);
        parametros.Add("@ate", SqlDbType.Date).Value = ate.ToDateTime(TimeOnly.MinValue);
        if (!string.IsNullOrWhiteSpace(cliente))
            parametros.Add("@cliente", SqlDbType.NVarChar, 100).Value = cliente.Trim();
        if (!string.IsNullOrWhiteSpace(artigo))
        {
            parametros.Add("@artigo", SqlDbType.NVarChar, 100).Value = artigo.Trim();
            parametros.Add("@artigoPadrao", SqlDbType.NVarChar, 210).Value = PadraoPesquisa(artigo);
        }
    }
}

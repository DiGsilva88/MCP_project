using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Vendas.Servidor.Modelos.Dados;

// Implementação real: lê as views MCP no SQL Server.
// "ligação" é a connection string que vem da variável de ambiente VENDAS_SQL
public sealed class RepoSql(string ligacao) : IVendasRepo
{
    // shortcut: a exportação de créditos do painel leva no máximo 5000; a lista já pagina no SQL. Aumentar se a exportação ficar curta.
    private const int MaxCreditos = 5000;

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
        string? nome = null, CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaClientes.Listar(mostrar, filtros, deslocamento, nome);
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
        string? nome = null, Campo? tambem = null, CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaClientes.ListarSensivel(mostrar, filtros, maiores, deslocamento, nome, tambem);
        var colunas = tambem is { } t ? new[] { "NomeCliente", mostrar.ToString(), t.ToString() } : ["NomeCliente", mostrar.ToString()];
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

    public async Task<IReadOnlyList<(string Id, string Nome)>> ProcurarClientesAsync(
        string cliente, int limite, CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaVendas.ProcurarClientes(cliente);
        return await LerAsync(sql, parametros, limite, leitor => (leitor.GetString(0), leitor.GetString(1)), cancellationToken);
    }

    public async Task<IReadOnlyList<ValorAgrupado>> RankingPorClienteAsync(
        DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaVendas.RankingPorCliente(desde, ate, cliente, filtros);
        return await LerAsync(sql, parametros, limite, LerValorAgrupado, cancellationToken);
    }

    public async Task<IReadOnlyList<ValorAgrupado>> RankingPorDimensaoAsync(
        DimensaoVenda dimensao, DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, bool porQuantidade = false,
        DimensaoVenda? emCada = null, bool menores = false, CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaVendas.RankingPorDimensao(dimensao, desde, ate, cliente, filtros, porQuantidade, emCada, menores);
        return await LerAsync(sql, parametros, limite, LerValorAgrupado, cancellationToken);
    }

    public async Task<IReadOnlyList<ClienteSemVendas>> ClientesSemVendasAsync(
        DateOnly desde, DateOnly ate, int limite, IReadOnlyDictionary<Campo, string>? filtros = null,
        CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaVendas.ClientesSemVendas(desde, ate, filtros);
        return await LerAsync(sql, parametros, limite,
            leitor => new ClienteSemVendas(leitor.GetString(0), Data(leitor, 1), leitor.GetInt32(2)),
            cancellationToken);
    }

    public async Task<ResumoDocumentos> ResumoDocumentosAsync(
        DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default)
    {
        var (sqlTipos, parametrosTipos) = ConsultaVendas.DocumentosPorTipo(desde, ate, cliente, filtros);
        var (sqlMeses, parametrosMeses) = ConsultaVendas.DocumentosPorMes(desde, ate, cliente, filtros);
        var (sqlUltimos, parametrosUltimos) = ConsultaVendas.UltimosDocumentos(desde, ate, cliente, filtros);

        var tipos = LerAsync(sqlTipos, parametrosTipos, limite,
            leitor => new DocumentosPorTipo(leitor.GetString(0), leitor.GetDecimal(1), leitor.GetInt32(2)), cancellationToken);
        var meses = LerAsync(sqlMeses, parametrosMeses, limite,
            leitor => new DocumentosPorMes(leitor.GetString(0), leitor.GetString(1), leitor.GetDecimal(2), leitor.GetInt32(3)), cancellationToken);
        static DocumentoEmitido Documento(SqlDataReader leitor) =>
            new(leitor.GetString(0), leitor.GetString(1), Data(leitor, 2)!.Value, leitor.GetString(3), leitor.GetDecimal(4));
        var ultimos = LerAsync(sqlUltimos, parametrosUltimos, limite, Documento, cancellationToken);
        var (sqlCreditos, parametrosCreditos) = ConsultaVendas.UltimosDocumentos(desde, ate, cliente, filtros, soCreditos: true);
        var creditos = LerAsync(sqlCreditos, parametrosCreditos, MaxCreditos, Documento, cancellationToken);
        await Task.WhenAll(tipos, meses, ultimos, creditos);

        return new ResumoDocumentos(tipos.Result, meses.Result, ultimos.Result, creditos.Result);
    }

    public async Task<PaginaCreditos> ListarCreditosAsync(
        DateOnly desde, DateOnly ate, string? tipo, string? pesquisa, int deslocamento, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default)
    {
        var (sqlPagina, parametrosPagina) = ConsultaVendas.PaginaCreditos(desde, ate, tipo, pesquisa, deslocamento, cliente, filtros);
        var (sqlContagem, parametrosContagem) = ConsultaVendas.ContagemCreditos(desde, ate, pesquisa, cliente, filtros);

        var itens = LerAsync(sqlPagina, parametrosPagina, limite,
            leitor => new CreditoListado(leitor.GetString(0), leitor.GetString(1), Data(leitor, 2)!.Value, leitor.GetString(3), leitor.GetString(4), leitor.GetDecimal(5)),
            cancellationToken);
        var contagem = LerAsync(sqlContagem, parametrosContagem, 0, leitor => (N: leitor.GetInt32(0), C: leitor.GetInt32(1)), cancellationToken);
        await Task.WhenAll(itens, contagem);

        return new PaginaCreditos(itens.Result, contagem.Result[0].N, contagem.Result[0].C);
    }

    public async Task<DetalheDocumento?> DetalheDocumentoAsync(
        string documento, string tipo, int ano, IReadOnlyDictionary<Campo, string>? filtros = null,
        CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaVendas.DetalheDocumento(documento, tipo, ano, filtros);
        var linhas = await LerAsync(sql, parametros, 0, leitor => (
            Cabecalho: new DetalheDocumento(leitor.GetString(0), leitor.GetString(1), Data(leitor, 2)!.Value, leitor.GetString(3), leitor.GetString(4), []),
            Linha: new LinhaDocumento(leitor.GetString(5), leitor.GetString(6), leitor.GetDecimal(7), leitor.GetDecimal(8), leitor.GetDecimal(9), leitor.GetDecimal(10), leitor.GetDecimal(11))),
            cancellationToken);

        return linhas.Count == 0 ? null : linhas[0].Cabecalho with { Linhas = linhas.Select(l => l.Linha).ToList() };
    }

    public async Task<ResumoCarteira> ResumoCarteiraAsync(
        DateOnly desde, DateOnly ate, IReadOnlyDictionary<Campo, string>? filtros = null,
        CancellationToken cancellationToken = default)
    {
        var (sqlMeses, parametrosMeses) = ConsultaVendas.ComprasPorMes(desde, ate, filtros);

        // limite 0: estas consultas não têm TOP
        var clientes = LerAsync(ConsultaVendas.CarteiraClientes(), _ => { }, 0,
            leitor => new ClienteCarteira(leitor.GetString(0), leitor.GetString(1), Data(leitor, 2)), cancellationToken);
        var meses = LerAsync(sqlMeses, parametrosMeses, 0,
            leitor => new CompraMensal(leitor.GetString(0), leitor.GetString(1), leitor.GetDecimal(2), leitor.GetInt32(3)), cancellationToken);
        await Task.WhenAll(clientes, meses);

        return new ResumoCarteira(clientes.Result, meses.Result);
    }

    public async Task<DateOnly?> UltimaVendaAsync(CancellationToken cancellationToken = default)
    {
        var (sql, parametros) = ConsultaVendas.UltimaVenda();
        return (await LerAsync(sql, parametros, 1, leitor => Data(leitor, 0), cancellationToken)).FirstOrDefault();
    }

    // DataDoc pode ser date ou datetime na view: Convert aceita os dois.
    private static DateOnly? Data(SqlDataReader leitor, int i)
        => leitor.IsDBNull(i) ? null : DateOnly.FromDateTime(Convert.ToDateTime(leitor.GetValue(i), CultureInfo.InvariantCulture));

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


    // Uma linha do SQL → um ValorAgrupado. A ordem das colunas é a do SELECT.
    // O nome lê-se diretamente, sem o Texto(...): o Campos.Limpar trocaria "(sem marca)",
    // "(sem família)" e "(cliente sem ficha)" por "sem dados", e o modelo perdia a
    // explicação. O COALESCE do SQL já garante que o nome nunca chega a NULL.
    private static ValorAgrupado LerValorAgrupado(SqlDataReader leitor) => new(
        Nome: leitor.GetString(0),
        Valor: leitor.GetDecimal(1),
        Quantidade: leitor.GetDecimal(2),
        Documentos: leitor.GetInt32(3),
        Total: leitor.GetDecimal(4),
        Grupos: leitor.GetInt32(5),
        Bruto: leitor.GetDecimal(6),
        Desconto: leitor.GetDecimal(7),
        Iva: leitor.GetDecimal(8),
        TotalDocumentos: leitor.GetInt32(9),
        UltimaData: Data(leitor, 10),
        TotalGrupo: leitor.FieldCount > 11 ? leitor.GetDecimal(11) : 0); // só o ranking com emCada traz esta coluna

}

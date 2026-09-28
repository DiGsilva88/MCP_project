using System.Data;
using Microsoft.Data.SqlClient;

namespace Vendas.Servidor.Modelos.Dados;

// Constrói o SQL e os parâmetros das consultas a clientes.
// As colunas vêm sempre de Coluna() (lista branca, definida aqui), nunca de texto do modelo.
internal static class ConsultaClientes
{
    private const string Origem =
        "[dbo].[ViewMCP_cliente] AS c JOIN [dbo].[ViewMCP_cliente_faturacao] AS f ON c.ClienteID = f.ClienteID";

    public static (string Sql, Action<SqlParameterCollection> Parametros) Contar(
        Campo agrupar, IReadOnlyDictionary<Campo, string> filtros)
    {
        var coluna = Coluna(agrupar);
        var (whereSql, aplicarFiltros) = FiltrosWhere(filtros);

        var sql = $"""
            SELECT TOP (@limite)
                   {coluna} AS Valor,
                   COUNT(*) AS Clientes,
                   SUM(COUNT(*)) OVER () AS Total,
                   COUNT(*) OVER () AS Grupos
            FROM   {Origem}
            {whereSql}
            GROUP BY {coluna}
            ORDER BY Clientes DESC, Valor;
            """;

        return (sql, aplicarFiltros);
    }

    public static (string Sql, Action<SqlParameterCollection> Parametros) Listar(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, int deslocamento)
    {
        var colunas = string.Join(", ", mostrar.Select(Coluna));
        var (whereSql, aplicarFiltros) = FiltrosWhere(filtros);

        var sql = $"""
            SELECT c.NomeCliente, {colunas},
                   COUNT(*) OVER () AS Total
            FROM   {Origem}
            {whereSql}
            ORDER BY c.NomeCliente
            OFFSET @deslocamento ROWS FETCH NEXT @limite ROWS ONLY;
            """;

        void Parametros(SqlParameterCollection p)
        {
            p.Add("@deslocamento", SqlDbType.Int).Value = deslocamento;
            aplicarFiltros(p);
        }

        return (sql, Parametros);
    }

    // Tradução de Campo para coluna SQL (lista branca). Só esta camada conhece os aliases
    // c./f. do JOIN em Origem — o enum Campo, no modelo de domínio, não sabe disto.
    private static string Coluna(Campo campo) => campo switch
    {
        Campo.Zona => "c.[Zona]",
        Campo.Vendedor => "c.[Vendedor]",
        Campo.TipoCliente => "c.[TipoCliente]",
        Campo.Actividade => "c.[Actividade]",
        Campo.Distrito => "c.[Distrito]",
        Campo.Pagamento => "f.[Pagamento]",
        Campo.Cobranca => "f.[Cobranca]",
        Campo.Expedicao => "f.[Expedicao]",
        Campo.SituacaoFinanceira => "f.[SitFinanceira]",
        Campo.EscalaoPlafond => "f.[EscalaoPlafond]",
        Campo.EscalaoVolumeVendas => "f.[EscalaoVolumeVendas]",
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, null)
    };

    // WHERE parametrizado a partir dos filtros (colunas de lista branca, valores como parâmetro).
    // valor "sem dados" não existe como texto na BD (a view guarda o marcador da coluna: "(sem ...)"
    // ou "0 - sem ...") por isso vira NULL/vazio/marcador em vez de comparação exata.
    private static (string Sql, Action<SqlParameterCollection> Parametros) FiltrosWhere(
        IReadOnlyDictionary<Campo, string> filtros)
    {
        if (filtros.Count == 0)
            return ("", _ => { });

        var pares = filtros.ToArray();
        var condicoes = pares.Select((par, i) => EhSemDados(par.Value)
            ? CondicaoSemDados(par.Key)
            // COLLATE accent/case-insensitive: o modelo (e o utilizador) escreve "Setubal", a BD guarda "Setúbal".
            : $"{Coluna(par.Key)} COLLATE Latin1_General_CI_AI = @filtro{i}");
        var sql = "WHERE " + string.Join(" AND ", condicoes);

        void Parametros(SqlParameterCollection p)
        {
            for (var i = 0; i < pares.Length; i++)
                if (!EhSemDados(pares[i].Value))
                    p.Add($"@filtro{i}", SqlDbType.NVarChar, 100).Value = pares[i].Value;
        }

        return (sql, Parametros);
    }

    private static bool EhSemDados(string valor) =>
        valor.Equals(Campos.SemDados, StringComparison.OrdinalIgnoreCase);

    private static string CondicaoSemDados(Campo campo)
    {
        var coluna = Coluna(campo);
        return $"({coluna} IS NULL OR {coluna} = '' OR {coluna} LIKE '{Campos.PrefixoSemTexto}%' OR {coluna} LIKE '{Campos.PrefixoSemNumero}%')";
    }
}

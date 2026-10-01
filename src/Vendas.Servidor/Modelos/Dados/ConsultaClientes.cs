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
            ORDER BY c.NomeCliente, c.ClienteID
            OFFSET @deslocamento ROWS FETCH NEXT @limite ROWS ONLY;
            """;

        void Parametros(SqlParameterCollection p)
        {
            p.Add("@deslocamento", SqlDbType.Int).Value = deslocamento;
            aplicarFiltros(p);
        }

        return (sql, Parametros);
    }

    // Como Listar, mas com uma coluna da view sensível. "maiores" ordena do maior para o menor
    // (rankings); só faz sentido nas colunas numéricas, quem chama já o validou.
    public static (string Sql, Action<SqlParameterCollection> Parametros) ListarSensivel(
        CampoSensivel mostrar, IReadOnlyDictionary<Campo, string> filtros, bool maiores, int deslocamento)
    {
        var coluna = ColunaSensivel(mostrar);
        var (whereSql, aplicarFiltros) = FiltrosWhere(filtros);

        var sql = $"""
            SELECT c.NomeCliente, {coluna},
                   COUNT(*) OVER () AS Total
            FROM   {Origem}
            JOIN   [dbo].[ViewMCP_cliente_sensivel] AS s ON s.ClienteID = c.ClienteID
            {whereSql}
            ORDER BY {(maiores ? coluna + " DESC, " : "")}c.NomeCliente, c.ClienteID
            OFFSET @deslocamento ROWS FETCH NEXT @limite ROWS ONLY;
            """;

        void Parametros(SqlParameterCollection p)
        {
            p.Add("@deslocamento", SqlDbType.Int).Value = deslocamento;
            aplicarFiltros(p);
        }

        return (sql, Parametros);
    }

    private static string ColunaSensivel(CampoSensivel campo) => campo switch
    {
        CampoSensivel.Contribuinte => "s.[ContribuinteID]",
        CampoSensivel.Email => "s.[Email]",
        CampoSensivel.Telefone => "s.[Telefone]",
        CampoSensivel.Morada => "s.[Endereco]",
        CampoSensivel.CodigoPostal => "s.[PostalID]",
        CampoSensivel.VolumeVendas => "s.[VolumeVendas]",
        CampoSensivel.Plafond => "s.[Plafond]",
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, null)
    };

    // Tradução de Campo para coluna SQL (lista branca). Só esta camada conhece os aliases
    // c./f. do JOIN em Origem — o enum Campo, no modelo de domínio, não sabe disto.
    private static string Coluna(Campo campo) => campo switch
    {
        Campo.Zona => "c.[Zona]",
        Campo.Localidade => "c.[Localidade]",
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
            // Também aceita o início do valor por palavras inteiras: "30 dias" apanha "30 Dias Fim do Mês".
            // Os REPLACE escapam [ % _ para o valor do modelo nunca funcionar como wildcard.
            : $"({Coluna(par.Key)} COLLATE Latin1_General_CI_AI = @filtro{i} " +
              $"OR {Coluna(par.Key)} COLLATE Latin1_General_CI_AI LIKE " +
              $"REPLACE(REPLACE(REPLACE(@filtro{i}, '[', '[[]'), '%', '[%]'), '_', '[_]') + ' %')");
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

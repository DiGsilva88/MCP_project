namespace Vendas.Servidor.Modelos;

// As views que o MCP pode ler. Para expor uma nova view: novo valor aqui + entrada em Vistas.
public enum Vista
{
    Clientes,
    Faturacao,
}

// Colunas selecionáveis de cada view (aparecem como lista fechada no schema da tool).
// Os nomes têm de coincidir com as colunas em Vistas.
public enum ColunaCliente
{
    ClienteID, NomeCliente, Zona, Vendedor, TipoCliente, Actividade, Distrito,
}

public enum ColunaFaturacao
{
    ClienteID, NomeCliente, Pagamento, Cobranca, Expedicao, SitFinanceira, EscalaoPlafond, EscalaoVolumeVendas,
}

public record ContagemCliente(string Valor, int Clientes, int Total, int Grupos);

// Linhas de uma view (todas as colunas, como texto) + total que cumpre o filtro.
public sealed record PaginaVista(IReadOnlyList<string> Colunas, IReadOnlyList<string[]> Linhas, int Total);

public static class Vistas
{
    // Nome da view e colunas permitidas: única fonte do que entra no SQL (nunca texto do modelo).
    private static readonly Dictionary<Vista, (string Nome, string[] Colunas)> Todas = new()
    {
        [Vista.Clientes] = ("ViewMCP_cliente",
            ["ClienteID", "NomeCliente", "Zona", "Vendedor", "TipoCliente", "Actividade", "Distrito"]),
        [Vista.Faturacao] = ("ViewMCP_cliente_faturacao",
            ["ClienteID", "NomeCliente", "Pagamento", "Cobranca", "Expedicao", "SitFinanceira",
             "EscalaoPlafond", "EscalaoVolumeVendas"]),
    };

    public const string SemDados = "sem dados";

    // NULL, vazio e os marcadores das views ("(sem distrito)", "0 - sem plafond", ...) -> "sem dados".
    public static string Limpar(string? valor)
    {
        var v = valor?.Trim();
        return string.IsNullOrEmpty(v) || v.StartsWith("(sem ", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("0 - sem ", StringComparison.OrdinalIgnoreCase) ? SemDados : v;
    }

    public static string Nome(Vista vista) => Todas[vista].Nome;

    public static IReadOnlyList<string> Colunas(Vista vista) => Todas[vista].Colunas;

}

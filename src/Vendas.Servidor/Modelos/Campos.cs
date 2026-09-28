namespace Vendas.Servidor.Modelos;

// Nome das colunas permitidas e normalização de valores "sem dados".
// A tradução de Campo para SQL (aliases c./f. do JOIN) vive em ConsultaClientes, na camada de
// Dados: este modelo não conhece detalhes de tabelas/junções.
public static class Campos
{
    public const string SemDados = "sem dados";

    // Marcadores de "sem valor" que a view devolve, por tipo de coluna (texto vs. escalão numérico).
    // Partilhados com ConsultaClientes.CondicaoSemDados, que filtra por eles.
    public const string PrefixoSemTexto = "(sem ";
    public const string PrefixoSemNumero = "0 - sem ";

    // NULL, vazio e os marcadores das views ("(sem distrito)", "0 - sem plafond", ...) -> "sem dados".
    public static string Limpar(string? valor)
    {
        var v = valor?.Trim();
        return string.IsNullOrEmpty(v) || v.StartsWith(PrefixoSemTexto, StringComparison.OrdinalIgnoreCase)
            || v.StartsWith(PrefixoSemNumero, StringComparison.OrdinalIgnoreCase) ? SemDados : v;
    }

}

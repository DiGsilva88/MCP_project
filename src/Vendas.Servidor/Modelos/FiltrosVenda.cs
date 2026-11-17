namespace Vendas.Servidor.Modelos;

// Filtros do painel que não cabem num só valor da ficha: várias zonas (distritos), marcas e tipos de documento.
// É também o dicionário dos filtros da ficha (Campo -> valor): passa por todos os métodos do repositório sem mudar
// nenhuma assinatura, e só o SQL das vendas (ConsultaVendas.FiltroFicha) olha para as listas.
public sealed class FiltrosVenda : Dictionary<Campo, string>
{
    public const int MaxValores = 30, MaxTexto = 100;

    public IReadOnlyList<string> Zonas { get; }
    public IReadOnlyList<string> Marcas { get; }
    public IReadOnlyList<string> Tipos { get; }

    private FiltrosVenda(IReadOnlyList<string> zonas, IReadOnlyList<string> marcas, IReadOnlyList<string> tipos) =>
        (Zonas, Marcas, Tipos) = (zonas, marcas, tipos);

    public bool TemListas => Zonas.Count > 0 || Marcas.Count > 0 || Tipos.Count > 0;

    // null + erro quando as listas não são aceitáveis (valores a mais, texto comprido, tipo com caracteres estranhos).
    public static (FiltrosVenda? Filtros, string? Erro) Criar(string[]? zonas, string[]? marcas, string[]? tipos)
    {
        static List<string> Limpar(string[]? v) => (v ?? []).Select(x => x?.Trim() ?? "").Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var (z, m, t) = (Limpar(zonas), Limpar(marcas), Limpar(tipos));
        if (new[] { z, m, t }.Any(l => l.Count > MaxValores || l.Any(x => x.Length > MaxTexto)))
            return (null, $"Os filtros têm no máximo {MaxValores} valores de {MaxTexto} caracteres.");
        if (t.Any(x => !x.All(char.IsLetterOrDigit) || x.Length > 10))
            return (null, "Tipo de documento inválido.");
        return (new FiltrosVenda(z, m, t), null);
    }
}

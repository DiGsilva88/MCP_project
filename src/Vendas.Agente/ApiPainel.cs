using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace Vendas.Agente;

public sealed record RespostaApi(int Estado, string Corpo, string Tipo = "application/json; charset=utf-8");

// As rotas /api/ do painel, separadas do HttpListener para se poderem testar sem rede.
// O perfil vem do servidor em cada pedido: o vendedor nunca é um parâmetro que o browser possa escolher.
public sealed class ApiPainel(
    Func<string, Dictionary<string, object?>, CancellationToken, Task<string>> ferramenta,
    Action<Perfil, string> auditar)
{
    private const int MaxTexto = 100;
    private static readonly TimeSpan ValidadeCreditos = TimeSpan.FromSeconds(60);
    // Os períodos que o painel aceita (também no /resumo do Program.cs).
    internal static readonly string[] Periodos =["Ultimos7Dias", "MesAtual", "TrimestreAtual", "AnoAtual", "Ultimos12Meses", "AnoAnterior"];

    private static readonly Regex IdDocumento =
        new("^([A-Za-z0-9]{1,10})-([A-Za-z0-9]{1,10})-([0-9]{4})-([0-9]{1,12})$", RegexOptions.CultureInvariant);

    private static RespostaApi Mensagem(int estado, string texto) =>
        new(estado, System.Text.Json.JsonSerializer.Serialize(new { mensagem = texto }));

    private static readonly RespostaApi Indisponivel = Mensagem(502, "Não foi possível consultar os dados. Tente de novo; se continuar, avise a equipa técnica.");
    private static readonly RespostaApi NaoEncontrado = Mensagem(404, "Documento não encontrado.");
    private static readonly RespostaApi PedidoInvalido = Mensagem(400, "Pedido inválido.");

    // Página e contadores: a lista repete muitas vezes o mesmo pedido ao voltar atrás e ao mudar de filtro.
    private readonly Dictionary<string, (DateTime Feito, string Json)> creditos = [];

    // "N-10-2026-1034675" (URL e hash) -> "N 10/2026/1034675" (DocumentoID). O número sozinho repete-se entre anos e secções.
    public static string? Documento(string id)
    {
        var m = IdDocumento.Match(id);
        return m.Success ? $"{m.Groups[1].Value} {m.Groups[2].Value}/{m.Groups[3].Value}/{m.Groups[4].Value}" : null;
    }

    public static string LinhaAuditoria(DateTimeOffset agora, string login, string documento) =>
        $"{agora:yyyy-MM-dd HH:mm:ss}\t{login}\t{documento}";

    public async Task<RespostaApi> TratarAsync(string caminho, NameValueCollection q, Perfil perfil, CancellationToken ct = default)
    {
        try
        {
            return caminho.Split('/', StringSplitOptions.RemoveEmptyEntries) switch
            {
                ["api", "creditos"] => await CreditosAsync(q, perfil, ct),
                ["api", "documentos", var id] => await DocumentoAsync(id, perfil, ct),
                // shortcut: não se sabe onde o VB guarda o PDF, por isso é sempre 404 e a página usa a pré-visualização. Ligar aqui quando se souber.
                ["api", "documentos", _, "pdf"] => Mensagem(404, "O PDF emitido não está disponível."),
                ["api", "clientes", var id] => await ClienteAsync(Uri.UnescapeDataString(id), perfil, ct),
                _ => Mensagem(404, "Rota inexistente."),
            };
        }
        catch (Exception ex) // inclui o limite de tempo e o servidor MCP em baixo
        {
            Console.Error.WriteLine($"Falha na rota {caminho}: {ex.Message}");
            return Indisponivel;
        }
    }

    private async Task<RespostaApi> CreditosAsync(NameValueCollection q, Perfil perfil, CancellationToken ct)
    {
        var tipo = q["tipo"];
        if (tipo is not (null or "N" or "C")) return PedidoInvalido;
        var (pesquisa, cliente, zona) = (q["q"]?.Trim(), q["cliente"]?.Trim(), q["zona"]?.Trim());
        if (pesquisa?.Length > MaxTexto || cliente?.Length > MaxTexto || zona?.Length > MaxTexto) return PedidoInvalido;

        var argumentos = new Dictionary<string, object?>
        {
            ["periodo"] = Periodos.Contains(q["periodo"]) ? q["periodo"] : "AnoAtual",
            ["pagina"] = int.TryParse(q["page"], out var pagina) && pagina > 0 ? pagina : 1,
        };
        if (tipo is not null) argumentos["tipo"] = tipo;
        if (!string.IsNullOrEmpty(pesquisa)) argumentos["pesquisa"] = pesquisa;
        if (!string.IsNullOrEmpty(cliente)) argumentos["cliente"] = cliente;
        if (!string.IsNullOrEmpty(zona)) { argumentos["coluna"] = "Distrito"; argumentos["valor"] = zona; }
        if (perfil.Vendedor is not null) argumentos["vendedor"] = perfil.Vendedor;

        var chave = string.Join('|', argumentos.Select(a => $"{a.Key}={a.Value}"));
        lock (creditos)
        {
            foreach (var velha in creditos.Where(c => DateTime.UtcNow - c.Value.Feito > ValidadeCreditos).Select(c => c.Key).ToList())
                creditos.Remove(velha);
            if (creditos.TryGetValue(chave, out var guardado)) return new(200, guardado.Json);
        }

        var texto = await ferramenta("consultar_creditos", argumentos, ct);
        if (!texto.StartsWith('{')) return Indisponivel; // erro não fica guardado: o pedido seguinte tenta de novo
        lock (creditos) creditos[chave] = (DateTime.UtcNow, texto);
        return new(200, texto);
    }

    private async Task<RespostaApi> DocumentoAsync(string id, Perfil perfil, CancellationToken ct)
    {
        if (Documento(id) is not { } documento) return NaoEncontrado;

        var argumentos = new Dictionary<string, object?> { ["documento"] = documento };
        if (perfil.Vendedor is not null) argumentos["vendedor"] = perfil.Vendedor;

        var texto = await ferramenta("consultar_documento", argumentos, ct);
        if (texto.StartsWith("""{"erro":""")) return NaoEncontrado;
        if (!texto.StartsWith('{')) return Indisponivel;
        auditar(perfil, documento); // só o que foi mesmo aberto
        return new(200, texto);
    }

    private async Task<RespostaApi> ClienteAsync(string id, Perfil perfil, CancellationToken ct)
    {
        if (id.Trim() is not { Length: > 0 and <= MaxTexto } cliente) return PedidoInvalido;

        var argumentos = new Dictionary<string, object?> { ["cliente"] = cliente };
        if (perfil.Vendedor is { } vendedor) { argumentos["cruzarCom"] = "Vendedor"; argumentos["valorCruzado"] = vendedor; }

        var texto = await ferramenta("consultar", argumentos, ct);
        // Num perfil de vendedor, só a ficha em si: a mensagem de "vários clientes" lista nomes de clientes de outros.
        if (perfil.Vendedor is not null && !(texto.StartsWith("NomeCliente,") && texto.Split('\n', StringSplitOptions.RemoveEmptyEntries).Count(l => !l.StartsWith('#')) > 1))
            return Mensagem(404, "Cliente não encontrado.");
        return new(200, texto, "text/plain; charset=utf-8");
    }
}

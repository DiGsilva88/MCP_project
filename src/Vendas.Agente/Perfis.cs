using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vendas.Agente;

// Quem faz o pedido. Vendedor null = vê todos os clientes.
public sealed record Perfil(string Login, string? Vendedor);

// perfis.json: { "DOMINIO\\joao": "João Silva", "DOMINIO\\maria": "*" }. O valor é o Vendedor da ficha; "*" vê tudo.
public static class Perfis
{
    public const string VeTudo = "*";

    // Sem ficheiro devolve null: só entra quem arrancou o agente (ver Resolver). Um ficheiro inválido impede o arranque.
    public static IReadOnlyDictionary<string, string>? Ler(string ficheiro)
    {
        if (!File.Exists(ficheiro)) return null;
        var lidos = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ficheiro))
            ?? throw new InvalidDataException("perfis.json está vazio.");
        return new Dictionary<string, string>(lidos, StringComparer.OrdinalIgnoreCase);
    }

    // login: o utilizador Windows autenticado no pedido (null = não autenticado). dono: quem arrancou o agente.
    // Sem perfis.json só o dono entra, e vê tudo: noutra sessão do mesmo PC (RDS) o localhost também responde.
    // Com o ficheiro, quem não está nele não entra. null = sem acesso.
    public static Perfil? Resolver(string? login, IReadOnlyDictionary<string, string>? mapa, string dono)
    {
        if (string.IsNullOrWhiteSpace(login)) return null;
        if (mapa is null) return login.Equals(dono, StringComparison.OrdinalIgnoreCase) ? new Perfil(login, null) : null;
        return !mapa.TryGetValue(login, out var vendedor) || string.IsNullOrWhiteSpace(vendedor) ? null
            : new Perfil(login, vendedor == VeTudo ? null : vendedor);
    }

    // Atrás do IIS o login chega num cabeçalho. Só vale vindo do proxy local que conhece o segredo: o resto é falsificável.
    public static string? LoginDoProxy(string? cabecalhoUtilizador, string? cabecalhoSegredo, string? segredo, bool loopback)
    {
        if (!loopback || string.IsNullOrWhiteSpace(segredo) || string.IsNullOrWhiteSpace(cabecalhoUtilizador) || cabecalhoSegredo is null) return null;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cabecalhoSegredo), Encoding.UTF8.GetBytes(segredo))
            ? cabecalhoUtilizador.Trim() : null;
    }

    // Entrada pelo Visual (demonstração, sem Windows Auth): o VB6 assina "login|validade" com HMAC-SHA256 (segredo em texto, saída em hex minúsculo).
    // shortcut: o link é reutilizável até expirar (poucos minutos); um nonce só se justifica se o link vier a circular fora do Visual.
    public static string? LoginValido(string? login, string? validade, string? assinatura, string? segredo, DateTimeOffset agora, TimeSpan validadeMax)
    {
        if (string.IsNullOrWhiteSpace(segredo) || string.IsNullOrWhiteSpace(login) || assinatura is null || !long.TryParse(validade, out var exp)) return null;
        var t = agora.ToUnixTimeSeconds();
        if (exp < t || exp > t + (long)validadeMax.TotalSeconds) return null;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(assinatura), Encoding.UTF8.GetBytes(Assinar(login, exp, segredo))) ? login : null;
    }

    static string Assinar(string login, long exp, string segredo) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes($"{login}|{exp}")));

    // Cookie de sessão que o agente emite depois do link: login escapado, validade e assinatura (o ':' nunca aparece no login escapado).
    public static string CriarSessao(string login, DateTimeOffset validade, string segredo)
    {
        var exp = validade.ToUnixTimeSeconds();
        return $"{Uri.EscapeDataString(login)}:{exp}:{Assinar(login, exp, segredo)}";
    }

    public static string? LoginDaSessao(string? cookie, string? segredo, DateTimeOffset agora, TimeSpan validadeMax)
    {
        var p = cookie?.Split(':');
        return p is { Length: 3 } ? LoginValido(Uri.UnescapeDataString(p[0]), p[1], p[2], segredo, agora, validadeMax) : null;
    }
}

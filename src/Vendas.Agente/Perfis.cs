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
}

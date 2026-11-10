using System.Text.Json;

namespace Vendas.Agente;

// Quem faz o pedido. Vendedor null = vê todos os clientes.
public sealed record Perfil(string Login, string? Vendedor);

// perfis.json: { "DOMINIO\\joao": "João Silva", "DOMINIO\\maria": "*" }. O valor é o Vendedor da ficha; "*" vê tudo.
public static class Perfis
{
    public const string VeTudo = "*";

    // Sem ficheiro fica vazio, ou seja, ninguém entra. Um ficheiro inválido impede o arranque: nunca abre sem filtros.
    public static IReadOnlyDictionary<string, string> Ler(string ficheiro)
    {
        if (!File.Exists(ficheiro)) return new Dictionary<string, string>();
        var lidos = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ficheiro))
            ?? throw new InvalidDataException("perfis.json está vazio.");
        return new Dictionary<string, string>(lidos, StringComparer.OrdinalIgnoreCase);
    }

    // null = sem acesso: quem não está no ficheiro não entra.
    public static Perfil? Resolver(string login, IReadOnlyDictionary<string, string> mapa) =>
        !mapa.TryGetValue(login, out var vendedor) || string.IsNullOrWhiteSpace(vendedor) ? null
        : new Perfil(login, vendedor == VeTudo ? null : vendedor);
}

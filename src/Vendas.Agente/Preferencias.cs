using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vendas.Agente;

// O que cada pessoa guarda no painel (Guardados, alertas personalizados, filtros): um ficheiro por login, fora da BD do ERP.
// Em RDS corre uma só instância para todos: o ficheiro é escolhido pelo login autenticado no servidor, nunca pelo browser.
public static class Preferencias
{
    public const int MaxCaracteres = 256 * 1024;
    // Só estas chaves: a página não pode usar o ficheiro para guardar outra coisa.
    private static readonly HashSet<string> Chaves = ["guardados", "alertas", "filtros"];
    private static readonly object Bloqueio = new();

    // Hash e não o login tal e qual: "DOMINIO\\ana" não é um nome de ficheiro, e assim não há ..\ possível.
    public static string Ficheiro(string pasta, string login) =>
        Path.Combine(pasta, "utilizadores", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(login.ToLowerInvariant())))[..32] + ".json");

    public static string Ler(string ficheiro)
    {
        lock (Bloqueio) return File.Exists(ficheiro) ? File.ReadAllText(ficheiro) : "{}";
    }

    // null = aceite; senão o motivo. Um objeto JSON só com as chaves conhecidas, cada uma lista ou objeto.
    public static string? Validar(string corpo)
    {
        if (corpo.Length > MaxCaracteres) return "Demasiados dados guardados.";
        try
        {
            using var d = JsonDocument.Parse(corpo);
            if (d.RootElement.ValueKind != JsonValueKind.Object) return "Pedido inválido.";
            foreach (var p in d.RootElement.EnumerateObject())
                if (!Chaves.Contains(p.Name) || p.Value.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object)) return "Pedido inválido.";
            return null;
        }
        catch (JsonException) { return "Pedido inválido."; }
    }

    public static void Guardar(string ficheiro, string corpo)
    {
        lock (Bloqueio)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ficheiro)!);
            File.WriteAllText(ficheiro, corpo);
        }
    }
}

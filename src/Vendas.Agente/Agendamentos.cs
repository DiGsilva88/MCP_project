using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace Vendas.Agente;

public record Agendamento(string Freq, string Email);

// Resumo por email à segunda: a página guarda o pedido; a tarefa agendada "Vendas.Agente --enviar-resumo" envia (ver README).
public static class Agendamentos
{
    // Os dados de vendas só saem para endereços da empresa.
    public const string Dominio = "inforap.pt";

    // Pedido da página ({"freq":"semanal","email":"..."}): devolve o agendamento ou o erro a mostrar.
    public static (Agendamento? Agendamento, string? Erro) DoPedido(string corpo)
    {
        Agendamento? a;
        try { a = JsonSerializer.Deserialize<Agendamento>(corpo, JsonSerializerOptions.Web); }
        catch (JsonException) { return (null, "Pedido inválido."); }

        if (a?.Freq is not ("semanal" or "mensal")) return (null, "Escolha semanal ou mensal.");
        var email = a.Email?.Trim() ?? "";
        // Address == email recusa "Nome <x@inforap.pt>" e listas: um só endereço, escrito tal e qual.
        if (!MailAddress.TryCreate(email, out var endereco) || endereco.Address != email
            || !endereco.Host.Equals(Dominio, StringComparison.OrdinalIgnoreCase))
            return (null, $"Só endereços @{Dominio}.");
        return (a with { Email = email }, null);
    }

    // Semanal: todas as segundas. Mensal: só na 1.ª segunda do mês.
    public static bool Devido(string freq, DateOnly hoje)
        => hoje.DayOfWeek == DayOfWeek.Monday && (freq == "semanal" || hoje.Day <= 7);

    // [desde, ate[: semanal = a semana anterior (segunda a domingo); mensal = o mês anterior.
    public static (DateOnly Desde, DateOnly Ate) Janela(string freq, DateOnly hoje)
    {
        if (freq == "mensal")
        {
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            return (inicioMes.AddMonths(-1), inicioMes);
        }
        var segunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));
        return (segunda.AddDays(-7), segunda);
    }

    public static List<Agendamento> Ler(string ficheiro)
        => File.Exists(ficheiro) ? JsonSerializer.Deserialize<List<Agendamento>>(File.ReadAllText(ficheiro)) ?? [] : [];

    // Um agendamento por endereço: escolher de novo troca a frequência.
    public static void Guardar(string ficheiro, Agendamento novo)
    {
        var lista = Ler(ficheiro);
        lista.RemoveAll(a => a.Email.Equals(novo.Email, StringComparison.OrdinalIgnoreCase));
        lista.Add(novo);
        Directory.CreateDirectory(Path.GetDirectoryName(ficheiro)!);
        File.WriteAllText(ficheiro, JsonSerializer.Serialize(lista));
    }

    // Os textos das ferramentas tal como vêm (já são legíveis), codificados: os nomes de clientes podem ter < ou &.
    public static string CorpoHtml(string titulo, IEnumerable<(string Secao, string Texto)> partes)
    {
        var html = new StringBuilder($"<h2>{WebUtility.HtmlEncode(titulo)}</h2>");
        foreach (var (secao, texto) in partes)
            html.Append($"<h3>{WebUtility.HtmlEncode(secao)}</h3><pre style=\"font-family:inherit;white-space:pre-wrap\">{WebUtility.HtmlEncode(texto)}</pre>");
        return html.ToString();
    }
}

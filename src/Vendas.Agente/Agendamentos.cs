using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace Vendas.Agente;

// Criador: o login que fez o pedido. Vem sempre do servidor, nunca do corpo do pedido.
public record Agendamento(string Freq, string Email, string? Criador = null);

// Resumo por email à segunda: a página guarda o pedido; a tarefa agendada "Vendas.Agente --enviar-resumo" envia (ver README).
public static class Agendamentos
{
    // Os dados de vendas só saem para endereços da empresa.
    public const string Dominio = "inforap.pt";
    // Freq que remove o agendamento do endereço em vez de o guardar.
    public const string Cancelar = "nenhum";

    // Pedido da página ({"freq":"semanal","email":"..."}): devolve o agendamento ou o erro a mostrar.
    public static (Agendamento? Agendamento, string? Erro) DoPedido(string corpo)
    {
        Agendamento? a;
        try { a = JsonSerializer.Deserialize<Agendamento>(corpo, JsonSerializerOptions.Web); }
        catch (JsonException) { return (null, "Pedido inválido."); }

        if (a?.Freq is not ("semanal" or "mensal" or Cancelar)) return (null, "Escolha semanal ou mensal.");
        var email = a.Email?.Trim() ?? "";
        // Address == email recusa "Nome <x@inforap.pt>" e listas: um só endereço, escrito tal e qual.
        if (!MailAddress.TryCreate(email, out var endereco) || endereco.Address != email
            || !endereco.Host.Equals(Dominio, StringComparison.OrdinalIgnoreCase))
            return (null, $"Só endereços @{Dominio}.");
        return (a with { Email = email, Criador = null }, null);
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
    {
        if (!File.Exists(ficheiro)) return [];
        try { return JsonSerializer.Deserialize<List<Agendamento>>(File.ReadAllText(ficheiro)) ?? []; }
        // Editado à mão e partido: diz qual é o ficheiro em vez de um erro de JSON solto.
        catch (JsonException ex) { throw new InvalidDataException($"{ficheiro} está corrompido: corrija-o ou apague-o. {ex.Message}", ex); }
    }

    private static readonly object Bloqueio = new();

    // Um agendamento por endereço: escolher de novo troca a frequência; "nenhum" deixa de enviar.
    // Com lock: dois pedidos ao mesmo tempo liam a mesma lista e um dos agendamentos perdia-se.
    public static void Guardar(string ficheiro, Agendamento novo)
    {
        lock (Bloqueio)
        {
            var lista = Ler(ficheiro);
            lista.RemoveAll(a => a.Email.Equals(novo.Email, StringComparison.OrdinalIgnoreCase));
            if (novo.Freq != Cancelar) lista.Add(novo);
            Directory.CreateDirectory(Path.GetDirectoryName(ficheiro)!);
            // shortcut: escrita direta, sem ficheiro temporário + troca (o File.Move falhava às vezes com o antivírus a ler o ficheiro);
            // um crash a meio da escrita parte o ficheiro e o Ler diz qual é.
            File.WriteAllText(ficheiro, JsonSerializer.Serialize(lista));
        }
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

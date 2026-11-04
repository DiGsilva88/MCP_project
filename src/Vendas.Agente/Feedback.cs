using System.Text.Json;

namespace Vendas.Agente;

// Avaliação das respostas ("Esta resposta está certa?"), guardada numa linha de texto por avaliação.
public static class Feedback
{
    // Os motivos que a página oferece; "" é o "Sim".
    private static readonly string[] Motivos = ["", "Números errados", "Não respondeu à pergunta", "Difícil de ler"];

    // Só a avaliação e um motivo da lista: a pergunta e a resposta podem ter nomes de clientes (dados pessoais).
    // Devolve null se o pedido não vier da página (avaliação ou motivo fora da lista).
    public static string? Linha(string corpo, DateTimeOffset agora)
    {
        try
        {
            using var json = JsonDocument.Parse(corpo);
            var avaliacao = json.RootElement.GetProperty("avaliacao").GetString();
            var motivo = json.RootElement.TryGetProperty("motivo", out var m) ? m.GetString() ?? "" : "";
            var valido = (avaliacao == "sim" && motivo == "") || (avaliacao == "nao" && motivo != "" && Motivos.Contains(motivo));
            return valido ? $"{agora:yyyy-MM-dd HH:mm}\t{avaliacao}\t{motivo}" : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}

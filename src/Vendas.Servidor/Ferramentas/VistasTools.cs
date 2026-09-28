using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

#nullable enable

namespace Vendas.Servidor.Ferramentas;

// Colunas que cada tool aceita: mesmos nomes do Campo, mas cada tool só vê o seu subconjunto
// (uma para os dados do cliente, outra para as condições de faturação).
public enum ColunaCliente { Zona, Vendedor, TipoCliente, Actividade, Distrito }
public enum ColunaFaturacao { Pagamento, Cobranca, Expedicao, SituacaoFinanceira, EscalaoPlafond, EscalaoVolumeVendas }

// Uma tool por view, cada uma só com as colunas da sua view (as views não se misturam).

[McpServerToolType]
public sealed class VistasTools(IVendasRepo repo, ILogger<VistasTools> log)
{
    private const int MaxLinhas = 100;

    // Frase devolvida ao modelo quando algo falha. Não revela nada do SQL.
    private const string ErroNeutro = "Não foi possivel consultar os dados neste momento";

    private static readonly IReadOnlyList<Campo> ColunasCliente =
        Enum.GetValues<ColunaCliente>().Select(c => Enum.Parse<Campo>(c.ToString())).ToArray();

    private static readonly IReadOnlyList<Campo> ColunasFaturacao =
        Enum.GetValues<ColunaFaturacao>().Select(c => Enum.Parse<Campo>(c.ToString())).ToArray();

    private const string Modo =
        " Sem coluna: todas as colunas. Só com coluna: NomeCliente e essa coluna. " +
        "Com coluna e valor: só as linhas com esse valor exato (valor=\"sem dados\" filtra quem não tem valor nessa coluna). " +
        "Com contar=true e coluna: conta clientes por valor da coluna, com percentagens. Devolve CSV. " +
        "Se a lista de linhas (não a de grupos) for cortada, use pagina=2, 3... para ver o resto.";

    // Parâmetros de uma chamada às tools de consulta, já traduzidos para Campo.
    // Agrupados num único tipo para não passar 8-9 argumentos soltos entre os métodos privados.
    private sealed record PedidoConsulta(
        Campo? Coluna, IReadOnlyList<Campo> TodasAsColunas, string? Valor, bool Contar,
        Campo? ColunaCruzada, string? ValorCruzado, int Limite, int Pagina);

    [McpServerTool(Name = "clientes_consultar")]
    [Description("Dados gerais dos clientes: NomeCliente, Zona, Vendedor, TipoCliente, Actividade, Distrito." + Modo +
        " Para cruzar duas colunas (ex.: contar por Zona só dos clientes de um Vendedor), use cruzarCom/valorCruzado.")]
    public Task<string> ClientesAsync(
        [Description("Opcional: coluna a mostrar/filtrar/contar.")] ColunaCliente? coluna = null,
        [Description("Opcional, só com coluna: valor exato, ex.: Lisboa, ou \"sem dados\"")] string? valor = null,
        [Description("true para contar clientes por valor da coluna.")] bool contar = false,
        [Description("Opcional: segunda coluna para cruzar, filtrando o resultado por ela também.")] ColunaCliente? cruzarCom = null,
        [Description("Opcional, só com cruzarCom: valor exato dessa segunda coluna.")] string? valorCruzado = null,
        [Description("Máximo de linhas ou grupos, 1 a 100")] int limite = 50,
        [Description("Página das linhas, 1 é a primeira (não pagina contagens/grupos).")] int pagina = 1,
        CancellationToken ct = default)
        => ConsultarAsync(new PedidoConsulta(
            coluna is null ? null : Enum.Parse<Campo>(coluna.ToString()!), ColunasCliente, valor, contar,
            cruzarCom is null ? null : Enum.Parse<Campo>(cruzarCom.ToString()!), valorCruzado, limite, pagina), ct);

    [McpServerTool(Name = "faturacao_consultar")]
    [Description("Condições de faturação dos clientes: NomeCliente, Pagamento, Cobranca, Expedicao, " +
        "SituacaoFinanceira, EscalaoPlafond, EscalaoVolumeVendas. O escalão de volume de vendas é o valor DECLARADO " +
        "na ficha do cliente, não a faturação real; não devolve valores faturados." + Modo +
        " Para cruzar duas colunas (ex.: contar por Cobranca só dos clientes com um Pagamento), use cruzarCom/valorCruzado.")]
    public Task<string> FaturacaoAsync(
        [Description("Opcional: coluna a mostrar/filtrar/contar.")] ColunaFaturacao? coluna = null,
        [Description("Opcional, só com coluna: valor exato, ex.: 0 - sem plafond, ou \"sem dados\"")] string? valor = null,
        [Description("true para contar clientes por valor da coluna.")] bool contar = false,
        [Description("Opcional: segunda coluna para cruzar, filtrando o resultado por ela também.")] ColunaFaturacao? cruzarCom = null,
        [Description("Opcional, só com cruzarCom: valor exato dessa segunda coluna.")] string? valorCruzado = null,
        [Description("Máximo de linhas ou grupos, 1 a 100")] int limite = 50,
        [Description("Página das linhas, 1 é a primeira (não pagina contagens/grupos).")] int pagina = 1,
        CancellationToken ct = default)
        => ConsultarAsync(new PedidoConsulta(
            coluna is null ? null : Enum.Parse<Campo>(coluna.ToString()!), ColunasFaturacao, valor, contar,
            cruzarCom is null ? null : Enum.Parse<Campo>(cruzarCom.ToString()!), valorCruzado, limite, pagina), ct);

    private async Task<string> ConsultarAsync(PedidoConsulta pedido, CancellationToken ct)
    {
        var limite = Math.Clamp(pedido.Limite, 1, MaxLinhas);
        var deslocamento = (Math.Max(1, pedido.Pagina) - 1) * limite;

        if (pedido.Coluna is null && (pedido.Contar || !string.IsNullOrWhiteSpace(pedido.Valor)))
            return "Indique uma coluna para contar ou filtrar.";

        if (pedido.ColunaCruzada is null && !string.IsNullOrWhiteSpace(pedido.ValorCruzado))
            return "Indique cruzarCom para filtrar por valorCruzado.";

        // contar agrupa pela coluna; filtrar a mesma coluna por um valor deixaria sempre um único
        // grupo a 100%. Para filtrar enquanto conta, usa-se cruzarCom noutra coluna.
        if (pedido.Contar && !string.IsNullOrWhiteSpace(pedido.Valor))
            return "Não faz sentido contar e filtrar pela mesma coluna. Para filtrar e contar, use cruzarCom/valorCruzado.";

        var filtros = new Dictionary<Campo, string>();
        if (pedido.Coluna is not null && !string.IsNullOrWhiteSpace(pedido.Valor))
            filtros[pedido.Coluna.Value] = pedido.Valor.Trim();
        if (pedido.ColunaCruzada is not null && !string.IsNullOrWhiteSpace(pedido.ValorCruzado))
            filtros[pedido.ColunaCruzada.Value] = pedido.ValorCruzado.Trim();

        try
        {
            return pedido.Contar
                ? FormatarContagem(pedido.Coluna!.Value.ToString(), await repo.ContarAsync(pedido.Coluna.Value, filtros, limite, ct))
                : FormatarDados(await repo.ListarAsync(
                    pedido.Coluna is null ? pedido.TodasAsColunas : [pedido.Coluna.Value], filtros, null, deslocamento, limite, ct));
        }
        catch (OperationCanceledException) { throw; } // cancelamento não é avaria: deixa passar
        catch (Exception ex) // nunca mostra ao modelo detalhes do SQL
        {
            log.LogError(ex, "Falha em {Campo}_consultar", pedido.Coluna);
            return ErroNeutro;
        }
    }

    private static string FormatarDados(PaginaClientes pagina)
    {
        if (pagina.Linhas.Count == 0)
            return "Nenhuma linha encontrada com esse filtro.";

        var csv = new StringBuilder(string.Join(',', pagina.Colunas)).Append('\n');
        foreach (var linha in pagina.Linhas)
            csv.Append(string.Join(',', linha.Select(Campo))).Append('\n');

        //Avisa o modelo quando a lista foi cortada para ele não pensar que já viu a info toda
        if (pagina.Total > pagina.Linhas.Count)
            csv.Append($"#Mostrados {pagina.Linhas.Count} de {pagina.Total}.Filtre por valor ou use pagina=2,3... para ver o resto.\n");

        return csv.ToString();
    }

    private static string FormatarContagem(string nome, IReadOnlyList<ContagemCliente> linhas)
    {
        if (linhas.Count == 0)
            return "Sem dados para esta coluna";

        //Total e grupos são iguais em todas as linhas (o SQL calcula-os com OVER() )
        var (total, grupos) = (linhas[0].Total, linhas[0].Grupos);

        var csv = new StringBuilder()
            .AppendLine($"Clientes por {nome}:{total} clientes em {grupos} grupos.")
            .AppendLine($"{nome}, clientes, percentagem");

        // InvariantCulture garante ponto decimal (14.1).
        // Com a cultura portuguesa sairia 14,1 e a vírgula partia o CSV
        foreach (var linha in linhas)
            csv.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{Campo(linha.Valor)}, {linha.Clientes},{100.0 * linha.Clientes / total:0.0}"));

        // Mais grupos na base de dados do que linhas mostradas: a lista foi cortada.
        if (grupos > linhas.Count)
            csv.AppendLine($" #mostrados {linhas.Count} , {grupos} de grupos, aumente o limite para ver os restantes.");

        return csv.ToString();
    }

    //Protege o CSV: um valor com vírgula, aspas ou quebra de linha vai entre aspas.
    // Ex.: Bento, Filhos  ->  "Bento, Filhos"
    private static string Campo(string texto)
        => texto.IndexOfAny([',', '"', '\n', '\r']) < 0 ? texto : $"\"{texto.Replace("\"", "\"\"")}\"";
}

using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Ferramentas;

// Uma só tool para as duas views (ficha do cliente + condições de faturação). O SQL já junta as
// views por ClienteID, por isso qualquer coluna pode ser cruzada com qualquer outra.
[McpServerToolType]
public sealed class VistasTools(IVendasRepo repo, ILogger<VistasTools> log)
{
    private const int MaxLinhas = 100;

    // Frase devolvida ao modelo quando algo falha. Não revela nada do SQL.
    private const string ErroNeutro = "Não foi possivel consultar os dados neste momento";

    // O filtro é por valor exato; sem match, o modelo desistia em vez de ir ver os valores que existem.
    private const string DicaValores =
        " O valor tem de ser exato: chame consultar com coluna=<coluna>, contar=true (sem valor) para ver os valores existentes e tente de novo.";

    private static readonly IReadOnlyList<Campo> TodasAsColunas = Enum.GetValues<Campo>();

    [McpServerTool(Name = "consultar")]
    [Description(
        "Dados dos clientes. Colunas da ficha: Zona, Localidade, Vendedor, TipoCliente, Actividade, Distrito. " +
        "Colunas de faturação: Pagamento, Cobranca, Expedicao, SituacaoFinanceira, EscalaoPlafond, EscalaoVolumeVendas " +
        "(o escalão de volume de vendas é o valor DECLARADO na ficha, não a faturação real; não há valores faturados). " +
        "Sem coluna: NomeCliente e todas as colunas. Só com coluna: NomeCliente e essa coluna. " +
        "Com coluna e valor: só as linhas com esse valor (exato ou o início, ex.: \"30 dias\" apanha \"30 Dias Fim do Mês\"; valor=\"sem dados\" filtra quem não tem valor). " +
        "Com contar=true: conta clientes por valor da coluna, com percentagens; com contar=true e valor: quantos clientes têm esse valor. " +
        "Os valores têm de ser exatos: se não souber os valores existentes, chame primeiro coluna=X, contar=true (sem valor). " +
        "cruzarCom/valorCruzado filtra também por outra coluna, de qualquer grupo " +
        "(ex.: coluna=Zona, contar=true, cruzarCom=Pagamento, valorCruzado=\"30 dias\"). " +
        "Devolve CSV. Se a lista de linhas for cortada, use pagina=2, 3... para ver o resto.")]
    public async Task<string> ConsultarAsync(
        [Description("Opcional: coluna a mostrar/filtrar/contar.")] Campo? coluna = null,
        [Description("Opcional, só com coluna: valor exato, ex.: Lisboa, ou \"sem dados\".")] string valor = null,
        [Description("true para contar clientes por valor da coluna.")] bool contar = false,
        [Description("Opcional: segunda coluna (diferente de coluna) para filtrar o resultado.")] Campo? cruzarCom = null,
        [Description("Obrigatório com cruzarCom: valor exato dessa segunda coluna.")] string valorCruzado = null,
        [Description("Máximo de linhas ou grupos, 1 a 100.")] int limite = 50,
        [Description("Página das linhas, 1 é a primeira (não pagina contagens).")] int pagina = 1,
        CancellationToken ct = default)
    {
        var temValor = !string.IsNullOrWhiteSpace(valor);
        var temValorCruzado = !string.IsNullOrWhiteSpace(valorCruzado);

        if (coluna is null && (contar || temValor))
            return "Indique uma coluna para contar ou filtrar.";

        if ((cruzarCom is not null) != temValorCruzado)
            return "cruzarCom e valorCruzado têm de ser usados juntos.";

        if (cruzarCom is not null && cruzarCom == coluna)
            return "Escolha uma coluna diferente para cruzar.";

        var filtros = new Dictionary<Campo, string>();
        if (temValor) filtros[coluna!.Value] = valor!.Trim();
        if (temValorCruzado) filtros[cruzarCom!.Value] = valorCruzado!.Trim();

        limite = Math.Clamp(limite, 1, MaxLinhas);
        var deslocamento = (Math.Max(1, pagina) - 1) * limite;

        try
        {
            return contar
                ? FormatarContagem(coluna!.Value.ToString(), await repo.ContarAsync(coluna.Value, filtros, limite, ct), filtros)
                : FormatarDados(await repo.ListarAsync(
                    coluna is null ? TodasAsColunas : [coluna.Value], filtros, deslocamento, limite, ct), filtros);
        }
        catch (OperationCanceledException) { throw; } // cancelamento não é avaria: deixa passar
        catch (Exception ex) // nunca mostra ao modelo detalhes do SQL
        {
            log.LogError(ex, "Falha em consultar ({Coluna})", coluna);
            return ErroNeutro;
        }
    }

    // Num cruzamento, a coluna filtrada que não é mostrada ficava invisível no resultado.
    // Nesse caso devolve todos os filtros ("Actividade = X e Distrito = Y"); senão, vazio.
    private static string FiltrosEscondidos(Dictionary<Campo, string> filtros, IEnumerable<string> mostradas)
        => filtros.Keys.Any(k => !mostradas.Contains(k.ToString()))
            ? string.Join(" e ", filtros.Select(f => $"{f.Key} = {f.Value}"))
            : "";

    private static string FormatarDados(PaginaClientes pagina, Dictionary<Campo, string> filtros)
    {
        if (pagina.Linhas.Count == 0)
            return "Nenhuma linha encontrada com esse filtro." + (filtros.Count > 0 ? DicaValores : "");

        var csv = new StringBuilder();
        var cruzados = FiltrosEscondidos(filtros, pagina.Colunas);
        if (cruzados.Length > 0)
            csv.Append($"Clientes com {cruzados}: {pagina.Total} clientes.\n");

        csv.Append(string.Join(',', pagina.Colunas)).Append('\n');
        foreach (var linha in pagina.Linhas)
            csv.Append(string.Join(',', linha.Select(Campo))).Append('\n');

        // Avisa o modelo quando a lista foi cortada para ele não pensar que já viu a info toda
        if (pagina.Total > pagina.Linhas.Count)
            csv.Append($"# Mostrados {pagina.Linhas.Count} de {pagina.Total}. Filtre por valor ou use pagina=2,3... para ver o resto.\n");

        return csv.ToString();
    }

    private static string FormatarContagem(string nome, IReadOnlyList<ContagemCliente> linhas, Dictionary<Campo, string> filtros)
    {
        if (linhas.Count == 0)
            return "Sem dados para esta coluna." + (filtros.Count > 0 ? DicaValores : "");

        // Total e grupos são iguais em todas as linhas (o SQL calcula-os com OVER())
        var (total, grupos) = (linhas[0].Total, linhas[0].Grupos);

        var cruzados = FiltrosEscondidos(filtros, [nome]);
        var csv = new StringBuilder()
            .AppendLine($"Clientes por {nome}{(cruzados.Length > 0 ? $", com {cruzados}" : "")}: {total} clientes em {grupos} grupos.")
            .AppendLine($"{nome},clientes,percentagem");

        // InvariantCulture garante ponto decimal (14.1).
        // Com a cultura portuguesa sairia 14,1 e a vírgula partia o CSV
        foreach (var linha in linhas)
            csv.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{Campo(linha.Valor)},{linha.Clientes},{100.0 * linha.Clientes / total:0.0}"));

        // Mais grupos na base de dados do que linhas mostradas: a lista foi cortada.
        if (grupos > linhas.Count)
            csv.AppendLine($"# Mostrados {linhas.Count} de {grupos} grupos. Aumente o limite para ver os restantes.");

        return csv.ToString();
    }

    // Protege o CSV: um valor com vírgula, aspas ou quebra de linha vai entre aspas.
    // Ex.: Bento, Filhos  ->  "Bento, Filhos"
    private static string Campo(string texto)
        => texto.IndexOfAny([',', '"', '\n', '\r']) < 0 ? texto : $"\"{texto.Replace("\"", "\"\"")}\"";
}

using System.ComponentModel;
using System.Diagnostics;
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
    internal const int MaxLinhas = 100;

    // Evita overflow de (pagina-1)*limite, que dava OFFSET negativo no SQL.
    private const int MaxPaginas = 1000;

    // Frase devolvida ao modelo quando algo falha. Não revela nada do SQL.
    internal const string ErroNeutro = "Não foi possivel consultar os dados neste momento";

    // Máximo de valores existentes devolvidos quando o filtro não bate (poupa um turno ao modelo).
    private const int MaxValores = 30;

    private const int MaxLinhasSensivel = 20;
    private const int MaxPaginasSensivel = 5;

    private static readonly IReadOnlyList<Campo> TodasAsColunas = Enum.GetValues<Campo>();

    [McpServerTool(Name = "consultar")]
    // Descrições curtas de propósito: o modelo corre em CPU e lê-as em cada pedido.
    [Description(
        "Listar ou contar clientes. Colunas: Zona, Localidade, Vendedor, TipoCliente, Actividade, Distrito, " +
        "Pagamento, Cobranca, Expedicao, SituacaoFinanceira, EscalaoPlafond, EscalaoVolumeVendas. " +
        "contar=true: nº de clientes por valor da coluna (ex.: top 5 localidades: coluna=Localidade, contar=true, limite=5). " +
        "valor: filtra a coluna. cruzarCom/valorCruzado: filtra também por outra coluna " +
        "(ex.: actividade com mais clientes em Corroios: coluna=Actividade, contar=true, cruzarCom=Localidade, valorCruzado=Corroios). " +
        "nomeComecaPor: clientes cujo nome começa por esse texto. " +
        "Faturação real (top clientes, artigos, familias, marcas, vendas de um cliente): usar consultar_vendas. " +
        "Volume declarado na ficha ou plafond: usar consultar_sensivel. " +
        "Devolve CSV; pagina=2 para continuar.")]
    public async Task<string> ConsultarAsync(
        [Description("Coluna a mostrar/filtrar/contar.")] Campo? coluna = null,
        [Description("Valor da coluna, ex.: Lisboa, ou \"sem dados\".")] string? valor = null,
        [Description("true para contar clientes por valor da coluna.")] bool contar = false,
        [Description("Segunda coluna, para filtrar.")] Campo? cruzarCom = null,
        [Description("Valor de cruzarCom.")] string? valorCruzado = null,
        [Description("Máximo de linhas ou grupos, 1 a 100.")] int limite = 10,
        [Description("Página das linhas, 1 é a primeira (não pagina contagens).")] int pagina = 1,
        [Description("Opcional: início do nome do cliente, ex.: A.")] string? nomeComecaPor = null,
        CancellationToken ct = default)
    {
        var temValor = !string.IsNullOrWhiteSpace(valor);
        var temValorCruzado = !string.IsNullOrWhiteSpace(valorCruzado);

        if (coluna is null && (contar || temValor))
            return "Indique uma coluna para contar ou filtrar.";

        if (contar && !string.IsNullOrWhiteSpace(nomeComecaPor))
            return "nomeComecaPor não se combina com contar; liste os clientes.";

        if ((cruzarCom is not null) != temValorCruzado)
            return "cruzarCom e valorCruzado têm de ser usados juntos.";

        if (cruzarCom is not null && cruzarCom == coluna)
            return "Escolha uma coluna diferente para cruzar.";

        // Erro típico do modelo: o mesmo valor (ex.: uma localidade) nas duas colunas. Só se avisa quando não há
        // resultados: "Setúbal" pode ser mesmo localidade e distrito ao mesmo tempo.
        var avisoMesmoValor = temValor && temValorCruzado
            && valor!.Trim().Equals(valorCruzado!.Trim(), StringComparison.OrdinalIgnoreCase)
            ?$"valor e valorCruzado são iguais. \"{valorCruzado!.Trim()}\" é um valor de {cruzarCom}: repita sem valor, " +
              $"ex.: coluna={coluna}{(contar ? ", contar=true" : "")}, cruzarCom={cruzarCom}, valorCruzado={valorCruzado.Trim()}."
            : null;

        var filtros = new Dictionary<Campo, string>();
        if (temValor) filtros[coluna!.Value] = valor!.Trim();
        if (temValorCruzado) filtros[cruzarCom!.Value] = valorCruzado!.Trim();

        limite = Math.Clamp(limite, 1, MaxLinhas);
        var deslocamento = (Math.Clamp(pagina, 1, MaxPaginas) - 1) * limite;

        var relogio = Stopwatch.StartNew();
        try
        {
            if (contar)
            {
                var contagens = await repo.ContarAsync(coluna!.Value, filtros, limite, ct);
                return contagens.Count == 0 && filtros.Count > 0
                    ? avisoMesmoValor ?? await SemCorrespondenciaAsync(filtros, ct)
                    : FormatarContagem(coluna.Value.ToString(), contagens, filtros);
            }

            var resultado = await repo.ListarAsync(
                coluna is null ? TodasAsColunas : [coluna.Value], filtros, deslocamento, limite, nomeComecaPor, ct);
            return resultado.Linhas.Count == 0 && filtros.Count > 0 && deslocamento == 0 && string.IsNullOrWhiteSpace(nomeComecaPor)
                ? avisoMesmoValor ?? await SemCorrespondenciaAsync(filtros, ct)
                : FormatarDados(resultado, filtros);
        }
        catch (OperationCanceledException) { throw; } // cancelamento não é avaria: deixa passar
        catch (Exception ex) // nunca mostra ao modelo detalhes do SQL
        {
            log.LogError(ex, "Falha em consultar ({Coluna})", coluna);
            return ErroNeutro;
        }
        finally
        {
            log.LogInformation("consultar {Coluna} em {Ms} ms", coluna, relogio.ElapsedMilliseconds);
        }
    }

    [McpServerTool(Name = "consultar_sensivel")]
    [Description(
        "Dado de cada cliente: Contribuinte (NIF), Email, Telefone, Morada, CodigoPostal, VolumeVendas, Plafond " +
        "(VolumeVendas = declarado na ficha, não faturado). maiores=true: do maior para o menor, para top N. " +
        "coluna/valor: filtra clientes (ex.: coluna=Localidade, valor=Azeitão). " +
        "mostrarTambem: junta outra coluna a cada linha (ex.: Actividade). nomeComecaPor: início do nome. Devolve CSV.")]
    public async Task<string> ConsultarSensivelAsync(
        [Description("Dado a mostrar.")] CampoSensivel dado,
        [Description("Coluna para filtrar os clientes.")] Campo? coluna = null,
        [Description("Valor dessa coluna.")] string? valor = null,
        [Description("true: do maior para o menor (só VolumeVendas e Plafond).")] bool maiores = false,
        [Description("Máximo de linhas, 1 a 20.")] int limite = 10,
        [Description("Página, 1 é a primeira.")] int pagina = 1,
        [Description("Início do nome do cliente.")] string? nomeComecaPor = null,
        [Description("Coluna a mostrar junto do dado.")] Campo? mostrarTambem = null,
        CancellationToken ct = default)
    {
        var temValor = !string.IsNullOrWhiteSpace(valor);
        if ((coluna is not null) != temValor)
            return "coluna e valor têm de ser usados juntos.";

        if (maiores && dado is not (CampoSensivel.VolumeVendas or CampoSensivel.Plafond))
            return "maiores só se aplica a VolumeVendas e Plafond.";

        // Recusar em vez de clampear: com clamp a página 6 devolvia a 5 e o modelo mostrava-a como nova.
        if (pagina > MaxPaginasSensivel)
            return $"Não há mais páginas: o máximo são {MaxPaginasSensivel}. Filtre por um valor para ver outros clientes.";

        var filtros = new Dictionary<Campo, string>();
        if (temValor) filtros[coluna!.Value] = valor!.Trim();

        limite = Math.Clamp(limite, 1, MaxLinhasSensivel);
        var deslocamento = (Math.Max(pagina, 1) - 1) * limite;

        int? linhas = null; // null na auditoria = a consulta falhou
        try
        {
            var resultado = await repo.ListarSensivelAsync(dado, filtros, maiores, deslocamento, limite, nomeComecaPor, mostrarTambem, ct);
            linhas = resultado.Linhas.Count;
            return resultado.Linhas.Count == 0 && filtros.Count > 0 && deslocamento == 0 && string.IsNullOrWhiteSpace(nomeComecaPor)
                ? await SemCorrespondenciaAsync(filtros, ct)
                : FormatarDados(resultado, filtros);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) // nunca mostra ao modelo detalhes do SQL
        {
            log.LogError(ex, "Falha em consultar_sensivel ({Dado})", dado); // sem valores: são dados pessoais
            return ErroNeutro;
        }
        finally
        {
            // Auditoria de cada acesso, também os que falham. Regista os parâmetros (o que permite ver
            // extrações por nomeComecaPor/páginas) mas nunca os valores devolvidos.
            log.LogInformation(
                "Auditoria: consultar_sensivel dado={Dado} filtros={Filtros} nomeComecaPor={Nome} maiores={Maiores} " +
                "mostrarTambem={Tambem} pagina={Pagina} limite={Limite} linhas={Linhas}",
                dado, string.Join(", ", filtros.Select(f => $"{f.Key}={f.Value}")), nomeComecaPor, maiores,
                mostrarTambem, pagina, limite, linhas?.ToString() ?? "falhou");
        }
    }

    // Filtro sem match: devolve já os valores existentes de cada coluna filtrada, para o modelo
    // não gastar outra chamada só a descobri-los.
    // Com um só filtro o valor não existe. Num cruzamento, verifica cada filtro sozinho: se todos existem,
    // a combinação tem 0 clientes; senão diz qual falhou e lista só os valores dessa coluna.
    // (Antes listava sempre todas as colunas e o modelo apresentava as contagens globais como resposta.)
    private async Task<string> SemCorrespondenciaAsync(Dictionary<Campo, string> filtros, CancellationToken ct)
    {
        var inexistentes = filtros;
        if (filtros.Count > 1)
        {
            // Uma consulta por filtro, em paralelo (cada chamada ao repo abre a sua ligação).
            var existe = await Task.WhenAll(filtros.Select(async f =>
                (f.Key, f.Value, Existe: (await repo.ContarAsync(f.Key, new Dictionary<Campo, string> { [f.Key] = f.Value }, 1, ct)).Count > 0)));
            inexistentes = existe.Where(e => !e.Existe).ToDictionary(e => e.Key, e => e.Value);

            if (inexistentes.Count == 0)
                return $"0 clientes com {string.Join(" e ", filtros.Select(f => $"{f.Key} = {f.Value}"))}: " +
                       "os valores existem, mas nenhum cliente tem os dois.";
        }

        var texto = new StringBuilder(filtros.Count == 1
            ? "Nenhuma correspondência. O valor tem de ser um dos existentes (entre parênteses, nº de clientes):\n"
            : $"Nenhuma correspondência: {string.Join(" e ", inexistentes.Select(f => $"{f.Key} = {f.Value}"))} não existe. " +
              "Valores existentes (entre parênteses, nº de clientes no total, não nesta combinação):\n");

        foreach (var campo in inexistentes.Keys)
        {
            var valores = await repo.ContarAsync(campo, new Dictionary<Campo, string>(), MaxValores, ct);
            texto.Append($"{campo}: {string.Join("; ", valores.Select(v => $"{Campo(v.Valor)} ({v.Clientes})"))}");
            if (valores.Count > 0 && valores[0].Grupos > valores.Count)
                texto.Append($" (mostrados {valores.Count} de {valores[0].Grupos})");
            texto.Append('\n');
        }
        return texto.ToString();
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
            return "Nenhuma linha encontrada com esse filtro.";

        var csv = new StringBuilder();
        var cruzados = FiltrosEscondidos(filtros, pagina.Colunas);
        if (cruzados.Length > 0)
            csv.Append($"Clientes com {cruzados}: {pagina.Total} clientes.\n");

        csv.Append(string.Join(',', pagina.Colunas)).Append('\n');
        foreach (var linha in pagina.Linhas)
            csv.Append(string.Join(',', linha.Select(Campo))).Append('\n');

        // Avisa o modelo quando a lista foi cortada para ele não pensar que já viu a info toda
        // Sem corte também diz o total, para o modelo nunca ter de o adivinhar ("10 de 10").
        csv.Append(pagina.Total > pagina.Linhas.Count
            ? $"# Mostrados {pagina.Linhas.Count} de {pagina.Total} clientes.\n"
            : $"# Lista completa: {pagina.Linhas.Count} clientes.\n");

        return csv.ToString();
    }

    private static string FormatarContagem(string nome, IReadOnlyList<ContagemCliente> linhas, Dictionary<Campo, string> filtros)
    {
        if (linhas.Count == 0)
            return "Sem dados para esta coluna.";

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
            csv.AppendLine($"# Mostrados {linhas.Count} de {grupos} grupos.");

        return csv.ToString();
    }

    // Protege o CSV: um valor com vírgula, aspas ou quebra de linha vai entre aspas.
    // Ex.: Bento, Filhos  ->  "Bento, Filhos"
    internal static string Campo(string texto)
        => texto.IndexOfAny([',', '"', '\n', '\r']) < 0 ? texto : $"\"{texto.Replace("\"", "\"\"")}\"";
}

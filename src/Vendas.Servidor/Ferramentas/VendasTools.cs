using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Vendas.Servidor.Modelos.Dados;
using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Ferramentas;


// Ferramentas sobre a view mcp.ViewMCP_vendas.
// Todas agregam no SQL: nenhuma devolve linhas de documentos.
[McpServerToolType]
public sealed class VendasTools(IVendasRepo repo, ILogger<VendasTools> log)
{
    // Quantos clientes se mostram quando o texto serve a vários.
    private const int MaxClientes = 5;

    [McpServerTool(Name = "consultar_vendas")]
    // Descrição curta de propósito, como nas outras tools: o modelo lê-a em cada pedido.
    [Description(

        "Ranking de faturação real num período (valor líquido sem IVA, notas de crédito descontadas). " +
        "agrupar=Cliente: melhores clientes (nome e ID); Artigo, Familia ou Marca: o que mais se vende; Mes: atividade mês a mês. " +
        "mes/ano: mês ou ano concreto (março de 2026: mes=3, ano=2026). " +
        "coluna/valor: só clientes dessa localidade, distrito, zona ou vendedor (ex.: agrupar=Cliente, coluna=Localidade, valor=Seixal). " +
        "cliente (ID ou nome) limita a esse cliente: o que compra, quanto, quando. " +
        "Devolve CSV: posicao, <agrupar>, valor, percentagem, documentos, media (+quantidade fora de Cliente), bruto, desconto, iva.")]
    public async Task<string> ConsultarVendasAsync(

        [Description("Cliente, Artigo, Familia, Marca ou Mes.")] DimensaoVenda agrupar = DimensaoVenda.Cliente,
        [Description("Ultimos7Dias, Ultimos30Dias, Ultimos90Dias, Ultimos12Meses, AnoAtual, AnoAnterior, MesAtual ou MesAnterior.")]
        Periodo periodo = Periodo.AnoAtual,
        [Description("Máximo de linhas, 1 a 100.")] int limite = 10,
        [Description("Opcional: ID do cliente ou início do nome.")] string? cliente = null,
        [Description("Opcional: mês 1 a 12 (março=3). Tem prioridade sobre periodo.")] int? mes = null,
        [Description("Opcional: ano, ex.: 2026. Sem mes = ano inteiro.")] int? ano = null,
        [Description("Opcional: coluna da ficha, ex.: Localidade, Distrito, Zona, Vendedor.")] Campo? coluna = null,
        [Description("Valor dessa coluna, ex.: Seixal.")] string? valor = null,
        CancellationToken ct = default)
    {
        limite = Math.Clamp(limite, 1, VistasTools.MaxLinhas);

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        if (mes is < 1 or > 12) return "mes tem de ser de 1 a 12.";
        if (ano is < 2000 || ano > hoje.Year) return $"ano tem de ser de 2000 a {hoje.Year}.";
        var temValor = !string.IsNullOrWhiteSpace(valor);
        if ((coluna is not null) != temValor) return "coluna e valor têm de ser usados juntos.";

        var filtros = new Dictionary<Campo, string>();
        if (temValor) filtros[coluna!.Value] = valor!.Trim();

        var relogio = Stopwatch.StartNew();
        try
        {
            // O cliente tem de existir e ser um só: senão "Sem vendas" ou uma soma de vários clientes
            // pareceriam a resposta de um cliente.
            string? rotulo = null;
            if (!string.IsNullOrWhiteSpace(cliente))
            {
                var texto = cliente.Trim();
                var encontrados = await repo.ProcurarClientesAsync(texto, MaxClientes + 1, ct);
                if (encontrados.Count == 0)
                    return $"Cliente \"{texto}\" não encontrado. Indique o ID ou o início do nome.";

                // Um ID exato ganha aos nomes que começam pelo mesmo texto.
                if (encontrados.Count > 1 && !encontrados[0].Id.Equals(texto, StringComparison.OrdinalIgnoreCase))
                    return $"Vários clientes começam por \"{texto}\". Indique o ID de um deles:\nid,cliente\n" +
                           string.Join('\n', encontrados.Take(MaxClientes).Select(c => $"{c.Id},{VistasTools.Campo(c.Nome)}")) +
                           (encontrados.Count > MaxClientes ? $"\n# Há mais de {MaxClientes}." : "");

                (cliente, rotulo) = (encontrados[0].Id, $"{encontrados[0].Nome} ({encontrados[0].Id})");
            }

            DateOnly desde, ate; string descricao;
            if (mes is null && ano is null)
            {
                (desde, ate) = Periodos.Intervalo(periodo, hoje);
                descricao = Periodos.Descricao(periodo);
            }
            else (desde, ate, descricao) = Periodos.IntervaloMes(mes, ano, hoje);

            // Um método por view de origem: os clientes precisam do nome que vive na ViewMCP_cliente.
            var linhas = agrupar == DimensaoVenda.Cliente
                ? await repo.TopClientesAsync(desde, ate, limite, cliente, filtros, ct)
                : await repo.TopArtigosAsync(agrupar, desde, ate, limite, cliente, filtros, ct);

            // Sem vendas com filtro: se o valor nem existe (ex.: "Seixall"), mostra os que existem.
            if (linhas.Count == 0 && filtros.Count > 0
                && (await repo.ContarAsync(coluna!.Value, filtros, 1, ct)).Count == 0)
                return await VistasTools.SemCorrespondenciaAsync(repo, filtros, ct);

            return FormatarRanking(agrupar, descricao, desde, ate, rotulo, filtros, linhas);
        }
        catch (OperationCanceledException) { throw; } // cancelamento não é avaria: deixa passar
        catch (Exception ex) // nunca mostra ao modelo detalhes do SQL
        {
            log.LogError(ex, "Falha em consultar_vendas ({Agrupar}, {Periodo})", agrupar, periodo);
            return VistasTools.ErroNeutro;
        }
        finally
        {
            log.LogInformation("consultar_vendas {Agrupar} {Periodo} em {Ms} ms", agrupar, periodo, relogio.ElapsedMilliseconds);
        }
    }

    private static string FormatarRanking(
        DimensaoVenda agrupar, string descricao, DateOnly desde, DateOnly ate, string? rotuloCliente,
        Dictionary<Campo, string> filtros, IReadOnlyList<ValorAgrupado> linhas)
    {
        var inv = CultureInfo.InvariantCulture;
        var (cabecalho, plural) = (Dimensoes.Cabecalho(agrupar), Dimensoes.Plural(agrupar));
        // "ate" é exclusivo: mostra-se o último dia incluído
        var intervalo = $"{desde.ToString("dd-MM-yyyy", inv)} a {ate.AddDays(-1).ToString("dd-MM-yyyy", inv)}";
        if (rotuloCliente is not null)
            intervalo += $", cliente \"{rotuloCliente}\"";
        if (filtros.Count > 0)
            intervalo += ", " + string.Join(" e ", filtros.Select(f => $"{f.Key} = {f.Value}"));

        if (linhas.Count == 0)
            return $"Sem vendas: {descricao} ({intervalo}).";

        // Total, grupos e documentos são iguais em todas as linhas (o SQL calcula-os uma vez)
        var (total, grupos, documentos) = (linhas[0].Total, linhas[0].Grupos, linhas[0].TotalDocumentos);
        var media = documentos == 0 ? "-" : (total / documentos).ToString("0.00", inv);
        var comQuantidade = agrupar != DimensaoVenda.Cliente;

        var csv = new StringBuilder()
            .AppendLine(string.Create(inv,
                $"Vendas por {cabecalho}, {descricao} ({intervalo}): total {total:0.00} em {grupos} {plural}, {documentos} documentos, média {media} por documento."))
            .AppendLine($"posicao,{cabecalho},valor,percentagem,documentos,media{(comQuantidade ? ",quantidade" : "")},bruto,desconto,iva");

        var posicao = 0;
        foreach (var linha in linhas)
        {
            // Total 0 (ex.: só notas de crédito que anulam as vendas): sem percentagem, evita dividir por zero
            var percentagem = total == 0 ? "-" : (100 * linha.Valor / total).ToString("0.0", inv);
            var quantidade = comQuantidade ? "," + linha.Quantidade.ToString("0.##", inv) : "";
            var mediaLinha = linha.Documentos == 0 ? "-" : (linha.Valor / linha.Documentos).ToString("0.00", inv);
            csv.AppendLine(string.Create(inv,
                $"{++posicao},{VistasTools.Campo(linha.Nome)},{linha.Valor:0.00},{percentagem},{linha.Documentos},{mediaLinha}{quantidade},{linha.Bruto:0.00},{linha.Desconto:0.00},{linha.Iva:0.00}"));
        }

        // Mais grupos na base de dados do que linhas mostradas: a lista foi cortada.
        if (grupos > linhas.Count)
            csv.AppendLine($"# Mostrados {linhas.Count} de {grupos} {plural}.");

        return csv.ToString();
    }
}
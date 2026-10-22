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

    // Dias sem vendas no fim do período a partir dos quais se avisa que os dados acabam antes.
    private const int DiasSemDadosAviso = 7;

    // Números no formato pt-PT (38 087,07), com espaço normal no milhar: o modelo pequeno
    // trocava dígitos ao converter "38087.07" sozinho, por isso recebe-os já prontos a copiar.
    private static readonly NumberFormatInfo Pt = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = " " };

    [McpServerTool(Name = "consultar_vendas")]
    // Descrição curta de propósito, como nas outras tools: o modelo lê-a em cada pedido.
    // Sem exemplos com valores reais (ex.: mes=3): o modelo pequeno copiava-os.
    [Description(
        "Ranking de faturação real num período (valor líquido sem IVA, notas de crédito descontadas). " +
        "agrupar=Cliente: melhores clientes (nome e ID); Artigo, Familia ou Marca: o que mais se vende; Mes: atividade mês a mês; " +
        "Localidade, Distrito, Zona, Vendedor ou Actividade: onde/quem vende mais (vendas, não nº de clientes). " +
        "ordenar=Quantidade: por unidades vendidas. " +
        "mes/ano: mês ou ano concreto (mes=<1 a 12>, ano=<ano>). " +
        "dataInicio/dataFim: intervalo concreto (semana, trimestre, semestre), formato dd-MM-aaaa. " +
        "coluna/valor: só clientes dessa localidade, distrito, zona ou vendedor (ex.: agrupar=Cliente, coluna=Localidade, valor=Seixal). " +
        "cliente (ID ou nome) limita a esse cliente: o que compra, quanto, quando. " +
        "semVendas=true: clientes SEM compras no período, com a data da última compra. " +
        "Devolve texto pronto a mostrar: título com total e período, valor médio por documento e lista numerada.")]


    public async Task<string> ConsultarVendasAsync(
        [Description("Cliente, Artigo, Familia, Marca, Mes, Localidade, Distrito, Zona, Vendedor ou Actividade.")] DimensaoVenda agrupar = DimensaoVenda.Cliente,
        [Description("Ultimos7Dias, Ultimos30Dias, Ultimos90Dias, Ultimos12Meses, AnoAtual, AnoAnterior, MesAtual ou MesAnterior.")]

        Periodo periodo = Periodo.AnoAtual,
        [Description("Máximo de linhas, 1 a 100.")] int limite = 10,
        [Description("Opcional: ID do cliente ou início do nome.")] string? cliente = null,
        [Description("Opcional: mês 1 a 12. Tem prioridade sobre periodo.")] int? mes = null,
        [Description("Opcional: ano com 4 dígitos. Sem mes = ano inteiro.")] int? ano = null,
        [Description("Opcional: data inicial dd-MM-aaaa. Usar com dataFim. Tem prioridade sobre mes, ano e periodo.")] string? dataInicio = null,
        [Description("Opcional: data final dd-MM-aaaa (incluída).")] string? dataFim = null,
        [Description("Opcional: coluna da ficha, ex.: Localidade, Distrito, Zona, Vendedor.")] Campo? coluna = null,
        [Description("Valor dessa coluna, ex.: Seixal.")] string? valor = null,
        [Description("Valor (euros) ou Quantidade (unidades).")] OrdenarVenda ordenar = OrdenarVenda.Valor,
        [Description("true: clientes sem compras no período.")] bool semVendas = false,
        CancellationToken ct = default)
    {
        limite = Math.Clamp(limite, 1, VistasTools.MaxLinhas);

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        if (mes is < 1 or > 12) return "mes tem de ser de 1 a 12.";
        if (ano is < 2000 || ano > hoje.Year) return $"ano tem de ser de 2000 a {hoje.Year}.";

        // Intervalo livre (semana, trimestre, semestre). Erros numa linha só: o agente não os conta
        // como dados e o modelo pode corrigir e chamar de novo.
        DateOnly? inicio = null, fim = null;
        if ((dataInicio is null) != (dataFim is null)) return "dataInicio e dataFim têm de ser usados juntos.";
        if (dataInicio is not null)
        {
            if (!DateOnly.TryParseExact(dataInicio.Trim(), "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var i)
                || !DateOnly.TryParseExact(dataFim!.Trim(), "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                return "Datas no formato dd-MM-aaaa, ex.: 01-08-2026.";
            if (f < i) return "dataFim tem de ser igual ou depois de dataInicio.";
            (inicio, fim) = (i, f);
        }

        var temValor = !string.IsNullOrWhiteSpace(valor);
        if ((coluna is not null) != temValor) return "coluna e valor têm de ser usados juntos.";
        // Somar unidades de artigos diferentes por cliente não quer dizer nada.
        if (ordenar == OrdenarVenda.Quantidade && agrupar == DimensaoVenda.Cliente && !semVendas)
            return "ordenar=Quantidade não se aplica a Cliente: use agrupar=Artigo, Familia, Marca ou Localidade.";
        if (semVendas && !string.IsNullOrWhiteSpace(cliente))
            return "semVendas lista vários clientes: não se combina com cliente.";

        var filtros = new Dictionary<Campo, string>();
        if (temValor) filtros[coluna!.Value] = valor!.Trim();

        var relogio = Stopwatch.StartNew();
        try
        {
            DateOnly desde, ate; string descricao;
            if (inicio is not null)
                // "ate" é exclusivo no resto do código: soma-se um dia à data final pedida.
                (desde, ate, descricao) = (inicio.Value, fim!.Value.AddDays(1), "período indicado");
            else if (mes is null && ano is null)
            {
                (desde, ate) = Periodos.Intervalo(periodo, hoje);
                descricao = Periodos.Descricao(periodo);
            }
            else (desde, ate, descricao) = Periodos.IntervaloMes(mes, ano, hoje);

            if (semVendas)
            {
                var semCompras = await repo.ClientesSemVendasAsync(desde, ate, limite, filtros, ct);
                if (semCompras.Count == 0 && filtros.Count > 0
                    && (await repo.ContarAsync(coluna!.Value, filtros, 1, ct)).Count == 0)
                    return await VistasTools.SemCorrespondenciaAsync(repo, filtros, ct);
                return FormatarSemVendas(descricao, desde, ate, filtros, semCompras);
            }

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

            var porQuantidade = ordenar == OrdenarVenda.Quantidade;
            // Um método por view de origem: os clientes precisam do nome que vive na ViewMCP_cliente.
            var linhas = agrupar == DimensaoVenda.Cliente
                ? await repo.TopClientesAsync(desde, ate, limite, cliente, filtros, ct)
                : await repo.TopArtigosAsync(agrupar, desde, ate, limite, cliente, filtros, porQuantidade, ct);

            // Sem vendas com filtro: se o valor nem existe (ex.: "Seixall"), mostra os que existem.
            if (linhas.Count == 0 && filtros.Count > 0
                && (await repo.ContarAsync(coluna!.Value, filtros, 1, ct)).Count == 0)
                return await VistasTools.SemCorrespondenciaAsync(repo, filtros, ct);

            // Período sem vendas nenhumas: diz até onde vão os dados, senão "não houve vendas" parece real
            // quando a view só ainda não foi atualizada.
            var ultimaVenda = linhas.Count == 0 && rotulo is null && filtros.Count == 0
                ? await repo.UltimaVendaAsync(ct)
                : null;

            return FormatarRanking(agrupar, porQuantidade, descricao, desde, ate, rotulo, filtros, linhas, ultimaVenda);
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

    private static string Euros(decimal v) => v.ToString("#,##0.00", Pt);
    private static string Data(DateOnly d) => d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    // "01-01-2026 a 21-10-2026, Localidade = Seixal". "ate" é exclusivo: mostra-se o último dia incluído.
    private static string Intervalo(DateOnly desde, DateOnly ate, Dictionary<Campo, string> filtros)
    {
        var intervalo = $"{Data(desde)} a {Data(ate.AddDays(-1))}";
        if (filtros.Count > 0)
            intervalo += ", " + string.Join(" e ", filtros.Select(f => $"{f.Key} = {f.Value}"));
        return intervalo;
    }

    // Texto pronto a mostrar ao utilizador (e legível pelo modelo): título, média, aviso, lista numerada, rodapé.
    private static string FormatarRanking(
        DimensaoVenda agrupar, bool porQuantidade, string descricao, DateOnly desde, DateOnly ate, string? rotuloCliente,
        Dictionary<Campo, string> filtros, IReadOnlyList<ValorAgrupado> linhas, DateOnly? ultimaVenda)
    {
        var (cabecalho, plural) = (Dimensoes.Cabecalho(agrupar), Dimensoes.Plural(agrupar));
        var intervalo = Intervalo(desde, ate, filtros);
        if (rotuloCliente is not null)
            intervalo += $", cliente \"{rotuloCliente}\"";

        if (linhas.Count == 0)
            return $"Sem vendas: {descricao} ({intervalo})." +
                   (ultimaVenda is { } u ? $"\nAtenção: as últimas vendas registadas são de {Data(u)}." : "");

        // Total, grupos, documentos e última data são iguais em todas as linhas (o SQL calcula-os uma vez)
        var (total, grupos, documentos) = (linhas[0].Total, linhas[0].Grupos, linhas[0].TotalDocumentos);
        var comQuantidade = agrupar != DimensaoVenda.Cliente;
        var ordem = porQuantidade && agrupar != DimensaoVenda.Mes ? ", por quantidade" : "";

        var texto = new StringBuilder()
            .AppendLine($"Vendas por {cabecalho}{ordem}, {descricao} ({intervalo}): total {Euros(total)} € em {grupos} {plural}, " +
                        $"{documentos.ToString("#,##0", Pt)} documentos. Valores em euros, sem IVA.")
            .AppendLine(documentos == 0 ? "Valor médio por documento: -" : $"Valor médio por documento: {Euros(total / documentos)} €.");

        // A última venda do período fica bem antes do fim: os dados acabam aí (view por atualizar).
        if (linhas[0].UltimaData is { } ultima && ate.AddDays(-1).DayNumber - ultima.DayNumber > DiasSemDadosAviso)
            texto.AppendLine($"Atenção: a última venda registada neste período é de {Data(ultima)}.");

        var posicao = 0;
        foreach (var linha in linhas)
        {
            var nome = string.IsNullOrWhiteSpace(linha.Nome) ? "(sem descrição)" : linha.Nome.Trim();
            // Total 0 (ex.: só notas de crédito que anulam as vendas): sem percentagem, evita dividir por zero
            var percentagem = total == 0 ? "" : $" ({(100 * linha.Valor / total).ToString("0.0", Pt)}%)";

            texto.AppendLine(porQuantidade && comQuantidade
                ? $"{++posicao}. {nome} – {linha.Quantidade.ToString("#,##0.##", Pt)} unidades ({Euros(linha.Valor)} €)"
                : $"{++posicao}. {nome} – {Euros(linha.Valor)} €{percentagem}");
        }

        // Diz sempre quantas linhas vieram e quantas há, para o modelo nunca ter de o adivinhar.
        texto.AppendLine(grupos > linhas.Count
            ? $"Mostro {linhas.Count} de {grupos} {plural}."
            : $"Lista completa: {linhas.Count} {plural}.");

        return texto.ToString();
    }

    private static string FormatarSemVendas(
        string descricao, DateOnly desde, DateOnly ate, Dictionary<Campo, string> filtros, IReadOnlyList<ClienteSemVendas> linhas)
    {
        var intervalo = Intervalo(desde, ate, filtros);
        if (linhas.Count == 0)
            return $"0 clientes sem vendas: {descricao} ({intervalo}). Todos os clientes da ficha compraram neste período.";

        var texto = new StringBuilder()
            .AppendLine($"Clientes sem vendas, {descricao} ({intervalo}): {linhas[0].Total} clientes.");

        var posicao = 0;
        foreach (var linha in linhas)
        {
            var nome = string.IsNullOrWhiteSpace(linha.Nome) ? "(sem nome)" : linha.Nome.Trim();
            texto.AppendLine($"{++posicao}. {nome} – " +
                             (linha.Ultima is { } u ? $"última compra a {Data(u)}" : "nunca comprou"));
        }

        texto.AppendLine(linhas[0].Total > linhas.Count
            ? $"Mostro {linhas.Count} de {linhas[0].Total} clientes."
            : $"Lista completa: {linhas.Count} clientes.");
        return texto.ToString();
    }
}
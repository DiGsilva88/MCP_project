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
public sealed class VendasTools(IVendasRepo repo, ILogger<VendasTools> log, TimeProvider? fonteTempo = null)
{
    // Quantos clientes se mostram quando o texto serve a vários.
    private const int MaxClientes = 5;

    // Dias sem vendas no fim do período a partir dos quais se avisa que os dados acabam antes.
    private const int DiasSemDadosAviso = 7;

    // Números no formato pt-PT (38 087,07), com espaço normal no milhar: o modelo pequeno
    // trocava dígitos ao converter "38087.07" sozinho, por isso recebe-os já prontos a copiar.
    private static readonly NumberFormatInfo Pt = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = " " };

    private readonly TimeProvider tempo = fonteTempo ?? TimeProvider.System;

    [McpServerTool(Name = "consultar_vendas")]
    // Descrição curta de propósito, como nas outras tools: o modelo lê-a em cada pedido.
    // Sem exemplos com valores reais (ex.: mes=3): o modelo pequeno copiava-os.
    [Description(
        "Ranking de faturação real num período (valor líquido sem IVA, notas de crédito descontadas). " +
        "agrupar=Cliente: melhores clientes (nome e ID); Artigo, Familia ou Marca: o que mais se vende; " +
        "Mes ou Trimestre: evolução no tempo, completa, com o melhor; " +
        "Localidade, Distrito, Zona, Vendedor ou Actividade: onde/quem vende mais (vendas, não nº de clientes). " +
        "emCada: o 1.º de agrupar dentro de cada grupo (ex.: agrupar=Artigo, emCada=Distrito: o artigo mais vendido em cada distrito). " +
        "ordenar=Quantidade: por unidades vendidas. " +
        "mes/ano: mês ou ano concreto (mes=<1 a 12>, ano=<ano>). " +
        "dataInicio/dataFim: intervalo concreto (semana, trimestre, semestre), formato dd-MM-aaaa. " +
        "coluna/valor: só clientes dessa localidade, distrito, zona ou vendedor (ex.: agrupar=Cliente, coluna=Localidade, valor=Seixal). " +
        "cliente (ID ou nome) limita a esse cliente: o que compra, quanto, quando. " +
        "semVendas=true: clientes SEM compras no período, com a data da última compra. " +
        "Devolve texto pronto a mostrar: título com total e período, valor médio por documento e lista numerada.")]
    public async Task<string> ConsultarVendasAsync(
        [Description("Cliente, Artigo, Familia, Marca, Mes, Trimestre, Localidade, Distrito, Zona, Vendedor ou Actividade.")] DimensaoVenda agrupar = DimensaoVenda.Cliente,
        [Description("Ultimos7Dias, Ultimos30Dias, Ultimos90Dias, Ultimos12Meses, AnoAtual, AnoAnterior, MesAtual ou MesAnterior.")] Periodo periodo = Periodo.AnoAtual,
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
        [Description("Opcional: Mes, Trimestre, Localidade, Distrito, Zona, Vendedor ou Actividade. O melhor de agrupar em cada um.")] DimensaoVenda? emCada = null,
        CancellationToken ct = default)
    {
        // Evolução no tempo vem sempre completa: com limite=10, 12 meses perdiam os 2 mais recentes.
        limite = Dimensoes.Cronologica(emCada ?? agrupar) ? Respostas.Maxlinhas : Math.Clamp(limite, 1, Respostas.Maxlinhas);

        var hoje = DateOnly.FromDateTime(tempo.GetLocalNow().DateTime);
        var (intervalo, erroPeriodo) = Periodos.Resolver(periodo, mes, ano, dataInicio, dataFim, hoje);
        if (intervalo is not { } janela) return erroPeriodo!;

        var temValor = !string.IsNullOrWhiteSpace(valor);
        if ((ErroDoPedido(agrupar, ordenar, coluna, temValor, semVendas, cliente) ?? ErroEmCada(agrupar, emCada, semVendas)) is { } erro) return erro;

        var filtros = new Dictionary<Campo, string>();
        if (temValor) filtros[coluna!.Value] = valor!.Trim();

        return await Respostas.ExecutarAsync(log, "consultar_vendas", $"{agrupar} {periodo}", () =>
            semVendas
                ? ListarSemVendasAsync(janela, limite, filtros, ct)
                : ListarRankingAsync(agrupar, ordenar, emCada, janela, limite, cliente, filtros, ct));
    }

    private static string? ErroEmCada(DimensaoVenda agrupar, DimensaoVenda? emCada, bool semVendas)
    {
        if (emCada is not { } grupo) return null;
        if (semVendas) return "semVendas não se combina com emCada.";
        if (grupo == agrupar) return "emCada tem de ser diferente de agrupar.";
        // O nome do cliente vive noutra view: o ranking por cliente não tem como se partir em grupos.
        if (agrupar == DimensaoVenda.Cliente || grupo == DimensaoVenda.Cliente)
            return "emCada não se aplica a Cliente: use agrupar=Artigo, Familia, Marca, Mes ou Localidade.";
        // As duas iriam buscar o valor à mesma ficha do cliente (ficha.ValorFicha).
        if (Dimensoes.CampoDaFicha(agrupar) is not null && Dimensoes.CampoDaFicha(grupo) is not null)
            return "agrupar e emCada não podem ser os dois da ficha (Localidade, Distrito, Zona, Vendedor, Actividade).";
        return null;
    }

    // Combinações que não fazem sentido. Erros numa linha só: o agente não os conta como dados
    // e o modelo pode corrigir e chamar de novo.
    private static string? ErroDoPedido(
        DimensaoVenda agrupar, OrdenarVenda ordenar, Campo? coluna, bool temValor, bool semVendas, string? cliente)
    {
        if ((coluna is not null) != temValor) return "coluna e valor têm de ser usados juntos.";
        // Somar unidades de artigos diferentes por cliente não quer dizer nada.
        if (ordenar == OrdenarVenda.Quantidade && agrupar == DimensaoVenda.Cliente && !semVendas)
            return "ordenar=Quantidade não se aplica a Cliente: use agrupar=Artigo, Familia, Marca ou Localidade.";
        if (semVendas && !string.IsNullOrWhiteSpace(cliente))
            return "semVendas lista vários clientes: não se combina com cliente.";
        return null;
    }

    private async Task<string> ListarSemVendasAsync(
        IntervaloVendas janela, int limite, Dictionary<Campo, string> filtros, CancellationToken ct)
    {
        var semCompras = await repo.ClientesSemVendasAsync(janela.Desde, janela.Ate, limite, filtros, ct);
        return semCompras.Count == 0 && await FiltroSemClientesAsync(filtros, ct)
            ? await Respostas.SemCorrespondenciaAsync(repo, filtros, ct)
            : FormatarSemVendas(janela, filtros, semCompras);
    }

    private async Task<string> ListarRankingAsync(
        DimensaoVenda agrupar, OrdenarVenda ordenar, DimensaoVenda? emCada, IntervaloVendas janela, int limite, string? cliente,
        Dictionary<Campo, string> filtros, CancellationToken ct)
    {
        var (idCliente, rotuloCliente, erro) = await ResolverClienteAsync(cliente, ct);
        if (erro is not null) return erro;

        var porQuantidade = ordenar == OrdenarVenda.Quantidade;
        // Um método por view de origem: os clientes precisam do nome que vive na ViewMCP_cliente.
        var linhas = agrupar == DimensaoVenda.Cliente
            ? await repo.RankingPorClienteAsync(janela.Desde, janela.Ate, limite, idCliente, filtros, ct)
            : await repo.RankingPorDimensaoAsync(agrupar, janela.Desde, janela.Ate, limite, idCliente, filtros, porQuantidade, emCada, ct);

        // Sem vendas com filtro: se o valor nem existe (ex.: "Seixall"), mostra os que existem.
        if (linhas.Count == 0 && await FiltroSemClientesAsync(filtros, ct))
            return await Respostas.SemCorrespondenciaAsync(repo, filtros, ct);

        // Período sem vendas nenhumas: diz até onde vão os dados, senão "não houve vendas" parece real
        // quando a view só ainda não foi atualizada.
        var ultimaVenda = linhas.Count == 0 && rotuloCliente is null && filtros.Count == 0
            ? await repo.UltimaVendaAsync(ct)
            : null;

        return FormatarRanking(agrupar, porQuantidade, emCada, janela, rotuloCliente, filtros, linhas, ultimaVenda);
    }

    // O cliente tem de existir e ser um só: senão "Sem vendas" ou uma soma de vários clientes
    // pareceriam a resposta de um cliente. Sem texto: sem filtro de cliente.
    private async Task<(string? Id, string? Rotulo, string? Erro)> ResolverClienteAsync(string? cliente, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cliente)) return (null, null, null);

        var texto = cliente.Trim();
        var encontrados = await repo.ProcurarClientesAsync(texto, MaxClientes + 1, ct);
        if (encontrados.Count == 0)
            return (null, null, $"Cliente \"{texto}\" não encontrado. Indique o ID ou o início do nome.");

        // Um ID exato ganha aos nomes que começam pelo mesmo texto.
        if (encontrados.Count > 1 && !encontrados[0].Id.Equals(texto, StringComparison.OrdinalIgnoreCase))
            return (null, null, $"Vários clientes começam por \"{texto}\". Indique o ID de um deles:\nid,cliente\n" +
                   string.Join('\n', encontrados.Take(MaxClientes).Select(c => $"{c.Id},{Respostas.EscaparCsv(c.Nome)}")) +
                   (encontrados.Count > MaxClientes ? $"\n# Há mais de {MaxClientes}." : ""));

        return (encontrados[0].Id, $"{encontrados[0].Nome} ({encontrados[0].Id})", null);
    }

    // Com filtro da ficha, "sem resultados" pode querer dizer que o valor não existe (ex.: "Seixall").
    private async Task<bool> FiltroSemClientesAsync(Dictionary<Campo, string> filtros, CancellationToken ct)
        => filtros.Count > 0 && (await repo.ContarAsync(filtros.Keys.First(), filtros, 1, ct)).Count == 0;

    private static string Euros(decimal v) => v.ToString("#,##0.00", Pt);
    private static string Data(DateOnly d) => d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    // "01-01-2026 a 21-10-2026, Localidade = Seixal". "Ate" é exclusivo: mostra-se o último dia incluído.
    private static string Intervalo(IntervaloVendas janela, Dictionary<Campo, string> filtros)
    {
        var intervalo = $"{Data(janela.Desde)} a {Data(janela.Ate.AddDays(-1))}";
        return filtros.Count > 0 ? $"{intervalo}, {Respostas.DescreverFiltros(filtros)}" : intervalo;
    }

    // Texto pronto a mostrar ao utilizador (e legível pelo modelo): título, média, aviso, lista numerada, rodapé.
    private static string FormatarRanking(
        DimensaoVenda agrupar, bool porQuantidade, DimensaoVenda? emCada, IntervaloVendas janela, string? rotuloCliente,
        Dictionary<Campo, string> filtros, IReadOnlyList<ValorAgrupado> linhas, DateOnly? ultimaVenda)
    {
        var intervalo = Intervalo(janela, filtros);
        if (rotuloCliente is not null)
            intervalo += $", cliente \"{rotuloCliente}\"";

        if (linhas.Count == 0)
            return $"Sem vendas: {janela.Descricao} ({intervalo})." +
                   (ultimaVenda is { } u ? $"\nAtenção: as últimas vendas registadas são de {Data(u)}." : "");

        // Total, grupos, documentos e última data são iguais em todas as linhas (o SQL calcula-os uma vez)
        var (total, grupos, documentos) = (linhas[0].Total, linhas[0].Grupos, linhas[0].TotalDocumentos);
        var cabecalho = Dimensoes.Cabecalho(agrupar);
        // Com emCada cada linha é um grupo (ex.: um distrito): a contagem e o rodapé falam desses grupos.
        var plural = Dimensoes.Plural(emCada ?? agrupar);
        var comQuantidade = porQuantidade && agrupar != DimensaoVenda.Cliente;
        var ordem = porQuantidade && (emCada is not null || !Dimensoes.Cronologica(agrupar)) ? ", por quantidade" : "";
        var titulo = emCada is { } grupo ? $"Melhor {cabecalho} em cada {Dimensoes.Cabecalho(grupo)}" : $"Vendas por {cabecalho}";

        var texto = new StringBuilder()
            .AppendLine($"{titulo}{ordem}, {janela.Descricao} ({intervalo}): total {Euros(total)} € em {grupos} {plural}, " +
                        $"{documentos.ToString("#,##0", Pt)} documentos. Valores em euros, sem IVA.")
            .AppendLine(documentos == 0 ? "Valor médio por documento: -" : $"Valor médio por documento: {Euros(total / documentos)} €.");

        // A última venda do período fica bem antes do fim: os dados acabam aí (view por atualizar).
        if (linhas[0].UltimaData is { } ultima && janela.Ate.AddDays(-1).DayNumber - ultima.DayNumber > DiasSemDadosAviso)
            texto.AppendLine($"Atenção: a última venda registada neste período é de {Data(ultima)}.");

        // Na evolução no tempo a 1.ª linha é a mais antiga, não a melhor: diz-se qual é, para não se responder com a 1.ª.
        if (emCada is null && Dimensoes.Cronologica(agrupar) && linhas.Count > 1)
        {
            var melhor = linhas.MaxBy(l => comQuantidade ? l.Quantidade : l.Valor)!;
            texto.AppendLine(comQuantidade
                ? $"Melhor {cabecalho}: {melhor.Nome} – {melhor.Quantidade.ToString("#,##0.##", Pt)} unidades ({Euros(melhor.Valor)} €)."
                : $"Melhor {cabecalho}: {melhor.Nome} – {Euros(melhor.Valor)} €.");
        }

        for (var i = 0; i < linhas.Count; i++)
            texto.AppendLine(emCada is { } g
                ? LinhaDoRanking(i + 1, linhas[i], linhas[i].TotalGrupo, comQuantidade,
                    (g is DimensaoVenda.Localidade or DimensaoVenda.Zona or DimensaoVenda.Actividade or DimensaoVenda.Familia or DimensaoVenda.Marca
                        ? " da " : " do ") + Dimensoes.Cabecalho(g))
                : LinhaDoRanking(i + 1, linhas[i], total, comQuantidade));

        // Diz sempre quantas linhas vieram e quantas há, para o modelo nunca ter de o adivinhar.
        texto.AppendLine(grupos > linhas.Count
            ? $"Mostro {linhas.Count} de {grupos} {plural}."
            : $"Lista completa: {linhas.Count} {plural}.");

        return texto.ToString();
    }

    // sufixo: de que total é a percentagem (" do distrito" com emCada; vazio = do total do período).
    private static string LinhaDoRanking(int posicao, ValorAgrupado linha, decimal total, bool comQuantidade, string sufixo = "")
    {
        var nome = string.IsNullOrWhiteSpace(linha.Nome) ? "(sem descrição)" : linha.Nome.Trim();
        if (comQuantidade)
            return $"{posicao}. {nome} – {linha.Quantidade.ToString("#,##0.##", Pt)} unidades ({Euros(linha.Valor)} €)";

        // Total 0 (ex.: só notas de crédito que anulam as vendas): sem percentagem, evita dividir por zero
        var percentagem = total == 0 ? "" : $" ({(100 * linha.Valor / total).ToString("0.0", Pt)}%{sufixo})";
        return $"{posicao}. {nome} – {Euros(linha.Valor)} €{percentagem}";
    }

    private static string FormatarSemVendas(
        IntervaloVendas janela, Dictionary<Campo, string> filtros, IReadOnlyList<ClienteSemVendas> linhas)
    {
        var intervalo = Intervalo(janela, filtros);
        if (linhas.Count == 0)
            return $"0 clientes sem vendas: {janela.Descricao} ({intervalo}). Todos os clientes da ficha compraram neste período.";

        var texto = new StringBuilder()
            .AppendLine($"Clientes sem vendas, {janela.Descricao} ({intervalo}): {linhas[0].Total} clientes.");

        for (var i = 0; i < linhas.Count; i++)
        {
            var nome = string.IsNullOrWhiteSpace(linhas[i].Nome) ? "(sem nome)" : linhas[i].Nome.Trim();
            texto.AppendLine($"{i + 1}. {nome} – " +
                             (linhas[i].Ultima is { } u ? $"última compra a {Data(u)}" : "nunca comprou"));
        }

        texto.AppendLine(linhas[0].Total > linhas.Count
            ? $"Mostro {linhas.Count} de {linhas[0].Total} clientes."
            : $"Lista completa: {linhas.Count} clientes.");
        return texto.ToString();
    }
}
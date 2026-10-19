using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Vendas.Servidor.Modelos.Dados;
using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Ferramentas;


//ferranentas sobre a view mcp_vendas 
//todas agregam no SQL : nenhuma devolve linhas de documentos

[McpServerToolType]

public sealed class VendasTools(IVendasRepo repo, ILogger<VendasTools> log)
{
    
    [McpServerTool(Name = "consultar_vendas" )]
    [Description(
        "Ranking de faturação real num periodo(valor liquído sem IVA, notas de crédito descontadas). " +
        "agrupar= Cliente: melhores clientes; Artigo, Familia ou Marca: o que mais se vende. "+
        "Devolve CSV: posicao, <agrupar>, valor, percentagem,documentos (+quantidade fora de Cliente.")]
    public async Task<string> ConsultarVendasAsync(

        [Description("Cliente, Artigo, Familia ou Marca.")] DimensaoVenda agrupar = DimensaoVenda.Cliente,
        [Description("Periodo.")] Periodo periodo = Periodo.AnoAtual,
        [Description("Máximo linhas, 1 a 100")] int limite =10,
        CancellationToken cancellationToken =default)
    {
        limite = Math.Clamp(limite,1, VistasTools.MaxLinhas);

        var relogio = Stopwatch.StartNew();
        try
        {
            var(desde, ate) = Periodos.Intervalo(periodo, Hoje());
            //0 repositorio tem um metodo por view de origem: os clientes precisam do nome da viewMCP_cliente

            var linhas = agrupar == DimensaoVenda.Cliente
            ? await repo.TopClientesAsync (desde, ate, limite, cancellationToken)
            : await repo.TopArtigosAsync (agrupar,desde,ate, limite, cancellationToken);

        return FormatarRanking(Dimensoes.Cabecalho(agrupar), Dimensoes.Plural(agrupar),
                Periodos.Descricao(periodo),desde,ate, linhas,
                comQuantidade: agrupar != DimensaoVenda.Cliente);
        }

        catch (OperationCanceledException) {throw;}
        catch (Exception exeption) //nunca mostra ao modelo detalhes  do Sql
        {
            log.LogError(exeption, "Falha em consultar_vendas ({Agrupar}), ({Periodo})", agrupar, periodo);
            return VistasTools.ErroNeutro;

        }
        finally
        {
            log.LogInformation(" consultar_vendas {Agrupar} {Periodo} em {Ms} ms",
            agrupar, periodo, relogio.ElapsedMilliseconds);
        }
    }

    private static DateOnly Hoje() => DateOnly.FromDateTime(DateTime.Today);

    internal static string FormatarRanking(string cabecalho, string plural, string descricao,
        DateOnly desde, DateOnly ate, IReadOnlyList<ValorAgrupado> linhas, bool comQuantidade)
    {
        if (linhas.Count == 0)
            return $"Sem vendas no periodo {descricao}.";

        // Total e grupos são iguais em todas as linhas (o SQL calcula-os com OVER())
        var (total, grupos) = (linhas[0].Total, linhas[0].Grupos);

        // "ate" é exclusivo no Intervalo; o último dia incluído é o anterior
        var csv = new StringBuilder()
            .AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"Vendas por {cabecalho}, {descricao} ({desde:yyyy-MM-dd} a {ate.AddDays(-1):yyyy-MM-dd}): total {total:0.00} em {grupos} {plural}."))
            .AppendLine($"posicao,{cabecalho},valor,percentagem,documentos{(comQuantidade ? ",quantidade" : "")}");

        // InvariantCulture: ponto decimal, senão a vírgula partia o CSV
        for (var i = 0; i < linhas.Count; i++)
        {
            var l = linhas[i];
            var percentagem = total == 0 ? 0 : 100 * l.Valor / total;
            csv.Append(string.Create(CultureInfo.InvariantCulture,
                $"{i + 1},{VistasTools.Campo(l.Nome)},{l.Valor:0.00},{percentagem:0.0},{l.Documentos}"));
            if (comQuantidade)
                csv.Append(string.Create(CultureInfo.InvariantCulture, $",{l.Quantidade:0.##}"));
            csv.AppendLine();
        }

        if (grupos > linhas.Count)
            csv.AppendLine($"# Mostrados {linhas.Count} de {grupos} {plural}.");

        return csv.ToString();
    }
}
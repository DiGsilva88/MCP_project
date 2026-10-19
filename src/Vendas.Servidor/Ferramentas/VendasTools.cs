
using System.ComponentModel;
using System.Diagnostics;

namespace Vendas.Servidor.Ferramentas;


//ferranentas sobre a view mcp_vendas 
//todas agregam no SQL : nenhuma devolve linhas de documentos

[McpServerToolType]

public sealed class VendasTools(IVendas repo, ILogger<VistasTools>log)
{
    
    [McpServerTool(Name = "consultar_vendas" )]
    [Description(
        "Ranking de faturação real num periodo(valor liquído sem IVA, notas de crédito descontadas). " +
        "agrupar= Cliente: melhores clientes; Artigo, Familia ou Marca: o que mais se vende. "+
        "Devolve CSV: posicao, <agrupar>, valor, percentagem,documentos (+quantidade fora de Cliente.")]
    public async Task<string> ConsultarVendasAsync(

        [Description("Cliente, Artigo, Familia ou Marca.")] DimensaoVenda agrupar = DimensaoVenda.Cliente,
        [Description("Periodo.")] Periodo periodo = periodo.AnoAtual,
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
    
    





}
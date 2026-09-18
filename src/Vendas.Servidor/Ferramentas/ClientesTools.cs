
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Vendas.Servidor.Modelos.Dados;
using Vendas.Servidor.Modelos;
using System.Linq.Expressions;

namespace Vendas.Servidor.Ferramentas;

[McpServerToolType]
public sealed class ClientesTools(
    IVendasRepo vendasRepo,
    ILogger<ClientesTools> log)
{
    private readonly IVendasRepo _vendasRepo = vendasRepo;
    private readonly ILogger<ClientesTools> _log = log;

    // [McpServerTool(Name = "clientes_inativos")]
    // [Description("Clientes que não compram há pelo menos N dias, do mais antigo para o mais recente. Devolve CSV com cliente, data da última compra e dias sem comprar.")]
    // public async Task<string> InativosAsync(
    //     [Description("Número mínimo de dias sem comprar (1 a 3650).")] int dias = 90,
    //     CancellationToken ct = default)
    // {
    //     var diasEfetivo = Math.Clamp(dias, 1, 3650);

    //     try
    //     {
    //         var linhas = await _vendasRepo.ObterInativosAsync(diasEfetivo, ct);

    //         var sb = new StringBuilder();
    //         sb.AppendLine("cliente,ultima_compra,dias_sem_comprar");

    //         foreach (var l in linhas)
    //         {
                
    //             sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
    //                 $"{l.Cliente},{l.UltimaCompra:yyyy-MM-dd},{l.DiasSemComprar}"));
    //         }

    //         return sb.ToString();
    //     }
    //     catch (Exception ex)
    //     {
    //         _log.LogError(ex, "Falha em clientes_inativos");
    //         return "Não foi possível obter os dados de clientes neste momento.";
    //     }
    // }

    [McpServerTool(Name = "clientes_nomes")]
    [Description("Nomes de todos os clientes da ficha de clientes, por ordem alfabética. Um nome por linha.")]
    public async Task<string> NomesAsync(
        [Description("Quantos nomes devolver(1 a 500).")] int limite = 200,
        CancellationToken ct = default)
    {
        limite= Math.Clamp(limite,1, 500);
        try
        {
            var nomes = await _vendasRepo.ObterNomesClientesAsync(limite +1,ct);
            if (nomes.Count == 0)
            
            return "Sem clientes registados.";

            var texto = string.Join("\n", nomes.Take(limite));
            return nomes.Count > limite
                ? $"{texto}\n# mostrados os primeiros {limite} nomes; existem mais."
                : texto;

        }
        catch(OperationCanceledException) {throw;}
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha em clientes_nomes");
            return "Não foi possível obter os clientes neste momento.";
        }


        
    }
    [McpServerTool(Name ="clientes_por")]
        [Description("Quantos clientes existem, agrupados por zona, vendedor , tipo de cliente," +
        "atividade ou distrito. Devolve CSV com o valor e o numero de clientes.")]

    public async Task<string> ClientesPorAsync(
        [Description("O atributo pelo qual vai agrupar")]
         DimensaoCliente agrupar = DimensaoCliente.Zona,
        [Description("Quantas linhas a devolver(1 a 50).")] int limite = 20,
        CancellationToken cancellationToken = default)

        {
        limite = Math.Clamp(limite, 1 , 50);

        try

        {
            var todas = await _vendasRepo.ContarClientesAsync(agrupar, limite +1 , cancellationToken);
            if 
            (todas.Count == 0) 
            
            return $"Não há clientes com {agrupar} preenchido.";

            var sb = new StringBuilder();
            var nome = Dimensoes.Cabecalho(agrupar); //agrupa por tipo
            var total = todas[0].Total;                 //total
            sb.AppendLine($"Clientes por {nome.Replace('_', ' ')}: {total} clientes em {todas[0].Grupos} grupos.");
            sb.AppendLine($"{nome},clientes,%");
            foreach (var l in todas.Take(limite))
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture, 
                $"{Escapar(l.Valor)},{l.Clientes},{100.0 * l.Clientes / total:0.0}"));

            if (todas.Count > limite) sb.AppendLine("# existem mais valores para além destes");
            return sb.ToString();
        }

        catch (OperationCanceledException) {throw;}
        catch (Exception ex)
        {
            _log.LogError(ex,"Falha em clientes_por");
            return "Não foi possivel obter os dados de clientes neste momento";
        }
    }

//adicionar a ferramenta faturação condições
[McpServerTool(Name = "faturacao_condicoes")]
[Description 
("Quantos clientes existem por condição de pagamento, forma de cobrança, " +
                 "expedição, situação financeira, escalão de plafond ou escalão de volume de " +
                 "vendas. Devolve uma frase com o total e um CSV com o valor, o número de " +
                 "clientes e a percentagem. O volume de vendas é o valor DECLARADO na ficha do " +
                 "cliente, não a faturação real; esta ferramenta não devolve valores faturados.")]

public async Task<string> FaturacaoCondicoesAsync(
    [Description("A condição pela qual vai agrupar.")] 
    DimensaoFaturacao agrupar = DimensaoFaturacao.Pagamento,
    [Description("Quantas linhas devolver( 1 a 50).")] int limite = 20,
    CancellationToken cancellationToken= default)

    {
        limite = Math.Clamp(limite, 1, 50);
        try
        {
            var todas= await _vendasRepo.ContarFaturacaoAsync(
                agrupar,limite +1,cancellationToken);
                return todas.Count ==0
                ? "Sem dados de condições de faturação."
                : FormatarContagem(Dimensoes.Cabecalho(agrupar), todas, limite);
        }
        catch (OperationCanceledException) {throw;}
        catch(Exception ex)
        {
            _log.LogError(ex, "Falha em faturacao_condicoes");
            return "Não foi possivel obter as condicões de faturação neste momento.";
        }
    }

    private static string FormatarContagem(string nome, IReadOnlyList<ContagemCliente> todas, int limite)

    {
        var(total, grupos) =(todas[0].Total,todas[0].Grupos);
        var sb = new StringBuilder();
        sb.AppendLine($"Clientes por {nome.Replace('_', ' ')}:{total} clientes em {grupos} grupos.");
        sb.AppendLine($"{nome}, clientes, % total");
        foreach (var l in todas.Take(limite))
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"{Escapar(l.Valor)}, {l.Clientes},{100.0 * l.Clientes/total:0.0}"));

            if (todas.Count> limite)
            
                sb.AppendLine($" #mostra {limite} , {grupos} de grupos, aumente o limite para ver os restantes.");
                return sb.ToString();
            
    
    }

   private static string Escapar(string valor) =>
    valor.AsSpan().IndexOfAny(",\"\n\r") >= 0
        ? $"\"{valor.Replace("\"", "\"\"")}\""
        : valor;
}

using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Ferramentas;

[McpServerToolType]
public sealed class ClientesTools(
    IVendasRepo vendasRepo,
    ILogger<ClientesTools> log)
{
    private readonly IVendasRepo _vendasRepo = vendasRepo;
    private readonly ILogger<ClientesTools> _log = log;

    [McpServerTool(Name = "clientes_inativos")]
    [Description("Clientes que não compram há pelo menos N dias, do mais antigo para o mais recente. Devolve CSV com cliente, data da última compra e dias sem comprar.")]
    public async Task<string> InativosAsync(
        [Description("Número mínimo de dias sem comprar (1 a 3650).")] int dias = 90,
        CancellationToken ct = default)
    {
        var diasEfetivo = Math.Clamp(dias, 1, 3650);

        try
        {
            var linhas = await _vendasRepo.ObterInativosAsync(diasEfetivo, ct);

            var sb = new StringBuilder();
            sb.AppendLine("cliente,ultima_compra,dias_sem_comprar");

            foreach (var l in linhas)
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"{l.Cliente},{l.UltimaCompra:yyyy-MM-dd},{l.DiasSemComprar}"));
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha em clientes_inativos");
            return "Não foi possível obter os dados de clientes neste momento.";
        }
    }

    [McpServerTool(Name = "clientes_nomes")]
    [Description("Nomes de todos os clientes com vendas registadas, por ordem alfabética. Um nome por linha.")]
    public async Task<string> NomesAsync(CancellationToken ct = default)
    {
        try
        {
            var nomes = await _vendasRepo.ObterNomesClientesAsync(ct);
            return nomes.Count == 0
                ? "Sem clientes registados."
                : string.Join("\n", nomes);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha em clientes_nomes");
            return "Não foi possível obter os clientes neste momento.";
        }
    }
}
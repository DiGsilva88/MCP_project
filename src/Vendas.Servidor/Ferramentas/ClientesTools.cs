using System.ComponentModel;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;
using ModelContextProtocol.Protocol;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;


namespace Vendas.Servidor.Modelos;


[McpServerToolType]

public sealed class ClientesTools(

    IVendasRepo vendasRepo,
    ILogger<ClientesTools> log)

{
    private readonly IVendasRepo _vendasRepo = vendasRepo;
    private readonly ILogger<ClientesTools> _log = log;

    private const int MaxClientes = 50;


    [McpServerTool(Name = "listar-clientes")]
    [Description("Devolve uma lista de clientes registados no sistema")]

    public async Task<IReadOnlyList<VendaPorCliente>> ListarClientesAsync(
        [Description("Texto opcional para filtrar por nome do cliente.")] string? filtro = null,
        CancellationToken cancellationToken = default)
    {
        var clientes = await _vendasRepo.ObterTopClientesAsync(MaxClientes, cancellationToken);

        if (string.IsNullOrWhiteSpace(filtro))
        {
            return clientes;
        }

        var filtroNormalizado = filtro.Trim();
        return clientes
            .Where(c => c.Cliente.Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

    

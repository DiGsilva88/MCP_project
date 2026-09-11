using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Dados;

public sealed class RepoMemoria : IVendasRepo
{
    private static readonly VendaPorCliente[] Dados = 
    [
        new ("Cliente A", 10, 1000.00m),
        new ("Cliente B", 5, 500.00m),
        new ("Cliente C", 8, 800.00m),
        new ("Cliente D", 3, 300.00m),
        new ("Cliente E", 12, 1200.00m),
        new ("Cliente F", 7, 700.00m),
        new ("Cliente G", 15, 1500.00m),

    ];
    
       // Implementação do método ObterTopClientesAsync
    public Task<IReadOnlyList<VendaPorCliente>> ObterTopClientesAsync(
        int limite, 
        CancellationToken cancellationToken = default)


    {
        // Cria uma lista de clientes ordenada pelo total vendido em ordem decrescente
        IReadOnlyList<VendaPorCliente> resultado = Dados
        
            .OrderByDescending(v => v.TotalVendido)
            .Take(limite)
            .ToList()
            .AsReadOnly();



        return Task.FromResult(resultado);
   
    }
     }

       


    


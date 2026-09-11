using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Dados;

public class RepoMemoria : IVendasRepo
{
    private static readonly DateTime Base = new DateTime(2026, 3, 15);
    private  readonly List<Venda> _venda = 
    [
        // Adiciona algumas vendas de exemplo
        //
      new Venda(01, "Cliente A", "Produto X", 100.0m, 2, 200, Base.AddDays(-10)),
      new Venda(02, "Cliente B", "Produto Y", 50.0m, 1, 50, Base.AddDays(-5)),
      new Venda(03, "Cliente A", "Produto Z", 75.0m, 3, 225, Base.AddDays(-2)),
      new Venda(04, "Cliente C", "Produto X", 100.0m, 1, 100, Base.AddDays(-1)),
      new Venda(05, "Cliente B", "Produto Z", 75.0m, 2, 150, Base.AddDays(-3)),
      new Venda(06, "Cliente D", "Produto Y", 50.0m, 4, 200, Base.AddDays(-7)),
      new Venda(07, "Cliente E", "Produto X", 100.0m, 1, 100, Base.AddDays(-4)),
      new Venda(08, "Cliente A", "Produto Y", 50.0m, 2, 100, Base.AddDays(-6)),
      new Venda(09, "Cliente C", "Produto Z", 75.0m, 1, 75, Base.AddDays(-8)),
      new Venda(10, "Cliente D", "Produto X", 100.0m, 3, 300, Base.AddDays(-9))

    ];
    
    // Implementação do método ObterTopClientesAsync
    public Task<IReadOnlyList<VendaPorCliente>> ObterTopClientesAsync(
        int limite, 
        CancellationToken cancellationToken = default)


    { // Agrupa as vendas por cliente, calcula o número de vendas e o total vendido, ordena pelo total vendido e retorna os top clientes
       var resultado = _venda
            .GroupBy(v => v.Cliente)
            .Select(g => new VendaPorCliente(
                g.Key,
                g.Count(),
                g.Sum(v => v.Valor)))
            .OrderByDescending(v => v.TotalVendido)
            .Take(limite)
            .ToList();



        return Task.FromResult<IReadOnlyList<VendaPorCliente>>(resultado);

    }
    // Implementação do método AdicionarVendaAsync
    public Task<IReadOnlyList<Venda>> ListarVendasAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Venda>>(_venda);
    }
     }

       


    


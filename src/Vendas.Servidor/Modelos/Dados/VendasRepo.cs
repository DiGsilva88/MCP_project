using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Modelos.Dados;

public class VendasRepo : IVendasRepo
{
    private readonly List<Venda> _vendas= new List<Venda>();

    public VendasRepo()
    {
        // Adiciona algumas vendas de exemplo
        _vendas.Add(new Venda(1, "Cliente A", "Produto X", 100.0m, 2, DateTime.Now));
        _vendas.Add(new Venda(2, "Cliente B", "Produto Y", 50.0m, 1, DateTime.Now));
        _vendas.Add(new Venda(3, "Cliente C", "Produto Z", 75.0m, 3, DateTime.Now));
    }

    public Task<IReadOnlyList<Venda>> ListarVendasAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Venda>>(_vendas);
    }

    public Task<Venda?> ObterporIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var venda = _vendas.FirstOrDefault(v => v.Id == id);
        return Task.FromResult(venda);
    }

    public Task<IReadOnlyList<VendaPorCliente>> ObterTopClientesAsync(int limite, CancellationToken cancellationToken = default)
    {
        var resultado = _vendas
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

    public Task AdicionarVendaAsync(Venda venda, CancellationToken cancellationToken = default)
    {
        _vendas.Add(venda);
        return Task.CompletedTask;
    }

    public Task AtualizarVendaAsync(Venda venda, CancellationToken cancellationToken = default)
    {
        var index = _vendas.FindIndex(v => v.Id == venda.Id);
        if (index != -1)
        {
            _vendas[index] = venda;
        }
        return Task.CompletedTask;
    }

    public Task ExcluirVendaAsync(int id)
    {
        var venda = _vendas.FirstOrDefault(v => v.Id == id);
        if (venda != null)
        {
            _vendas.Remove(venda);
        }
        return Task.CompletedTask;
    }
}
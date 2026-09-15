using System.Runtime.Intrinsics;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Modelos.Dados;

public sealed class RepoMemoria : IVendasRepo
{
    private static readonly List<Venda> _venda = Gerar();

    private static List<Venda> Gerar()
    {

        string[] clientes =
         [
            "Padaria Central", "Supermercado Bom Preço", "Restaurante Saboroso", "Loja de Roupas Fashion", 
        "Farmácia Saúde", "Cafeteria Aroma", "Mercado Verde", "Pizzaria Delicatium", "Livraria Cultura", "Academia Fitness"
        ] ;

        string[] produtos =
         [
            "Pão integral", "Leite Integral", "Arroz Branco", "Frango Congelado", "Café Torrado",
        "Macarrão Espaguete", "Queijo", "Chocolate", "Refrigerante", "Suco Natural"
        ];

        

    int[] inativoHaMeses = [0, 0, 0, 0, 0, 0, 0, 4, 8, 14];


        var random = new Random(42);
        var hoje = DateTime.Today;
        var vendas = new List<Venda>();
        var id = 1;
        
        for (var mes =23; mes >= 0; mes--)

        {
         var inicioMes = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-mes);
         var diasNoMes = DateTime.DaysInMonth(inicioMes.Year, inicioMes.Month);

        var elegiveis = Enumerable.Range(0, clientes.Length)
                .Where(c => inativoHaMeses[c] == 0 || mes >= inativoHaMeses[c])
                .ToArray();


         var vendasNoMes = random.Next(5, 21); // Entre 5 e 20 vendas por mês, depois é alterado mediante o cliente


            for (var i = 0; i < vendasNoMes; i++)
            {
                var data =inicioMes.AddDays(random.Next(diasNoMes));
                if (data > hoje) continue;

                var valorUnitario = Math.Round((decimal)random.NextDouble() * 100m + 5m, 2); // Valor entre 5 e 105
                var quantidade = random.Next(1, 6);

            vendas.Add(new Venda(
                    id++,
                    clientes[elegiveis[random.Next(elegiveis.Length)]],
                    produtos[random.Next(produtos.Length)],
                    valorUnitario,
                    quantidade,
                    valorUnitario * quantidade,
                    data));
            }
    
    }

        Console.Error.WriteLine($"{vendas.Count} vendas | clientes distintos: {vendas.Select(v => v.Cliente).Distinct().Count()}");

        return vendas;
    }
    // Implementação do método AdicionarVendaAsync
    public Task<IReadOnlyList<Venda>> ListarVendasAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Venda>>(_venda);
    }

    // Implementação do método ObterInativosAsync
    public Task<IReadOnlyList<ClienteInativo>> ObterInativosAsync(
        int dias,
        CancellationToken cancellationToken = default)
    {
        var agora = DateTime.Now;

        var resultado = _venda
            .GroupBy(v => v.Cliente)
            .Select(g =>
            {
                var ultimaCompra = g.Max(v => v.DataVenda);
                return new ClienteInativo(g.Key, ultimaCompra, (agora - ultimaCompra).Days);
            })
            .Where(c => c.DiasSemComprar >= dias)
            .OrderBy(c => c.UltimaCompra)
            .ToList();

        return Task.FromResult<IReadOnlyList<ClienteInativo>>(resultado);
    }

    // Implementação do método ObterNomesClientesAsync
    public Task<IReadOnlyList<string>> ObterNomesClientesAsync(
        CancellationToken cancellationToken = default)
    {
        var resultado = _venda
            .Select(v => v.Cliente)
            .Distinct()
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(resultado);
    }

    // Agrupa as vendas por cliente, calcula o número de vendas e o total vendido, ordena pelo total vendido e retorna os top clientes
    public Task<IReadOnlyList<VendaPorCliente>> ObterTopClientesAsync(int limite,int dias, CancellationToken ct = default)
    {


        var desde = DateTime.Today.AddDays(-dias);

        var resultado = _venda
        .Where(v => v.DataVenda >= desde)
            .GroupBy(v => v.Cliente)
            .Select(g => new VendaPorCliente(
                g.Key,
                g.Count(),
                g.Sum(v => v.ValorLinha)))
            .OrderByDescending(v => v.TotalVendido)
            .Take(limite)
            .ToList();

        return Task.FromResult<IReadOnlyList<VendaPorCliente>>(resultado);
    }

    // Agrupa as vendas por produto, calcula o número de vendas e o total vendido, ordena pelo total vendido e retorna os top produtos
    

    public Task<IReadOnlyList<VendaPorProduto>> ObterTopProdutosAsync(
    int limite, int dias, CancellationToken ct = default)
{
    var limiar = DateTime.Today.AddDays(-dias);

    var resultado = _venda
        .Where(v => v.DataVenda >= limiar)
        .GroupBy(v => v.Produto)
        .Select(g => new VendaPorProduto(
            g.Key,
            g.Select(v => v.NumeroEncomenda).Distinct().Count(),
            g.Sum(v => v.ValorLinha)))
        .OrderByDescending(l => l.TotalVendido)
        .Take(limite)
        .ToList();

    return Task.FromResult<IReadOnlyList<VendaPorProduto>>(resultado);
}

public Task<IReadOnlyList<ContagemCliente>> ContarClientesAsync(
    DimensaoCliente agrupar, int limite, CancellationToken cancellationToken = default)
    => Task.FromResult<IReadOnlyList<ContagemCliente>>([]);
}

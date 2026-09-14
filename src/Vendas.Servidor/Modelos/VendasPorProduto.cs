namespace Vendas.Servidor.Modelos;

public record VendaPorProduto(
    string Produto,
    int NumeroVendas,
    decimal TotalVendido
);

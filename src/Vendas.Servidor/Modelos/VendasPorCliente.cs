namespace Vendas.Servidor.Modelos;

public record  VendaPorCliente(

    string Cliente,
    int NumeroVendas,
    decimal TotalVendido
);


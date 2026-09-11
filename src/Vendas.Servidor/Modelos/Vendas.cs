namespace Vendas.Servidor.Modelos;


public record Venda(
    int NumeroEncomenda, 
    String Cliente, 
    string Produto, 
    decimal Valor, 
    int Quantidade, 
    decimal ValorLinha,
    DateTime DataVenda);

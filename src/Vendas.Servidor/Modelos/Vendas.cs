namespace Vendas.Servidor.Modelos;


public record Venda(
    int Id, 
    String Cliente, 
    string Produto, 
    decimal Valor, 
    int Quantidade, 
    DateTime DataVenda);

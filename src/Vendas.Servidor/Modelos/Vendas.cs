namespace Vendas.Servidor.Modelos;


public record Venda(
    int id, 
    String Cliente, 
    string Produto, 
    decimal Valor, 
    int Quantidade, 
    DateTime DataVenda);

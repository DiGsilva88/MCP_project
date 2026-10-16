namespace Vendas.Servidor.Modelos;

//uma linha um ranking de vendas Ex: nome = "Casa Costa", valor= xxxx
//valor = soma do valor liquido (sem Iva, notas de crédito já descontadas)

//quantidade = soma das quantidades, documentos = quantas vendas diferentes, grupos=quantos grupos existem

public sealed record ValorAgrupado( 
    string Nome, decimal Valor, decimal Quantidade, int Documentos, decimal Total, int Grupos);
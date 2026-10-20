namespace Vendas.Servidor.Modelos;

//uma linha um ranking de vendas Ex: nome = "Casa Costa", valor= xxxx
//valor = soma do valor liquido (sem Iva, notas de crédito já descontadas)

//quantidade = soma das quantidades, documentos = quantas vendas diferentes, grupos=quantos grupos existem
//bruto = valor ilíquido (antes do desconto), desconto = negativo, iva = imposto sobre o líquido
//totalDocumentos = documentos distintos no período todo (igual em todas as linhas), para a média por venda

public sealed record ValorAgrupado(
    string Nome, decimal Valor, decimal Quantidade, int Documentos, decimal Total, int Grupos,
    decimal Bruto = 0, decimal Desconto = 0, decimal Iva = 0, int TotalDocumentos = 0);



namespace Vendas.Servidor.Modelos;

public record ClienteInativo(string Cliente, DateTime UltimaCompra, int DiasSemComprar);
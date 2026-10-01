namespace Vendas.Servidor.Modelos;

// Colunas de ViewMCP_cliente_sensivel (sem máscara). Ficam num enum à parte de Campo para a tool
// "consultar" nunca as poder pedir. Nova coluna: valor aqui + ConsultaClientes.ColunaSensivel.
public enum CampoSensivel
{
    Contribuinte,
    Email,
    Telefone,
    Morada,
    CodigoPostal,
    VolumeVendas,
    Plafond,
}

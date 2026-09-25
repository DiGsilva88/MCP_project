namespace Vendas.Servidor.Modelos;

// As views que o MCP pode ler. Para expor uma nova view: novo valor aqui + entrada em ConsultaClientes.Coluna.
public enum Campo
{
    //ficha cliente -viewmcp_cliente
    Zona,
    Vendedor,
    TipoCliente,
    Actividade,
    Distrito,

    //condições de pagamento
    Pagamento,
    Cobranca,
    Expedicao,
    SituacaoFinanceira,
    EscalaoPlafond,
    EscalaoVolumeVendas,
}

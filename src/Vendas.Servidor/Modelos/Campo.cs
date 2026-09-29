namespace Vendas.Servidor.Modelos;

// Colunas das views que o MCP pode ler. Para expor uma nova coluna: novo valor aqui + entrada em ConsultaClientes.Coluna.
public enum Campo
{
    //ficha do cliente
    Zona,
    Localidade,
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

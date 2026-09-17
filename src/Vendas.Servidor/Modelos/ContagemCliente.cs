


namespace Vendas.Servidor.Modelos;

public enum DimensaoCliente
{
    Zona,
    Vendendor,
    TipoCliente,
    Actividade,
    Distrito
}

public enum DimensaoFaturacao {
    Pagamento, Cobranca, SituacaoFinanceira,EscalaoPlafond
    }

public record ContagemCliente(string Valor, int Clientes, int Total, int Grupos);


    public  static class Dimensoes 
    {
        //Nome da coluna na VIEW.
        public static string Coluna(DimensaoCliente dimensao) => dimensao switch
    
    {
        DimensaoCliente.Zona => "Zona",
        DimensaoCliente.Vendendor => "Vendedor",
        DimensaoCliente.TipoCliente => "TipoCliente",
        DimensaoCliente.Actividade => "Actividade",
        DimensaoCliente.Distrito => "Distrito",
        _ => throw new ArgumentOutOfRangeException(nameof(dimensao), dimensao , null)
    };
    //nome da coluna no CSV devolvido ao modelo
    public static string Cabecalho(DimensaoCliente dimensao) => dimensao switch
   {
        DimensaoCliente.Zona => "zona",
        DimensaoCliente.Vendendor => "vendedor",
        DimensaoCliente.TipoCliente => "tipo_cliente",
        DimensaoCliente.Actividade => "actividade",
        DimensaoCliente.Distrito => "distrito",
        _ => throw new ArgumentOutOfRangeException(nameof(dimensao), dimensao , null)
    };
        
         public static string Coluna(DimensaoFaturacao dimensao) => dimensao switch
    
    {
        DimensaoFaturacao.Pagamento => "Pagamento",
        DimensaoFaturacao.Cobranca => "Cobranca",
        DimensaoFaturacao.SituacaoFinanceira => "SitFinanceira",
        DimensaoFaturacao.EscalaoPlafond => "EscalaoPlafond",
        
        _ => throw new ArgumentOutOfRangeException(nameof(dimensao), dimensao , null)
    };

    public static string Cabecalho(DimensaoFaturacao dimensao) => dimensao switch
   {
       DimensaoFaturacao.Pagamento => "condicao_pagamento",
        DimensaoFaturacao.Cobranca => "cobranca",
        DimensaoFaturacao.SituacaoFinanceira => "situacao_financeira",
        DimensaoFaturacao.EscalaoPlafond => "escalao_plafond",
        
        _ => throw new ArgumentOutOfRangeException(nameof(dimensao), dimensao , null)
    };



    }



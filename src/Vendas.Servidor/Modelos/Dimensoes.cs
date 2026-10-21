namespace Vendas.Servidor.Modelos;

// Localidade..Actividade vêm da ficha do cliente: vendas somadas pelo valor dessa coluna.
public enum DimensaoVenda {Cliente, Artigo, Familia, Marca, Mes, Localidade, Distrito, Zona, Vendedor, Actividade}

// Por onde se ordena o ranking: euros (líquido) ou unidades vendidas.
public enum OrdenarVenda {Valor, Quantidade}


//traduz a dimensao para o SQL e para a resposta
public static class Dimensoes
{

    public static string Cabecalho(DimensaoVenda dimensao) => dimensao switch
{
    DimensaoVenda.Cliente => "cliente",
    DimensaoVenda.Artigo => "artigo",
    DimensaoVenda.Familia => "familia",
    DimensaoVenda.Marca => "marca",
    DimensaoVenda.Mes => "mes",
    DimensaoVenda.Localidade => "localidade",
    DimensaoVenda.Distrito => "distrito",
    DimensaoVenda.Zona => "zona",
    DimensaoVenda.Vendedor => "vendedor",
    DimensaoVenda.Actividade => "actividade",
    _=> throw new ArgumentOutOfRangeException(nameof(dimensao))

};

    public static string Plural(DimensaoVenda dimensao) => dimensao switch
{
    DimensaoVenda.Cliente => "clientes",
    DimensaoVenda.Artigo => "artigos",
    DimensaoVenda.Familia => "famílias",
    DimensaoVenda.Marca => "marcas",
    DimensaoVenda.Mes => "meses",
    DimensaoVenda.Localidade => "localidades",
    DimensaoVenda.Distrito => "distritos",
    DimensaoVenda.Zona => "zonas",
    DimensaoVenda.Vendedor => "vendedores",
    DimensaoVenda.Actividade => "actividades",
    _=> throw new ArgumentOutOfRangeException(nameof(dimensao))

};

    // Coluna da ficha do cliente por onde se agrupa; null nas dimensões da própria venda.
    public static Campo? CampoDaFicha(DimensaoVenda dimensao) => dimensao switch
{
    DimensaoVenda.Localidade => Campo.Localidade,
    DimensaoVenda.Distrito => Campo.Distrito,
    DimensaoVenda.Zona => Campo.Zona,
    DimensaoVenda.Vendedor => Campo.Vendedor,
    DimensaoVenda.Actividade => Campo.Actividade,
    _ => null
};

}

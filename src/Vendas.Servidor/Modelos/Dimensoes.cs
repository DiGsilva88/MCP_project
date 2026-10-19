namespace Vendas.Servidor.Modelos;

public enum DimensaoVenda {Cliente, Artigo, Familia, Marca, Mes}


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
    _=> throw new ArgumentOutOfRangeException(nameof(dimensao))

};

    public static string Plural(DimensaoVenda dimensao) => dimensao switch
{
    DimensaoVenda.Cliente => "clientes",
    DimensaoVenda.Artigo => "artigos",
    DimensaoVenda.Familia => "famílias",
    DimensaoVenda.Marca => "marcas",
    DimensaoVenda.Mes => "meses",
    _=> throw new ArgumentOutOfRangeException(nameof(dimensao))

};

}

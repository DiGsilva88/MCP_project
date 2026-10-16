namespace Vendas.Servidor.Modelos;

public enum DimensaoArtigo {Artigo, Familia, Marca}


//traduz a dimensao para o SQL e para a resposta
public static class Dimensoes 
{
public static string Coluna(DimensaoArtigo dimensao) => dimensao switch
{
    DimensaoArtigo.Artigo => "Artigo",
    DimensaoArtigo.Familia => "Família",
    DimensaoArtigo.Marca => "Marca",

    _=> throw new ArgumentOutOfRangeException(nameof(dimensao))

};

public static string Cabecalho(DimensaoArtigo dimensao) => dimensao switch
{
    DimensaoArtigo.Artigo => "artigo",
    DimensaoArtigo.Familia => "família",
    DimensaoArtigo.Marca => "marca",
    _=> throw new ArgumentOutOfRangeException(nameof(dimensao))

};

public static string Plural(DimensaoArtigo dimensao) => dimensao switch
{
    DimensaoArtigo.Artigo => "artigos",
    DimensaoArtigo.Familia => "famílias",
    DimensaoArtigo.Marca => "marcas",
    _=> throw new ArgumentOutOfRangeException(nameof(dimensao))

};

}
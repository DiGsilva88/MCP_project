namespace Vendas.Servidor.Modelos;

// Localidade..Actividade vêm da ficha do cliente: vendas somadas pelo valor dessa coluna.
public enum DimensaoVenda { Cliente, Artigo, Familia, Marca, Mes, Trimestre, Localidade, Distrito, Zona, Vendedor, Actividade }

// Por onde se ordena o ranking: euros (líquido) ou unidades vendidas.
public enum OrdenarVenda { Valor, Quantidade }


//traduz a dimensao para o SQL e para a resposta
public static class Dimensoes
{

    public static string Cabecalho(DimensaoVenda dimensao) => Nomes(dimensao).Singular;

    public static string Plural(DimensaoVenda dimensao) => Nomes(dimensao).Plural;

    // Mes e Trimestre: mostram-se por ordem do tempo e sempre completos (é uma evolução, não um top).
    public static bool Cronologica(DimensaoVenda? dimensao) => dimensao is DimensaoVenda.Mes or DimensaoVenda.Trimestre;

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

    private static (string Singular, string Plural) Nomes(DimensaoVenda dimensao) => dimensao switch

    {
        DimensaoVenda.Cliente => ("cliente", "clientes"),
        DimensaoVenda.Artigo => ("artigo", "artigos"),
        DimensaoVenda.Familia => ("familia", "famílias"),
        DimensaoVenda.Marca => ("marca", "marcas"),
        DimensaoVenda.Mes => ("mês", "meses"),
        DimensaoVenda.Trimestre => ("trimestre", "trimestres"),
        DimensaoVenda.Localidade => ("localidade", "localidades"),
        DimensaoVenda.Distrito => ("distrito", "distritos"),
        DimensaoVenda.Zona => ("zona", "zonas"),
        DimensaoVenda.Vendedor => ("vendedor", "vendedores"),
        DimensaoVenda.Actividade => ("actividade", "actividades"),
        _ => throw new ArgumentOutOfRangeException(nameof(dimensao))
    };
}


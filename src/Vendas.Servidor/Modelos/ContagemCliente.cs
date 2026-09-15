


namespace Vendas.Servidor.Modelos;

public enum DimensaoCliente
{
    Zona,
    Vendendor,
    TipoCliente,
    Actividade,
    Distrito
}

public record ContagemCliente(string Valor, int Clientes);


    public  static class Dimensoes 
    {

        public static string Coluna(DimensaoCliente dimensao) => dimensao switch
    
    {
        DimensaoCliente.Zona => "Zona",
        DimensaoCliente.Vendendor => "Vendedor",
        DimensaoCliente.TipoCliente => "TipoCliente",
        DimensaoCliente.Actividade => "Actividade",
        DimensaoCliente.Distrito => "Distrito",
        _ => throw new ArgumentOutOfRangeException(nameof(dimensao), dimensao , null)
    };

    }



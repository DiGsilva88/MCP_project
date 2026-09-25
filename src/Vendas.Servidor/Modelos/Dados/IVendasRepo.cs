using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Modelos.Dados;

//O "contrato" entre as ferramentas e a origem dos dados.
// Os filtros já chegam validados (só campos da lista branca)

public interface IVendasRepo
{
   
   //quantos clientes existem em cada valor no campo, só entra os que cumprem os filtros
   //exemplo agrupar= vendendor , filtros {zona: norte} - clientes do norte agrupados por vendendor
    Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        Campo agrupar, IReadOnlyDictionary<Campo, string> filtros, int limite,
        CancellationToken cancellationToken = default);
    

    //clientes que cumprem os filtros, com os campos pedidos

    Task<PaginaClientes> ListarAsync(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, string? nome, int deslocamento, int limite, CancellationToken cancellationToken = default);
        
        
    
}

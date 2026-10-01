using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Modelos.Dados;

// O "contrato" entre as ferramentas e a origem dos dados.
// Os filtros já chegam validados (só campos da lista branca).
public interface IVendasRepo
{
    // Quantos clientes existem em cada valor do campo, só entram os que cumprem os filtros.
    // Ex.: agrupar=Zona, filtros {Pagamento: 30 dias} -> clientes a 30 dias agrupados por zona.
    Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        Campo agrupar, IReadOnlyDictionary<Campo, string> filtros, int limite,
        CancellationToken cancellationToken = default);

    // Clientes que cumprem os filtros, com os campos pedidos.
    Task<PaginaClientes> ListarAsync(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, int deslocamento, int limite,
        CancellationToken cancellationToken = default);

    // Como ListarAsync, mas com um dado da view sensível (NIF, contactos, valores reais).
    // maiores=true ordena esse dado do maior para o menor (só colunas numéricas).
    Task<PaginaClientes> ListarSensivelAsync(
        CampoSensivel mostrar, IReadOnlyDictionary<Campo, string> filtros, bool maiores, int deslocamento, int limite,
        CancellationToken cancellationToken = default);
}

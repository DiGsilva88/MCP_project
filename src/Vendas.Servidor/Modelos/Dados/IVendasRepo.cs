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

    // Clientes que cumprem os filtros, com os campos pedidos. nome: só clientes cujo nome começa por esse texto.
    Task<PaginaClientes> ListarAsync(
        IReadOnlyList<Campo> mostrar, IReadOnlyDictionary<Campo, string> filtros, int deslocamento, int limite,
        string? nome = null, CancellationToken cancellationToken = default);

    // Como ListarAsync, mas com um dado da view sensível (NIF, contactos, valores reais).
    // maiores=true ordena esse dado do maior para o menor (só colunas numéricas).
    Task<PaginaClientes> ListarSensivelAsync(
        CampoSensivel mostrar, IReadOnlyDictionary<Campo, string> filtros, bool maiores, int deslocamento, int limite,
        string? nome = null, Campo? tambem = null, CancellationToken cancellationToken = default);

    // Clientes com esse ID ou cujo nome começa pelo texto (o ID exato primeiro), no máximo "limite".
    Task<IReadOnlyList<(string Id, string Nome)>> ProcurarClientesAsync(
        string cliente, int limite, CancellationToken cancellationToken = default);

    // Rankings de vendas no período [desde, ate[. cliente (opcional): só as vendas desse cliente (ID exato).
    // filtros (opcional): só clientes com esses valores na ficha (ex.: Localidade = Seixal).
    Task<IReadOnlyList<ValorAgrupado>> RankingPorClienteAsync(
        DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, CancellationToken cancellationToken = default);

    // porQuantidade: ordena por unidades vendidas em vez de valor (o Mes fica sempre por ordem cronológica).
    Task<IReadOnlyList<ValorAgrupado>> RankingPorDimensaoAsync(
        DimensaoVenda dimensao, DateOnly desde, DateOnly ate, int limite, string? cliente = null,
        IReadOnlyDictionary<Campo, string>? filtros = null, bool porQuantidade = false,
        CancellationToken cancellationToken = default);

    // Clientes da ficha que não têm vendas em [desde, ate[, com a última compra anterior.
    Task<IReadOnlyList<ClienteSemVendas>> ClientesSemVendasAsync(
        DateOnly desde, DateOnly ate, int limite, IReadOnlyDictionary<Campo, string>? filtros = null,
        CancellationToken cancellationToken = default);

    // Data da venda mais recente que existe (null = sem vendas nenhumas).
    Task<DateOnly?> UltimaVendaAsync(CancellationToken cancellationToken = default);
}

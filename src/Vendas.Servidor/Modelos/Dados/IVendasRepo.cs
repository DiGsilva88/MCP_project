using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Modelos.Dados;

//O "contrato" entre as ferramentas e a origem dos dados.
// As colunas recebidas já vêm validadas por Vistas.Coluna.

public interface IVendasRepo
{
    // Linhas da view (todas as colunas), opcionalmente filtradas por coluna = valor exato.
    Task<PaginaVista> ConsultarAsync(
        Vista vista, string? colunaFiltro, string? valor, int limite, CancellationToken cancellationToken = default);

    // Quantas linhas há em cada valor da coluna. Ex.: Lisboa 120, Porto 95
    Task<IReadOnlyList<ContagemCliente>> ContarAsync(
        Vista vista, string coluna, int limite, CancellationToken cancellationToken = default);
}

namespace Vendas.Servidor.Modelos;


//resultado de uma listagem
public sealed record PaginaClientes(
    IReadOnlyList<string> Colunas, IReadOnlyList<string[]> Linhas, int Total);

    

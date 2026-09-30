
using Vendas.Servidor.Modelos.Dados;
using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Tests.Dados;

//falam com a base de dados,correm só quando é pedido
//

[Trait("Categoria", "Integração")]
public class RepoSqlIntegracaoTests
{
  
    private static RepoSql CriarRepo()
    {
        var ligacao = Environment.GetEnvironmentVariable("VENDAS_SQL_TESTES");
        Assert.False(string.IsNullOrEmpty(ligacao), "Defina VENDAS_SQL_TESTES para correr estes testes.");
        return new RepoSql(ligacao);

       
    }

    [Fact]
    public async Task Contar_Por_Zona_Soma0TotalDeClientes()
    {
        var linhas = await CriarRepo().ContarAsync(Campo.Zona, new Dictionary<Campo, string>(),limite:100);

        Assert.NotEmpty(linhas);
        Assert.Equal(linhas[0].Total, linhas.Sum(l =>l.Clientes)); //só é igual se Grupos <= limite
    }

    [Fact]
    public async Task Listar_DuasPaginas_NaoRepeteClientes()
    {
        var repo =CriarRepo();
        var sem = new Dictionary<Campo, string>();

        var p1 = await repo.ListarAsync([Campo.Zona],sem, deslocamento: 0,limite:20);
        var p2 = await repo.ListarAsync([Campo.Zona],sem, deslocamento:20, limite:20);

        Assert.Empty(p1.Linhas.Select(l => l[0]).Intersect(p2.Linhas.Select(l => l[0])));

    }

}

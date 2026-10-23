
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

        // Mesma regra do servidor: sem Password= na ligação, vem do sql.pwd (DPAPI)
        var cs = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ligacao);
        var ficheiroPwd = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vendas", "sql.pwd");
        if (OperatingSystem.IsWindows() && string.IsNullOrEmpty(cs.Password) && File.Exists(ficheiroPwd))
            cs.Password = System.Text.Encoding.Unicode.GetString(
                System.Security.Cryptography.ProtectedData.Unprotect(
                    Convert.FromHexString(File.ReadAllText(ficheiroPwd).Trim()), null,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser));
        return new RepoSql(cs.ConnectionString);
    }

    [FactComBd]
    public async Task Contar_Por_Zona_Soma0TotalDeClientes()
    {
        var linhas = await CriarRepo().ContarAsync(Campo.Zona, new Dictionary<Campo, string>(), limite: 100);

        Assert.NotEmpty(linhas);
        Assert.Equal(linhas[0].Total, linhas.Sum(l => l.Clientes)); //só é igual se Grupos <= limite
    }

    [FactComBd]
    public async Task Listar_DuasPaginas_NaoRepeteClientes()
    {
        var repo = CriarRepo();
        var sem = new Dictionary<Campo, string>();

        var p1 = await repo.ListarAsync([Campo.Zona], sem, deslocamento: 0, limite: 20);
        var p2 = await repo.ListarAsync([Campo.Zona], sem, deslocamento: 20, limite: 20);

        Assert.Empty(p1.Linhas.Select(l => l[0]).Intersect(p2.Linhas.Select(l => l[0])));

    }

    // Agrupar pela ficha não pode perder nem duplicar vendas: o total tem de ser o do ranking por mês.
    [FactComBd]
    public async Task VendasPorDistrito_TotalIgualAoDosMeses()
    {
        var repo = CriarRepo();
        var (desde, ate) = (new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1));

        var distritos = await repo.RankingPorDimensaoAsync(DimensaoVenda.Distrito, desde, ate, limite: 100);
        var meses = await repo.RankingPorDimensaoAsync(DimensaoVenda.Mes, desde, ate, limite: 100);

        Assert.NotEmpty(distritos);
        Assert.Equal(meses[0].Total, distritos[0].Total);
        Assert.Equal(meses[0].TotalDocumentos, distritos[0].TotalDocumentos);
        Assert.NotNull(meses[0].UltimaData);
    }

    // Por quantidade: a 1.ª linha é a de mais unidades.
    [FactComBd]
    public async Task ArtigosPorQuantidade_OrdenadosPorUnidades()
    {
        var linhas = await CriarRepo().RankingPorDimensaoAsync(
            DimensaoVenda.Artigo, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1), limite: 5, porQuantidade: true);

        Assert.Equal(linhas.Max(l => l.Quantidade), linhas[0].Quantidade);
    }

    [FactComBd]
    public async Task ClientesSemVendas_EUltimaVenda_Correm()
    {
        var repo = CriarRepo();
        var semVendas = await repo.ClientesSemVendasAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 1), limite: 5);

        Assert.NotNull(await repo.UltimaVendaAsync());
        // A última compra de quem não comprou no período é sempre anterior ao período.
        Assert.All(semVendas, c => Assert.True(c.Ultima is null || c.Ultima < new DateOnly(2026, 6, 1)));
    }

}

// Sem VENDAS_SQL_TESTES o teste aparece como saltado em vez de falhado (ex.: Test Explorer, que não usa o filtro do CI).
public sealed class FactComBdAttribute : FactAttribute
{
    public FactComBdAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VENDAS_SQL_TESTES")))
            Skip = "Defina VENDAS_SQL_TESTES para correr estes testes.";
    }
}

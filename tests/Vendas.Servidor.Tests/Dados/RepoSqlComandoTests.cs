using Microsoft.Data.SqlClient;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Tests.Dados;

public class RepoSqlComandoTests
{
    // Com a máquina ocupada (ex.: o modelo local a gerar), as consultas do resumo passavam os 30 s do SqlCommand
    // por omissão e falhavam com "Execution Timeout Expired" (Error Number -2): o painel mostrava "Não foi possível carregar os dados".
    [Fact]
    public void Consulta_TemTempoLimiteMaiorQueOsTrintaSegundosPorOmissao()
    {
        using var ligacao = new SqlConnection();

        using var comando = RepoSql.NovoComando("SELECT 1", ligacao);

        Assert.Equal(120, comando.CommandTimeout);
    }
}

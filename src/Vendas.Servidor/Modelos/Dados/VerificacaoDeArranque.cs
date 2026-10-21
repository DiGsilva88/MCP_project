using System.Data;
using Microsoft.Data.SqlClient;


namespace Vendas.Servidor.Modelos.Dados;

//corre uma vez no arranque, confirma antes do servidor aceitar perguntas
//Se alguma view mostrar uma coluna a mais(ex: um NIF) ou nennhuma coluna (Grant perdido), lança uma exeção e o servidor não arranca

public static class VerificacaoDeArranque
{
    //nome schema - a sunicas colunas que essa view pode ter
    private static readonly Dictionary<string, string []> Permitidas = new()
    {
        ["mcp.ViewMCP_cliente"]= ["ClienteID", "NomeCliente", "Zona", "Vendedor","TipoCliente", "Actividade","Distrito"],

        ["mcp.ViewMCP_cliente_faturacao"] = ["ClienteID", "NomeCliente","Pagamento","Cobranca","Expedicao","SitFinanceira","EscalaoPlafond", "EscalaoVolumeVendas"],

        ["mcp.ViewMCP_ViewMCP_cliente_sensivel"]=["ClienteID", "NomeCliente","ContribuinteID","Email","Telefone","VolumeVendas","Plafond"],

        ["mcp.ViewMCP_vendas"]=["ClienteID", "DocumentoID","TipoDocID","DataDoc","ArtigoID","Artigo","Familia","Quantidade","ValorLiquido","ValorIva","ValorIliquido","QuantidadeHomogenizada"]


    };

    public static async Task VerificarAsync(string ligacao, CancellationToken cancellationToken= default)

    {
        await using var ligacaoSql = new SqlConnection(ligacao);
        await ligacaoSql.OpenAsync(cancellationToken);

        foreach(var(view, permitidas)in Permitidas)
        {
            //aqui fica o nome da view pode ser parametro : o object_id recebe um valor (texto)
            await using var comando = new SqlCommand(
                "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@view);",ligacaoSql);
            comando.Parameters.Add("@view", SqlDbType.NVarChar, 256).Value = view;

            var visiveis = new List<string>();
            await using(var leitor = await comando.ExecuteReaderAsync(cancellationToken))
            {
                while(await leitor.ReadAsync(cancellationToken))
                visiveis.Add(leitor.GetString(0));
            }

            if (visiveis.Count == 0)
                throw new InvalidOperationException(
                    $"{view}: nenhuma coluna visivel. A view não existe ou falta o GRANT");
                
                //esta base- ORDINAL distingue maiusculas (Cobranca / cobrança)
                var aMais = visiveis.Except(permitidas,StringComparer.Ordinal).ToList();
                if (aMais.Count >0)
                throw new InvalidOperationException(
                    $"{view}: colunas não autorizadas: {string.Join(",",aMais)}."
                );
            
        }
    }
}
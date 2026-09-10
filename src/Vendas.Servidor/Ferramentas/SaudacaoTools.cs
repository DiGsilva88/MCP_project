using System.ComponentModel;
using ModelContextProtocol.Server;


namespace Vendas.Servidor.Ferramentas;

[McpServerToolType]
public static class SaudacaoTools
{
    [McpServerTool(Name = "Saudacao")]
     [Description ("Retorna uma saudação para o nome fornecido.Serve para confirmar que a ligação com o servidor está a funcionar.")]
    
    public static string Saudacao([Description ("O nome da pessoa a saudar")] string nome)
    {
        return $"Olá, {nome} . O servidor está a funcionar!";
    }
}

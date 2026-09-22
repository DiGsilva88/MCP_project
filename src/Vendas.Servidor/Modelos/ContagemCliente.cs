namespace Vendas.Servidor.Modelos;

// As views que o MCP pode ler. Para expor uma nova view: novo valor aqui + entrada em Vistas.
public enum Campo
{

    //ficha cliente -viewmcp_cliente
    Zona,
    Vendedor,
    TipoCliente,
    Actividade,
    Distrito,

    //condições de pagamento
    Pagamento,
    Cobranca,
    Expedicao,
    SituacaoFinanceira,
    EscalaoPlafond,
    EscalaoVolumeVendas,
}




public record ContagemCliente(string Valor, int Clientes, int Total, int Grupos);


public static class Campos
{
    public const string SemDados = "sem dados";

    // Nome das colunas permitidas: única fonte do que entra no SQL (nunca texto do modelo).
    // c = dbo.ViewMCP_cliente, f = dbo.ViewMCP_cliente_faturacao (ver ConsultaClientes).
public static string Coluna(Campo campo) => campo switch
{
    Campo.Zona => "c.[Zona]",
    Campo.Vendedor => "c.[Vendedor]",
    Campo.TipoCliente => "c.[TipoCliente]",
    Campo.Actividade => "c.[Actividade]",
    Campo.Distrito => "c.[Distrito]",
    Campo.Pagamento => "f.[Pagamento]",
    Campo.Cobranca => "f.[Cobranca]",
    Campo.Expedicao => "f.[Expedicao]",
    Campo.SituacaoFinanceira => "f.[SitFinanceira]",
    Campo.EscalaoPlafond => "f.[EscalaoPlafond]",
    Campo.EscalaoVolumeVendas => "f.[EscalaoVolumeVendas]",
    _ => throw new ArgumentOutOfRangeException(nameof(campo), campo,null)
};

// NULL, vazio e os marcadores das views ("(sem distrito)", "0 - sem plafond", ...) -> "sem dados".
public static string Limpar(string? valor)
{
    var v = valor?.Trim();
    return string.IsNullOrEmpty(v) || v.StartsWith("(sem ", StringComparison.OrdinalIgnoreCase)
        || v.StartsWith("0 - sem ", StringComparison.OrdinalIgnoreCase) ? SemDados : v;
}

public static string Validos => string.Join(", ", Enum.GetNames<Campo>());

//converte os filtros do modelo(Zona...) em campos de lista branca
public static Dictionary<Campo, string> LerFiltros(IDictionary<string, string>? filtros)
    {
        var lidos = new Dictionary<Campo, string>();
        foreach(var(chave, valor) in filtros ?? new Dictionary<string, string>())
        {
            //ignora as maisculas ,só nomes exatos
            var nome =Enum.GetNames<Campo>().FirstOrDefault(n => n.Equals(chave?.Trim(),StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($" Campo desconhecido em filtros: '{chave}'.Campos válidos: {Validos}");

            var campo = Enum.Parse<Campo>(nome);

            if(!string.IsNullOrWhiteSpace(valor))
            lidos[campo] = valor.Trim();
            
        
    }
    return lidos;
 


}

}

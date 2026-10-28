//os periodos que o modelo pode pedir
//lista fechada de periodos:o modelo escolhe, o servidor calcula as datas

using System.Globalization;

namespace Vendas.Servidor.Modelos;

// Lista fechada de períodos: o modelo escolhe, o servidor calcula as datas.
public enum Periodo
{
    Ultimos7Dias,
    Ultimos30Dias,
    Ultimos90Dias,
    Ultimos12Meses,
    AnoAtual,
    AnoAnterior,
    MesAtual,
    MesAnterior,
    TrimestreAtual,
    TrimestreAnterior
}


// Intervalo [Desde, Ate[: "Ate" não está incluído. Descricao é o nome do período para a resposta
public readonly record struct IntervaloVendas(DateOnly Desde, DateOnly Ate, string Descricao);
public static class Periodos
{
    private const string FormatoData = "dd-MM-yyyy";
    internal static readonly string[] Meses =
  ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];
    //mes /ano concretos


    // Traduz o que o modelo pediu num intervalo, por esta prioridade: dataInicio/dataFim, trimestre, mes/ano, periodo.
    // Erro (uma linha, para o modelo corrigir e chamar de novo) em vez de exceção: Erro != null quando o pedido é inválido.
    public static (IntervaloVendas? Intervalo, string? Erro) Resolver(
     Periodo periodo, int? mes, int? ano, string? dataInicio, string? dataFim, DateOnly hoje, int? trimestre = null)
    {
        if (ErroMesEAno(mes, ano, hoje) is { } erro) return (null, erro);
        if (trimestre is < 1 or > 4) return (null, "trimestre tem de ser de 1 a 4.");
        if (trimestre is not null && mes is not null) return (null, "mes e trimestre não se combinam. Chama de novo só com um.");

        if (dataInicio is not null || dataFim is not null)
            return ResolverDatas(dataInicio, dataFim);

        // O modelo pequeno não fazia as contas às datas de "1.º trimestre": escolhia TrimestreAtual.
        if (trimestre is { } t)
        {
            // Sem ano, um trimestre que ainda não começou é o do ano passado (como o mes).
            var a = ano ?? (t > (hoje.Month + 2) / 3 ? hoje.Year - 1 : hoje.Year);
            var desde = new DateOnly(a, (t - 1) * 3 + 1, 1);
            return (new IntervaloVendas(desde, desde.AddMonths(3), $"{t}.º trimestre de {a}"), null);
        }

        if (mes is not null || ano is not null)
        {
            var (desde, ate, descricao) = IntervaloMes(mes, ano, hoje);
            return (new IntervaloVendas(desde, ate, descricao), null);
        }

        var (inicio, fim) = Intervalo(periodo, hoje);
        return (new IntervaloVendas(inicio, fim, Descricao(periodo)), null);
    }

    private static string? ErroMesEAno(int? mes, int? ano, DateOnly hoje)
    {
        if (mes is < 1 or > 12) return "mes tem de ser de 1 a 12.";
        if (ano is < 2000 || ano > hoje.Year) return $"ano tem de ser de 2000 a {hoje.Year}.";
        return null;
    }

    private static (IntervaloVendas? Intervalo, string? Erro) ResolverDatas(string? dataInicio, string? dataFim)
    {
        if (dataInicio is null || dataFim is null)
            return (null, "dataInicio e dataFim têm de ser usados juntos.");

        if (!DateOnly.TryParseExact(dataInicio.Trim(), FormatoData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio)
            || !DateOnly.TryParseExact(dataFim.Trim(), FormatoData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fim))
            return (null, "Datas no formato dd-MM-aaaa, ex.: 01-08-2026.");

        if (fim < inicio) return (null, "dataFim tem de ser igual ou depois de dataInicio.");

        // "Ate" é exclusivo: soma-se um dia à data final pedida.
        return (new IntervaloVendas(inicio, fim.AddDays(1), "período indicado"), null);
    }



    // Devolve [desde, ate[. Exemplo: mês anterior a 15-10-2026 = 01-09-2026 a 01-10-2026.
    // Recebe "hoje" para se poder testar com outras datas.
    public static (DateOnly desde, DateOnly ate) Intervalo(Periodo periodo, DateOnly hoje)
    {
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var inicioAno = new DateOnly(hoje.Year, 1, 1);
        var amanha = hoje.AddDays(1);
        var inicioTrimestre = new DateOnly(hoje.Year, (hoje.Month - 1) / 3 * 3 + 1, 1);

        return periodo switch
        {
            Periodo.Ultimos7Dias => (hoje.AddDays(-6), amanha),
            Periodo.Ultimos30Dias => (hoje.AddDays(-29), amanha),
            Periodo.Ultimos90Dias => (hoje.AddDays(-89), amanha),
            Periodo.AnoAtual => (inicioAno, amanha),
            Periodo.MesAtual => (inicioMes, amanha),
            Periodo.AnoAnterior => (inicioAno.AddYears(-1), inicioAno),
            Periodo.MesAnterior => (inicioMes.AddMonths(-1), inicioMes),
            Periodo.Ultimos12Meses => (hoje.AddMonths(-12).AddDays(1), amanha),
            Periodo.TrimestreAtual => (inicioTrimestre, amanha),
            // "último trimestre" = o último trimestre completo do calendário
            Periodo.TrimestreAnterior => (inicioTrimestre.AddMonths(-3), inicioTrimestre),
            _ => throw new ArgumentOutOfRangeException(nameof(periodo))

        };
    }

    //mes e ano concretos
    public static (DateOnly desde, DateOnly ate, string descricao) IntervaloMes(int? mes, int? ano, DateOnly hoje)

    {
        if (mes is null)
        {
            var inicioAno = new DateOnly(ano!.Value, 1, 1);
            return (inicioAno, inicioAno.AddYears(1), $"ano {ano}");
        }
        var a = ano ?? (mes > hoje.Month ? hoje.Year - 1 : hoje.Year);
        var desde = new DateOnly(a, mes.Value, 1);
        return (desde, desde.AddMonths(1), $"{Meses[mes.Value - 1]} de {a}");
    }
    //o nome do periodo para a frase de resposta
    public static string Descricao(Periodo periodo) => periodo switch
    {
        Periodo.Ultimos7Dias => "últimos 7 dias",
        Periodo.Ultimos30Dias => "últimos 30 dias",
        Periodo.Ultimos90Dias => "últimos 90 dias",
        Periodo.Ultimos12Meses => "últimos 12 meses",
        Periodo.MesAtual => "mês atual",
        Periodo.AnoAtual => "ano atual",
        Periodo.MesAnterior => "mês anterior",
        Periodo.AnoAnterior => "ano anterior",
        Periodo.TrimestreAtual => "trimestre atual",
        Periodo.TrimestreAnterior => "trimestre anterior",
        _ => throw new ArgumentOutOfRangeException(nameof(periodo))

    };

}














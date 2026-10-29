
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Vendas.Servidor.Modelos;
using Vendas.Servidor.Modelos.Dados;

namespace Vendas.Servidor.Ferramentas;

//os que as tools partilham:limites, a frase de erro,o CSV e a execução co log
internal static class Respostas
{
    internal const int Maxlinhas = 100;

    //frase devolvida ao modelo quando algo falta.Nada revela SQL

    internal const string ErroNeutro = "Não foi possível consultar os dados neste momento";

    //máximo de valores existentes devolvidos quando o filtro não bate (poupa um turno ao modelo)

    private const int MaxValores = 30;

    //corre a consulta de uma tool. Cancelar não é uma varia e passa, qualquer outro erro vai para o log
    // o modelo recebe só o ErroNeutro. "detalhe" são os parâmetros sem dados pessoais.

    internal static async Task<string> ExecutarAsync(
    ILogger log, string tool, object? detalhe, Func<Task<string>> consulta)

    {
        var relogio = Stopwatch.StartNew();

        try
        {
            return await consulta();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falha em {Tool} ({Detalhe})", tool, detalhe);
            return ErroNeutro;
        }
        finally
        {
            log.LogInformation("{Tool} {Detalhe} em {Ms} ms", tool, detalhe, relogio.ElapsedMilliseconds);
        }
    }

    // Protege o CSV: um valor com o separador, aspas ou quebra de linha vai entre aspas.

    internal static string EscaparCsv(string texto, char separador = ',')
    => texto.IndexOfAny([separador, '"', '\n', '\r']) < 0 ? texto : $"\"{texto.Replace("\"", "\"\"")}\"";

    // "Zona = A e Distrito = B": os filtros tal como o modelo os pediu.
    internal static string DescreverFiltros(IEnumerable<KeyValuePair<Campo, string>> filtros)
        => string.Join(" e ", filtros.Select(f => $"{f.Key} = {f.Value}"));

    // Filtro sem match: devolve já os valores existentes de cada coluna filtrada, para o modelo
    // não gastar outra chamada só a descobri-los.
    // (Antes listava sempre todas as colunas e o modelo apresentava as contagens globais como resposta.)

    internal static async Task<string> SemCorrespondenciaAsync(
        IVendasRepo repo, Dictionary<Campo, string> filtros, CancellationToken cancellationToken)
    {
        var inexistentes = filtros;
        if (filtros.Count > 1)
        {

            // Uma consulta por filtro, em paralelo (cada chamada ao repo abre a sua ligação)
            var existe = await Task.WhenAll(filtros.Select(async f =>
            (f.Key, f.Value, Existe: (await repo.ContarAsync(f.Key, new Dictionary<Campo, string> { [f.Key] = f.Value }, 1, cancellationToken)).Count > 0)));
            inexistentes = existe.Where(e => !e.Existe).ToDictionary(e => e.Key, e => e.Value);

            if (inexistentes.Count == 0)
                return $"0 clientes com {DescreverFiltros(filtros)}:" + "os valores existem.mas nenhum cliente tem os dois.";
        }

        var texto = new StringBuilder(filtros.Count == 1
            ? "Nenhuma correspondência. O valor tem de ser um dos existentes (entre parênteses, nº de clientes):\n"
            : $"Nenhuma correspondência: {DescreverFiltros(inexistentes)} não existe. " +
              "Valores existentes (entre parênteses, nº de clientes no total, não nesta combinação):\n");

        foreach (var campo in inexistentes.Keys)
        {
            var valores = await repo.ContarAsync(campo, new Dictionary<Campo, string>(), MaxValores, cancellationToken);
            texto.Append($"{campo}: {string.Join("; ", valores.Select(v => $"{EscaparCsv(v.Valor)} ({v.Clientes})"))}");
            if (valores.Count > 0 && valores[0].Grupos > valores.Count)
                texto.Append($" (mostrados {valores.Count} de {valores[0].Grupos})");
            texto.Append('\n');
        }

        return texto.ToString();

    }

    // Quantos clientes se mostram quando o texto serve a vários.
    private const int MaxClientes = 5;

    // O cliente tem de existir e ser um só: senão "Sem vendas" ou uma soma de vários clientes
    // pareceriam a resposta de um cliente (e na sensível, os contactos de vários). Sem texto: sem filtro de cliente.
    internal static async Task<(string? Id, string? Rotulo, string? Erro)> ResolverClienteAsync(
        IVendasRepo repo, string? cliente, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cliente)) return (null, null, null);

        var texto = cliente.Trim();
        var encontrados = await repo.ProcurarClientesAsync(texto, MaxClientes + 1, ct);
        if (encontrados.Count == 0)
            return (null, null, $"Cliente \"{texto}\" não encontrado. Indique o ID ou o início do nome.");

        // Um ID exato ganha aos nomes que começam pelo mesmo texto.
        if (encontrados.Count > 1 && !encontrados[0].Id.Equals(texto, StringComparison.OrdinalIgnoreCase))
            return (null, null, $"Vários clientes começam por \"{texto}\". Indique o ID de um deles:\nid,cliente\n" +
                   string.Join('\n', encontrados.Take(MaxClientes).Select(c => $"{c.Id},{EscaparCsv(c.Nome)}")) +
                   (encontrados.Count > MaxClientes ? $"\n# Há mais de {MaxClientes}." : ""));

        return (encontrados[0].Id, $"{encontrados[0].Nome} ({encontrados[0].Id})", null);
    }
}




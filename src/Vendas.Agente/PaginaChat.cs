using System;
using System.IO;
using System.Linq;
using System.Reflection;
namespace Vendas.Agente;


public static class PaginaChat
{
    // static readonly em vez de const: o valor só se conhece ao arrancar.
    // Quem usa PaginaChat.Html não precisa de mudar nada.
    // O avatar do Flow é um SVG dentro da página: já não há imagem para embutir.
    // Nome na saudação: o login do Windows ("Diana.Silva" -> "Diana Silva"); vai para um atributo, por isso codificado.
    public static readonly string Html = Ler("pagina-chat.html")
        .Replace("{{UTILIZADOR}}", System.Net.WebUtility.HtmlEncode(Environment.UserName.Replace('.', ' ').Replace('_', ' ')));

    private static Stream Abrir(string nomeFicheiro)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var recurso = assembly.GetManifestResourceNames().Single(n => n.EndsWith(nomeFicheiro));
            return assembly.GetManifestResourceStream(recurso)!;
    }
    private static string Ler(string nomeFicheiro)
    {
        using var leitor = new StreamReader(Abrir(nomeFicheiro));
        return leitor.ReadToEnd();
    }
}
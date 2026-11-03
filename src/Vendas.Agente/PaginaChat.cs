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
    public static readonly string Html = Ler("pagina-chat.html");

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
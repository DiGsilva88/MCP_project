using System;
using System.IO;
using System.Linq;
using System.Reflection;
namespace Vendas.Agente;


public static class PaginaChat
{
    // static readonly em vez de const: o valor só se conhece ao arrancar.
    // Quem usa PaginaChat.Html não precisa de mudar nada.
    public static readonly string Html = Ler("pagina-chat.html")
        .Replace("{{LOGO}}", DataUri("logo.png", "image/png"))
        .Replace("{{AVATAR}}", DataUri("avatar-assistente-vendas.png", "image/png"));
    
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
      

    private static string DataUri(string nomeFicheiro, string tipo)
    {
        using var stream = Abrir(nomeFicheiro);
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return $"data:{tipo};base64,{Convert.ToBase64String(memoria.ToArray())}";
      

    }
}
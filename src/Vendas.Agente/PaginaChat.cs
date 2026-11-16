using System;
using System.IO;
using System.Linq;
using System.Reflection;
namespace Vendas.Agente;


public static class PaginaChat 
{
    // A página está dividida em Pagina/css, Pagina/html e Pagina/js; {{inclui:css/01-tokens.css}} é trocado pelo ficheiro.
    private static readonly string Modelo = System.Text.RegularExpressions.Regex.Replace(
Ler("pagina-chat.html"), @"{{inclui:([\w/.-]+)}}", m => Ler(m.Groups[1].Value));

    // Nome na saudação: o login do Windows ("DOMINIO\Diana.Silva" -> "Diana Silva"); vai para um atributo, por isso codificado.
    // vendedor: o perfil só vê o painel; a página esconde o chat e o email, que o servidor recusa.
    public static string Para(string login, bool vendedor = false) =>
        Modelo.Replace("{{UTILIZADOR}}", System.Net.WebUtility.HtmlEncode(login[(login.LastIndexOf('\\') + 1)..].Replace('.', ' ').Replace('_', ' ')))
              .Replace("{{VENDEDOR}}", vendedor ? "1" : "");

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
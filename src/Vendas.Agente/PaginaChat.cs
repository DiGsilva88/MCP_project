using System.Reflection;
namespace Vendas.Agente;
// A página do modo web, lida de dentro do próprio .exe no arranque.
// O agente continua a não servir ficheiros soltos.
public static class PaginaChat
{
// static readonly em vez de const: o valor só se conhece ao arrancar.
// Quem usa PaginaChat.Html não precisa de mudar nada.
public static readonly string Html = Ler("pagina-chat.html");
private static string Ler(string nomeFicheiro)
{
var assembly = Assembly.GetExecutingAssembly();

// O nome interno é "Vendas.Agente.Pagina.pagina-chat.html"; procura-se pelo fim.
// Single(): se o ficheiro não estiver embebido, o agente falha logoao arrancar.

var recurso = assembly.GetManifestResourceNames().Single(n =>
n.EndsWith(nomeFicheiro));
using var stream = assembly.GetManifestResourceStream(recurso)!;
using var leitor = new StreamReader(stream);
return leitor.ReadToEnd();

}
}
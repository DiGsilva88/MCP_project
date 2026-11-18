using System.Text.RegularExpressions;

namespace Vendas.Agente.Tests;

public class PaginaChatTests
{
    private static readonly string Html = PaginaChat.Para("DOM\\teste");

    // Os ficheiros da página tal como estão embebidos (pagina/css/…, pagina/html/…, pagina/js/…)
    private static IEnumerable<(string Nome, string Texto)> Recursos(string pasta)
    {
        var a = typeof(PaginaChat).Assembly;
        foreach (var nome in a.GetManifestResourceNames().Where(n => n.StartsWith($"pagina/{pasta}/")))
        {
            using var leitor = new StreamReader(a.GetManifestResourceStream(nome)!);
            yield return (nome, leitor.ReadToEnd());
        }
    }

    [Fact]
    public void Vendedor_MarcaOBody_ParaAPaginaEsconderChatEEmail()
    {
        Assert.Contains("data-vendedor=\"1\"", PaginaChat.Para("DOM\\ana", vendedor: true));
        Assert.Contains("data-vendedor=\"\"", Html);
        Assert.DoesNotContain("{{inclui:", Html);   // todas as inclusões e marcas foram trocadas
        Assert.DoesNotContain("{{UTILIZADOR}}", Html);
        Assert.DoesNotContain("{{VENDEDOR}}", Html);
    }

    [Fact]
    public void Html_TemAEstruturaDaNovaUi()
    {
        foreach (var id in new[] { "entrada", "ent-pergunta", "rail-lista", "pesquisa", "fresco", "sino", "principal", "flow", "flow-corpo", "flow-campo", "flow-abrir", "totop", "drawer-root", "menu-root", "anuncio" })
            Assert.Contains($"id=\"{id}\"", Html);
        Assert.Contains("content=\"light\"", Html);
        Assert.Contains("Valores apresentados sem IVA.", Html);
        Assert.Contains("Só leitura. Não altera a base de dados.", Html);
        Assert.Contains("Pergunte sobre esta vista…", Html);
    }

    [Fact]
    public void Html_TemOBotaoSair_EOEcraDeEntradaNaoFicaSaltadoParaSempre()
    {
        Assert.Contains("id=\"sair\"", Html);
        Assert.Contains("aria-label=\"Sair\"", Html);
        Assert.Contains("removeItem(LUGAR)", Html);                   // sair (e abrir de novo) apagam a marca de "já entrei"
        Assert.Contains("getEntriesByType('navigation')", Html);      // só um reload salta a entrada
    }

    [Fact]
    public void Briefing_DizQueAsFaturasPorPagarSaoComIvaEMostraOsCreditos()
    {
        Assert.Contains("com IVA", Html);                              // o saldo por pagar inclui o IVA (verificado na BD)
        Assert.Contains("Notas de crédito por abater", Html);
    }

    [Fact]
    public void Css_SoOsTokensTemCores()
    {
        // Nenhum componente escreve cores à mão: só o 01-tokens.css tem hexadecimais e rgba
        var cor = new Regex(@"#[0-9A-Fa-f]{3}\b|#[0-9A-Fa-f]{6}\b|rgba?\(");
        foreach (var (nome, texto) in Recursos("css").Concat(Recursos("html")).Concat(Recursos("js")).Where(r => !r.Nome.EndsWith("01-tokens.css")))
            Assert.False(cor.IsMatch(texto), $"{nome} tem uma cor escrita à mão: {cor.Match(texto).Value}");
        Assert.Contains("--blue: #1F5BF5;", Html);
        Assert.Contains("--ink-3: #6B7280;", Html);
    }

    [Fact]
    public void Css_NaoTemComentariosComBarrasDuplas()
    {
        // "//" não é comentário em CSS: anulava a regra seguinte
        var inicio = Html.IndexOf("<style>");
        Assert.DoesNotMatch(@"(?m)^\s*//", Html[inicio..Html.IndexOf("</style>", inicio)]);
    }

    [Theory]
    // Clientes, artigos, documentos, valores e emails de exemplo dos protótipos (docs/ui): nada disto pode estar no código
    [InlineData("Público Com Desconto")]
    [InlineData("Façanha Assertiva")]
    [InlineData("Box Self-Storage")]
    [InlineData("Auto Peças Minho")]
    [InlineData("Oficina Cávado")]
    [InlineData("Pastilhas de travão")]
    [InlineData("Filtro de óleo")]
    [InlineData("2026/34816")]
    [InlineData("direcao@empresa.pt")]
    [InlineData("Guimarães")]
    [InlineData("Diana")]
    [InlineData("Dados de exemplo")]
    [InlineData("Protótipo")]
    public void Html_NaoTemDadosDosPrototipos(string exemplo) => Assert.DoesNotContain(exemplo, Html);

    [Fact]
    public void Html_NaoTemValoresEscritosNoCodigo()
    {
        // Os números do ecrã vêm sempre do servidor: nenhum valor em euros nem "Dados até <data>" no código.
        // Os comentários ficam de fora: descrevem o formato do texto que o servidor escreve.
        var codigo = Regex.Replace(Regex.Replace(Html, @"/\*.*?\*/", "", RegexOptions.Singleline), @"(?m)^\s*//.*$", "");
        Assert.DoesNotMatch(@"\d[\s ]?€", codigo);
        Assert.DoesNotMatch(@"Dados (atualizados )?até \d", codigo);
        Assert.Contains("fmt.dataLonga(D.ate)", Html);   // "Dados até" sai da última venda que o servidor devolve
    }

    [Fact]
    public void Js_LigaAsRotasDoServidor()
    {
        foreach (var rota in new[] { "'/resumo?'", "'/api/creditos?'", "/api/clientes/${encodeURIComponent(id)}/briefing","'/api/documentos/'", "'/api/preferencias'", "'/preferencias'", "'/api/agendamentos'", "'/agendamentos'", "'/perguntar'", "'/parar'", "'/feedback'" })
            Assert.Contains(rota, Html);
        Assert.Contains("não é o documento fiscal", Html);   // a pré-visualização nunca pode parecer o documento fiscal
    }

    [Fact]
    public void Acessibilidade_DrawersGraficosEMovimento()
    {
        Assert.Contains("role=\"dialog\" aria-modal=\"true\"", Html);
        Assert.Contains("aria-current=\"page\"", Html);
        Assert.Contains("role=\"img\"", Html);
        Assert.Contains("prefers-reduced-motion", Html);
        Assert.Contains(":focus-visible { outline: 2px solid var(--blue)", Html);
    }

    [Fact]
    public void EcraDeEntrada_TemOTituloEAFonteLocal()
    {
        Assert.Contains("Uma plataforma", Html);
        Assert.Contains("Ir direto ao painel de vendas", Html);
        Assert.Contains("/fonts/Onest-Variable.woff2", Html);
        Assert.DoesNotContain("fonts.googleapis.com", Html);   // os servidores RDS podem não ter internet
        using var fonte = typeof(PaginaChat).Assembly.GetManifestResourceStream("fonts/Onest-Variable.woff2");
        Assert.NotNull(fonte);
    }

    [Theory]
    [InlineData("DOMINIO\\Ana.Costa", "Ana Costa")]
    [InlineData("Ana_Costa", "Ana Costa")]
    public void Para_MostraONomeDeQuemPediuSemODominio(string login, string nome)
    {
        Assert.Contains(nome, PaginaChat.Para(login));
        Assert.DoesNotContain("DOMINIO", PaginaChat.Para(login));
    }
}

        /* ---------- Gráficos como imagem (PNG) ---------- */
    // Cores fixas e claras: a imagem vai para Word, PowerPoint ou email, que têm fundo branco
    const COR = { marca: '#1d46c0', texto: '#16213a', nota: '#566179', grelha: '#e3e8f1', pista: '#e6ebf5', anterior: '#7f93cc' };
    const fonte = (tamanho, peso = 400) => `${peso} ${tamanho}px system-ui, "Segoe UI", Arial, sans-serif`;
    // Corta o texto com "…" para caber na largura
    function cortar(g, texto, largura) {
      if (g.measureText(texto).width <= largura) return texto;
      while (texto.length > 1 && g.measureText(texto + '…').width > largura) texto = texto.slice(0, -1);
      return texto + '…';
    }
    // Canvas com fundo branco, título e período; a escala 2 dá uma imagem nítida no Word
    function novaTela(largura, altura, titulo, subtitulo) {
      const cv = document.createElement('canvas');
      cv.width = largura * 2; cv.height = altura * 2;
      const g = cv.getContext('2d');
      g.scale(2, 2);
      g.fillStyle = '#fff'; g.fillRect(0, 0, largura, altura);
      g.textAlign = 'left'; g.textBaseline = 'alphabetic';
      g.fillStyle = COR.texto; g.font = fonte(16, 700); g.fillText(titulo || 'Vendas', 24, 30);
      if (subtitulo) { g.fillStyle = COR.nota; g.font = fonte(12); g.fillText(subtitulo, 24, 48); }
      return { cv, g };
    }

    // Evolução por mês: os mesmos meses e valores do gráfico do ecrã
    function pngSerie(s, titulo, subtitulo) {
      const L = 960, A = 420, m = { t: 72, r: 24, b: 36, l: 80 };
      const { cv, g } = novaTela(L, A, titulo, subtitulo);
      const max = Math.max(1, ...s.meses.flatMap(x => [x.atual, x.anterior]).filter(v => v > 0));   // 1: sem valores > 0 dava -Infinity
      const passo = Math.pow(10, Math.floor(Math.log10(max))) / 2;
      const topo = Math.ceil(max / passo) * passo;
      const w = L - m.l - m.r, h = A - m.t - m.b, n = s.meses.length;
      const X = i => m.l + (i + 0.5) / n * w;
      const Y = v => m.t + h - v / topo * h;

      // Legenda, em texto, no canto
      const legenda = [String(s.anoAtual), s.anoAnt ? s.anoAnt + ' (tracejado)' : '', s.meses.some(x => x.parcial) ? 'ponto vazio = mês incompleto' : ''];
      g.textAlign = 'right'; g.fillStyle = COR.nota; g.font = fonte(12);
      g.fillText(legenda.filter(Boolean).join(' · '), L - m.r, 30);

      // Grelha e eixos
      g.textBaseline = 'middle'; g.lineWidth = 1;
      [0, topo / 2, topo].forEach(v => {
        g.strokeStyle = COR.grelha; g.beginPath(); g.moveTo(m.l, Y(v)); g.lineTo(L - m.r, Y(v)); g.stroke();
        g.fillStyle = COR.nota; g.fillText(v ? milhares(v) : '0', m.l - 10, Y(v));
      });
      g.textAlign = 'center'; g.textBaseline = 'top';
      s.meses.forEach((x, i) => g.fillText(MESES[x.m - 1].slice(0, 3).toLowerCase(), X(i), A - m.b + 10));

      // Linhas: ano anterior tracejado, ano atual a cheio só nos meses completos
      const linha = (pontos, cor, tracejado) => {
        if (pontos.length < 2) return;
        g.strokeStyle = cor; g.lineWidth = 2.5; g.lineJoin = 'round'; g.setLineDash(tracejado ? [6, 5] : []);
        g.beginPath(); pontos.forEach(([x, y], k) => k ? g.lineTo(x, y) : g.moveTo(x, y)); g.stroke();
        g.setLineDash([]);
      };
      linha(s.meses.map((x, i) => x.anterior != null ? [X(i), Y(x.anterior)] : null).filter(Boolean), COR.anterior, true);
      linha(s.meses.map((x, i) => x.atual != null && !x.parcial ? [X(i), Y(x.atual)] : null).filter(Boolean), COR.marca, false);
      s.meses.forEach((x, i) => {
        if (x.atual == null) return;
        g.beginPath(); g.arc(X(i), Y(x.atual), 4.5, 0, 2 * Math.PI);
        g.fillStyle = x.parcial ? '#fff' : COR.marca; g.fill();
        g.strokeStyle = COR.marca; g.lineWidth = 2; g.stroke();
      });

      // Valor só no melhor mês, para não amontoar números
      const comDados = s.meses.filter(x => x.atual != null && !x.parcial);
      if (comDados.length) {
        const melhor = comDados.reduce((a, b) => b.atual > a.atual ? b : a);
        const i = s.meses.indexOf(melhor);
        g.fillStyle = COR.texto; g.font = fonte(12, 600); g.textAlign = 'center'; g.textBaseline = 'bottom';
        g.fillText(euros(melhor.atual), X(i), Y(melhor.atual) - 10);
      }
      return cv;
    }

    // Ranking: posição e nome | barra | valor e %
    function pngRanking(itens, titulo, subtitulo) {
      const L = 960, alturaLinha = 32, m = { t: 72, r: 24, b: 24, l: 24 };
      const A = m.t + itens.length * alturaLinha + m.b;
      const { cv, g } = novaTela(L, A, titulo, subtitulo);
      const max = Math.max(...itens.map(i => i.num));
      const xBarra = m.l + 280, xValor = L - m.r, wBarra = xValor - 170 - xBarra;
      g.textBaseline = 'middle';
      itens.forEach((it, k) => {
        const y = m.t + k * alturaLinha + alturaLinha / 2;
        const nome = (it.pos ? it.pos + '. ' : '') + it.nome + (it.codigo ? ' (' + it.codigo + ')' : '');
        g.textAlign = 'left'; g.fillStyle = COR.texto; g.font = fonte(13);
        g.fillText(cortar(g, nome, 260), m.l, y);
        g.fillStyle = COR.pista; g.fillRect(xBarra, y - 6, wBarra, 12);
        g.fillStyle = COR.marca; g.fillRect(xBarra, y - 6, Math.max(2, it.num / max * wBarra), 12);
        g.textAlign = 'right'; g.fillStyle = COR.texto; g.font = fonte(13, 600);
        g.fillText(it.valor, xValor - (it.pct ? 56 : 0), y);
        if (it.pct) { g.fillStyle = COR.nota; g.font = fonte(12); g.fillText(it.pct, xValor, y); }
      });
      return cv;
    }

    function botaoImagem(desenhar, titulo) {
      const b = botaoEl('chat-acao', 'Guardar imagem', () => {
        desenhar().toBlob(blob => {
          const nome = semAcento(titulo || '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 50) || 'grafico';
          const a = el('a');
          a.href = URL.createObjectURL(blob);
          a.download = `${nome}-${new Date().toISOString().slice(0, 10)}.png`;
          a.click();
          anunciar('Imagem guardada.');
          setTimeout(() => URL.revokeObjectURL(a.href), 1000);
        }, 'image/png');
      });
      b.dataset.imagem = '';   // o "Exportar dados" do topo oferece a imagem quando existe
      return b;
    }

    // Rodapé de qualquer resposta com dados: Copiar, Exportar CSV e quantas linhas vão
    function rodapeDados(art, linhas, titulo, nota, imagem) {
      const rod = el('div', 'chat-folha-rodape');
      const tabela = linhas && linhas.length > 1 ? semColunasVazias(linhas) : null;
      const n = tabela ? tabela.length - 1 : 0;
      if (tabela) rod.append(botaoCopiar(tabela), botaoExportar(tabela, titulo));
      if (imagem) rod.append(botaoImagem(imagem, titulo));   // só quando há gráfico
      const meta = [n ? `${n} ${n === 1 ? 'linha' : 'linhas'}` : '', nota || ''].filter(Boolean).join(' · ');
      if (meta) rod.append(el('p', 'chat-folha-meta', meta));
      if (rod.childNodes.length) art.append(rod);
    }


    // Ranking (top marcas, artigos, clientes): nome | barra | valor | variação.
    // Cada linha abre o detalhe; a métrica e a ordem mudam sem nova pergunta.
    /* ---------- Gráfico circular: partes de um total ---------- */
    // Cores por ordem fixa (validadas para daltonismo); a 1.ª parte é sempre a maior
    const CORES_CIRCULO = ['--serie-1', '--serie-2', '--serie-3', '--serie-4', '--serie-5', '--serie-6'];
    const percentagem = i => /^\d+(?:[.,]\d+)?\s*%$/.test(i.pct || '') ? parseFloat(i.pct.replace(',', '.')) : null;
    // Circular só quando a resposta divide um total: 2 a 6 partes, todas com "% do total" e a somar ~100%
    function eParteDoTodo(itens) {
      if (itens.length < 2 || itens.length > 6) return false;
      if (itens.some(i => i.delta != null || i.unidades || percentagem(i) == null)) return false;
      const soma = itens.reduce((s, i) => s + percentagem(i), 0);
      return soma >= 97 && soma <= 103;
    }

    function desenharCirculo(itens, art, titulo) {
      const estado = { vista: 'circulo' };
      const g = el('div', 'chat-seg');
      g.setAttribute('role', 'group');
      g.setAttribute('aria-label', 'Ver como');
      const controlos = el('div', 'chat-controlos');
      controlos.append(g);
      const zona = el('div');
      art.append(controlos, zona);
      [['circulo', 'Circular'], ['barras', 'Barras']].forEach(([id, texto]) => {
        const b = botaoEl('', texto, () => { estado.vista = id; desenhar(); });
        b.dataset.id = id;
        g.append(b);
      });

      function desenhar() {
        [...g.children].forEach(b => b.setAttribute('aria-pressed', String(b.dataset.id === estado.vista)));
        zona.replaceChildren();
        if (estado.vista === 'barras') { desenharRanking(itens, zona, titulo); return; }

        const lista = [...itens].sort((a, b) => b.num - a.num);
        const bloco = el('div', 'chat-bloco chat-circulo');
        const NS = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(NS, 'svg');
        svg.setAttribute('viewBox', '0 0 120 120');
        svg.setAttribute('role', 'img');
        svg.setAttribute('aria-label', lista.map(i => `${i.nome} ${i.pct}`).join(', '));
        let inicio = 0;
        const folga = lista.length > 1 ? 0.6 : 0;      // 2px de fundo entre as fatias
        lista.forEach((it, k) => {
          const parte = percentagem(it);
          const c = document.createElementNS(NS, 'circle');
          c.setAttribute('cx', 60); c.setAttribute('cy', 60); c.setAttribute('r', 46);
          c.setAttribute('pathLength', 100);
          c.setAttribute('stroke-dasharray', `${Math.max(0.4, parte - folga)} ${100 - Math.max(0.4, parte - folga)}`);
          c.setAttribute('stroke-dashoffset', String(25 - inicio));   // começa às 12 horas
          c.style.stroke = `var(${CORES_CIRCULO[k]})`;
          const dica = document.createElementNS(NS, 'title');
          dica.textContent = `${it.nome}: ${it.valor} (${it.pct})`;
          c.append(dica);
          svg.append(c);
          inicio += parte;
        });
        const centro = el('div', 'chat-circulo-centro');
        centro.append(el('strong', '', lista[0].pct), el('span', '', lista[0].nome));
        const roda = el('div', 'chat-circulo-roda');
        roda.append(svg, centro);

        // Legenda com nome, valor e % (a cor nunca é a única pista)
        const legenda = el('ul', 'chat-circulo-legenda');
        lista.forEach((it, k) => {
          const li = el('li');
          const cor = el('span', 'chat-circulo-cor');
          cor.style.background = `var(${CORES_CIRCULO[k]})`;
          cor.setAttribute('aria-hidden', 'true');
          li.append(cor, el('span', 'chat-circulo-nome', it.nome), el('strong', '', it.valor), el('small', '', it.pct));
          legenda.append(li);
        });
        bloco.append(roda, legenda);
        zona.append(bloco);
      }
      desenhar();
    }

    // O mesmo gráfico em PNG, para o botão "Guardar imagem"
    function pngCirculo(itens, titulo, subtitulo) {
      const lista = [...itens].sort((a, b) => b.num - a.num);
      const cores = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#6250d6'];
      const L = 720, A = Math.max(320, 96 + lista.length * 32);
      const { cv, g } = novaTela(L, A, titulo, subtitulo);
      const cx = 150, cy = 72 + (A - 72) / 2 - 8, r = 92;
      let ang = -Math.PI / 2;
      g.lineWidth = 36;
      lista.forEach((it, k) => {
        const fim = ang + percentagem(it) / 100 * 2 * Math.PI;
        g.strokeStyle = cores[k];
        g.beginPath(); g.arc(cx, cy, r, ang + 0.01, Math.max(ang + 0.02, fim - 0.01)); g.stroke();
        ang = fim;
      });
      g.textAlign = 'center'; g.textBaseline = 'middle';
      g.fillStyle = COR.texto; g.font = fonte(20, 700); g.fillText(lista[0].pct, cx, cy - 8);
      g.fillStyle = COR.nota; g.font = fonte(12); g.fillText(cortar(g, lista[0].nome, 110), cx, cy + 14);
      g.textAlign = 'left';
      lista.forEach((it, k) => {
        const y = cy - (lista.length - 1) * 16 + k * 32;
        g.fillStyle = cores[k]; g.fillRect(300, y - 6, 12, 12);
        g.fillStyle = COR.texto; g.font = fonte(13); g.fillText(cortar(g, it.nome, 220), 322, y);
        g.textAlign = 'right'; g.font = fonte(13, 600); g.fillText(it.valor, L - 84, y);
        g.fillStyle = COR.nota; g.font = fonte(12); g.fillText(it.pct, L - 24, y);
        g.textAlign = 'left';
      });
      return cv;
    }

    function desenharRanking(itens, art, titulo) {
      const comEuros = itens.every(i => i.unidades && /€/.test(i.pct));   // "18 390 unidades (436 176,07 €)"
      const comDelta = itens.some(i => i.delta != null);
      // Meses/trimestres em unidades ("2026-03", "2026-T1"): evolução, por ordem do tempo
      const estado = { metrica: 'principal', ordem: itens.every(i => /^\d{4}-(\d{2}|T\d)$/.test(i.nome)) ? 'nome' : 'valor', aberto: -1 };
      const valor = i => estado.metrica === 'euros' ? paraNumero(i.pct) : i.num;
      const dim = dimensao(titulo);   // "Vendas por localidade" -> "localidade"

      const controlos = el('div', 'chat-controlos');
      const grupo = (rotulo, chave, opcoes) => {
        const g = el('div', 'chat-seg');
        g.setAttribute('role', 'group');
        g.setAttribute('aria-label', rotulo);
        opcoes.forEach(([id, texto]) => {
          const b = botaoEl('', texto, () => { estado[chave] = id; estado.aberto = -1; desenhar(); });
          b.dataset.id = id;
          g.append(b);
        });
        g.dataset.chave = chave;
        controlos.append(g);
      };
      if (comEuros) grupo('Métrica', 'metrica', [['principal', 'Unidades'], ['euros', 'Euros']]);
      if (itens.length > 2) grupo('Ordenar por', 'ordem', [['valor', 'Valor'], ...(comDelta ? [['variacao', 'Variação']] : []), ['nome', 'Nome']]);
      if (controlos.childNodes.length) art.append(controlos);

      const bloco = el('div', 'chat-bloco chat-ranking');
      const ol = el('ol', 'chat-rank');
      ol.setAttribute('role', 'list');
      const eixo = el('div', 'chat-rank-linha chat-rank-eixo');
      eixo.setAttribute('aria-hidden', 'true');
      bloco.append(ol, eixo);
      art.append(bloco);

      function desenhar(foco) {
        controlos.querySelectorAll('.chat-seg').forEach(g =>
          [...g.children].forEach(b => b.setAttribute('aria-pressed', String(b.dataset.id === estado[g.dataset.chave]))));
        const max = Math.max(...itens.map(valor));
        const passo = Math.pow(10, Math.floor(Math.log10(max))) / 2;
        const topo = Math.ceil(max / passo) * passo;     // ex.: 299 053 -> 300 000
        const lista = [...itens].sort(estado.ordem === 'nome' ? (a, b) => a.nome.localeCompare(b.nome, 'pt')
          : estado.ordem === 'variacao' ? (a, b) => (b.delta ?? -Infinity) - (a.delta ?? -Infinity)
          : (a, b) => valor(b) - valor(a));

        ol.replaceChildren(...lista.map((it, k) => {
          const n = itens.indexOf(it);
          const li = el('li');
          const linha = el('button', 'chat-rank-linha');
          linha.type = 'button';
          linha.setAttribute('aria-expanded', String(estado.aberto === n));
          linha.setAttribute('aria-controls', 'detalhe-' + n);
          linha.addEventListener('click', () => { estado.aberto = estado.aberto === n ? -1 : n; desenhar(n); });
          const nome = el('span', 'chat-rank-nome');
          const vazio = /^\(?sem descri[çc][ãa]o\)?$/i.test(it.nome);
          nome.append(el('span', vazio ? 'chat-item-nome--vazio' : '', vazio ? 'Sem descrição' : it.nome));
          // "Balcão: Artigo X (código)": o artigo e o código numa só linha por baixo da zona (eram três linhas empilhadas)
          if (it.sub) nome.append(el('small', '', it.sub + (it.codigo ? ' · ' + it.codigo : '')));
          else if (it.codigo) nome.append(el('small', '', it.codigo));
          nome.title = it.sub ? `${it.nome}: ${it.sub}` : it.nome;   // nome inteiro ao passar o rato; no teclado, no detalhe
          const pista = el('span', 'chat-rank-pista'), cheio = el('span');
          pista.setAttribute('aria-hidden', 'true');
          cheio.style.width = Math.max(1, valor(it) / topo * 100) + '%';
          pista.append(cheio);
          const val = el('span', 'chat-rank-valor');
          const emEuros = estado.metrica === 'euros';
          val.append(el('strong', '', emEuros ? it.pct : it.valor));
          if (it.pct) val.append(el('small', '', emEuros ? it.valor : it.pct));
          if (it.media) val.append(el('span', 'so-leitor', ', valor médio ' + it.media));
          const variacao = el('span', 'chat-rank-delta');   // coluna própria: não empurra o valor para cima da barra
          // Em euros, a variação continua a ser a das unidades (é a que vem na resposta): o texto diz isso
          if (it.delta != null) variacao.append(chipDelta(it.delta, (it.anterior ? ' vs ' + it.anterior.ano : '') + (emEuros ? ' em unidades' : '')));
          else if (it.novo) variacao.append(chipNeutro('Sem vendas', ' em ' + it.novo));
          else if (it.anterior) variacao.append(chipNeutro(it.anterior.valor, ' em ' + it.anterior.ano));
          linha.append(el('span', 'chat-rank-pos', String(k + 1)), nome, pista, val, variacao);

          const det = el('div', 'chat-rank-detalhe');
          det.id = 'detalhe-' + n;   // só há uma resposta no ecrã de cada vez, por isso o id é único
          det.hidden = estado.aberto !== n;
          if (!det.hidden) {
            const dl = el('dl', 'chat-factos');
            const facto = (rotulo, v) => { if (!v) return; const g = el('div'); g.append(el('dt', '', rotulo), el('dd', '', v)); dl.append(g); };
            facto(it.unidades ? 'Unidades' : 'Valor', it.valor);
            facto(/€/.test(it.pct) ? 'Vendas' : /%\s+\S/.test(it.pct) ? 'Percentagem' : 'Parte do total', it.pct);
            facto(rotuloSub(titulo), it.sub);
            facto('Código', it.codigo);
            if (it.anterior) facto('Em ' + it.anterior.ano, it.anterior.valor);
            if (it.delta != null) facto('Variação', textoDelta(it.delta) + (it.anterior ? ' vs ' + it.anterior.ano : ''));
            facto('Valor médio por documento', it.media);
            const acoes = el('div', 'chat-acoes');
            if (limparDim(dim) === 'cliente' && it.codigo) acoes.append(botaoEl('chat-chip', 'Ver resumo deste cliente', () => pedirResumo(null, it.codigo)));
            perguntasDoItem(it.nome, dim).forEach(q => acoes.append(botaoEl('chat-chip', q, () => prepararPergunta(q))));
            det.append(el('strong', '', it.nome), dl);
            if (acoes.childNodes.length) det.append(acoes);
          }
          li.append(linha, det);
          return li;
        }));

        // Eixo alinhado com a coluna das barras
        const marcas = el('div', 'chat-rank-marcas');
        for (let k = 0; k <= 4; k++) marcas.append(el('span', '', k ? milhares(topo * k / 4).replace(' €', '') : '0'));
        eixo.replaceChildren(el('span'), el('span'), marcas, el('span'), el('span'));
        if (foco != null) ol.querySelector(`[aria-controls="detalhe-${foco}"]`).focus();
      }
      desenhar();
    }

    // Lista simples: posição | nome e código | valor. Com grupos ("Clientes em Odivelas") e nomes repetidos.
    function desenharLista(itens, art, titulo) {
      const nums = itens.map(i => Math.abs(i.num)).filter(n => n > 0);
      const max = nums.length ? Math.max(...nums) : 0;
      const rotulos = new Set(itens.map(i => i.rotulo));
      const comum = rotulos.size === 1 ? [...rotulos][0] : '';
      const bloco = el('div', 'chat-bloco chat-bloco--lista');
      if (titulo) bloco.append(el('h3', 'chat-lista-titulo', titulo));
      if (comum) { const c = el('div', 'chat-lista-cab', maiuscula(comum)); c.setAttribute('aria-hidden', 'true'); bloco.append(c); }

      // O mesmo nome repetido no mesmo grupo aparece uma vez, com a contagem
      const chave = i => (i.grupo || '') + '|' + i.nome + '|' + i.valor;
      const vezes = new Map(), porGrupo = new Map();
      itens.forEach(i => {
        vezes.set(chave(i), (vezes.get(chave(i)) || 0) + 1);
        porGrupo.set(i.grupo || '', (porGrupo.get(i.grupo || '') || 0) + 1);
      });

      const ol = el('ol', 'chat-lista');
      ol.setAttribute('role', 'list');
      let grupoAnterior = '';
      for (const it of itens) {
        const n = vezes.get(chave(it));
        if (!n) continue;                       // repetido já mostrado
        vezes.set(chave(it), 0);
        if (it.grupo && it.grupo !== grupoAnterior) {
          const g = el('li', 'chat-grupo-cab', it.grupo + ' ');
          g.append(el('span', '', '· ' + porGrupo.get(it.grupo)));
          ol.append(g);
        }
        grupoAnterior = it.grupo || '';
        const li = el('li', 'chat-item');
        const corpo = el('div', 'chat-item-corpo');
        const vazio = /^\(?sem descri[çc][ãa]o\)?$/i.test(it.nome);
        corpo.append(el('span', 'chat-item-nome' + (vazio ? ' chat-item-nome--vazio' : ''), vazio ? 'Sem descrição' : it.nome));
        if (it.codigo) corpo.append(el('span', 'chat-item-codigo', it.codigo));
        if (it.sub) corpo.append(el('span', 'chat-item-codigo', it.sub));
        if (n > 1) corpo.append(el('span', 'chat-item-codigo', n + ' registos com este nome'));
        const v = Math.abs(it.num);
        if (v > 0 && max > 0) {
          const b = el('span', 'chat-item-barra' + (it.num < 0 ? ' chat-item-barra--neg' : '')), f = el('span');
          b.setAttribute('aria-hidden', 'true');
          f.style.width = Math.max(2, v / max * 100) + '%';
          b.append(f); corpo.append(b);
        }
        const val = el('div', 'chat-item-valor');
        if (it.rotulo) val.append(el('span', comum ? 'so-leitor' : 'chat-item-rotulo', maiuscula(it.rotulo) + (comum ? ': ' : '')));
        if (it.valor) val.append(el('strong', it.rotulo ? 'chat-data' : '', it.valor));
        if (it.pct) val.append(el('span', 'chat-item-pct', it.pct));
        if (it.delta != null) val.append(chipDelta(it.delta, it.anterior ? ' vs ' + it.anterior.ano : ''));
        li.append(el('span', 'chat-item-pos', it.pos), corpo, val);
        ol.append(li);
      }
      bloco.append(ol);
      art.append(bloco);
    }

    // Perguntas seguintes, calculadas a partir da própria resposta (em vez das sugestões fixas do tema)
    function perguntasSeguintes(r, itens, serie) {
      if (serie) {
        const completos = serie.meses.filter(x => x.atual != null && !x.parcial);
        if (completos.length < 2) return [];
        const melhor = completos.reduce((a, b) => b.atual > a.atual ? b : a);
        const pior = completos.reduce((a, b) => b.atual < a.atual ? b : a);
        const mes = x => MESES[x.m - 1].toLowerCase() + ' de ' + x.a;
        return [`Que artigos venderam mais em ${mes(melhor)}?`, `Que clientes compraram mais em ${mes(melhor)}?`, `Vendas por zona em ${mes(pior)}`];
      }
      const validos = itens.filter(i => i.num > 0 && !/^\(/.test(i.nome));   // "(sem localidade)" não dá boa pergunta
      if (validos.length < 2) return [];
      const dim = dimensao(r.titulo || (r.paragrafos || []).join(' '));   // sem título: "A marca que mais faturou…"
      const n = i => nomeProprio(i.nome);
      const primeiro = validos.reduce((a, b) => b.num > a.num ? b : a);
      const ano = +((r.periodo.match(/(\d{4})$/) || [])[1] || new Date().getFullYear());
      const comVariacao = validos.filter(i => i.delta != null);
      // Sem variação, ou artigo/marca/família (não se filtram): as mesmas perguntas do detalhe da linha
      if (comVariacao.length < 2 || !filtravel(dim)) return perguntasDoItem(primeiro.nome, dim, `em ${ano}`);
      // Só o que as ferramentas respondem: o "porquê" de uma descida não está nos dados
      const sobe = comVariacao.reduce((a, b) => b.delta > a.delta ? b : a);
      const desce = comVariacao.reduce((a, b) => b.delta < a.delta ? b : a);
      const lista = [];
      if (sobe.delta > 0) lista.push(`Que artigos venderam mais ${naDim(dim, n(sobe))} face ao ano passado?`);
      if (desce.delta < 0) lista.push(`Comparar as vendas ${daDim(dim, n(desce))} mês a mês com o ano passado`);
      lista.push(`Evolução mensal das vendas ${daDim(dim, n(primeiro))} em ${ano}`);
      return lista;
    }

    // Perguntas seguintes: escrevem no campo, para rever antes de enviar
    function blocoSeguintes(perguntas) {
      const sec = el('section', 'chat-sugestoes');
      const lista = el('div');
      perguntas.forEach(p => lista.append(botaoEl('chat-chip', p, () => prepararPergunta(p))));
      sec.append(el('h3', '', 'Perguntas seguintes'), lista);
      return sec;
    }

    // "Clientes com Vendedor = Paulo Afonso e Localidade = Corroios" -> título "Clientes" + filtros
    function lerFiltros(titulo) {
      const filtros = [...(titulo || '').matchAll(/(\p{L}[\p{L} ]*?)\s*=\s*(.+?)(?=\s+e\s+\p{L}[\p{L} ]*?\s*=|$)/gu)]
        .map(m => [m[1].replace(/^(?:.*\bcom\s+|e\s+)/i, ''), m[2].trim()]);
      return { titulo: filtros.length ? titulo.replace(/\s+com\s+.*=.*$/i, '') : titulo, filtros };
    }

    // "Qual foi a localidade que mais faturou este ano?" + "no mês passado" -> "... faturou no mês passado?"
    function trocarPeriodo(pergunta, periodo) {
      const sem = pergunta.replace(/\?\s*$/, '')
        .replace(/\s+(?:(?:n?este|deste)\s+ano|(?:no|do|o)\s+ano\s+(?:atual|passado)|(?:no|do|n?este|deste)\s+m[eê]s(?:\s+(?:passado|atual))?|(?:no|do|neste)\s+[uú]ltimo\s+trimestre|(?:no|do)\s+1\.º\s+semestre|(?:no|do)\s+dia\s+\d{2}-\d{2}-\d{4}|(?:em|de)\s+\d{4})(?=\s|$)/i, '').replace(/\s{2,}/g, ' ').trim();
      return `${sem} ${periodo}?`;
    }

    // "Como li a pergunta": filtros e período em etiquetas. O período muda-se com um clique.
    const PERIODOS = ['este ano', 'no mês passado', 'no último trimestre', 'no ano passado'];
    function etiquetasLeitura(cab, filtros, periodo, pergunta) {
      if (!filtros.length && !periodo) return;
      const div = el('div', 'chat-filtros');
      div.setAttribute('role', 'group');
      div.setAttribute('aria-label', 'Como li a pergunta');
      filtros.forEach(([k, v]) => { const f = el('span', 'chat-filtro'); f.append(el('span', '', k), v); div.append(f); });
      cab.append(div);
      if (!periodo) return;
      const menu = el('div', 'chat-periodos');
      menu.hidden = true;
      PERIODOS.forEach(p => menu.append(botaoEl('chat-chip', maiuscula(p.replace(/^no\s+/, '')), () => { if (!ocupado) perguntar(trocarPeriodo(pergunta, p)); })));
      const b = botaoEl('chat-filtro chat-filtro--botao', '', () => {
        menu.hidden = !menu.hidden;
        b.setAttribute('aria-expanded', String(!menu.hidden));
      });
      b.setAttribute('aria-expanded', 'false');
      b.append(el('span', '', 'Período'), periodo, el('span', '', ' ▾'));
      div.append(b);
      cab.append(menu);
    }

    // Destaque em texto: quem lidera, quem mais subiu e quem mais desceu, calculado a partir dos números
    // Nome com o código: muitos artigos chamam-se todos "(sem descrição)", só o código os distingue
    const nomeECodigo = i => nomeProprio(i.nome) + (i.codigo ? ` (${i.codigo})` : '');

    function fraseDestaque(itens) {
      const comValor = itens.filter(i => i.num > 0);
      if (comValor.length < 3 || itens.some(i => i.sub)) return '';   // "melhor X em cada Y": não há um líder
      const primeiro = comValor.reduce((a, b) => b.num > a.num ? b : a);
      let frase = `${nomeECodigo(primeiro)} lidera com ${primeiro.valor}` + (/%/.test(primeiro.pct) ? ` (${primeiro.pct} do total)` : '') + '.';
      const comVariacao = comValor.filter(i => i.delta != null);
      if (comVariacao.length > 1) {
        const sobe = comVariacao.reduce((a, b) => b.delta > a.delta ? b : a);
        const desce = comVariacao.reduce((a, b) => b.delta < a.delta ? b : a);
        if (sobe.delta > 0) frase += ` A maior subida é de ${nomeECodigo(sobe)} (${textoDelta(sobe.delta)}).`;
        if (desce.delta < 0) frase += ` A maior descida é de ${nomeECodigo(desce)} (${textoDelta(desce.delta)}).`;
      }
      return frase;
    }

    function folhaLista(r, original, pergunta = '') {
      const art = el('article', 'chat-folha');
      const simples = !r.titulo && !r.itens.length && !r.factos.length && !r.avisos.length;
      if (simples) {
        art.append(el('p', 'chat-texto', original.trim()));
        const rod = el('div', 'chat-folha-rodape');
        rod.append(botaoCopiar(original.trim()));   // texto: só Copiar
        art.append(rod);
        return art;
      }

      // Período de um só dia: título com o dia da semana e sem "1 meses" no total
      const dia = r.periodo.match(/^(\d{2})-(\d{2})-(\d{4}) a \1-\2-\3$/);
      if (dia) {
        const semana = new Date(+dia[3], +dia[2] - 1, +dia[1]).toLocaleDateString('pt-PT', { weekday: 'long' });
        r.titulo = `Faturação de ${semana}, ${dia[1]}-${dia[2]}-${dia[3]}`;
        r.periodo = '';
        r.resumo = r.resumo.replace(/\bem 1 m[eê]s(es)?,\s*/i, 'em ');
      }

      const cab = el('div', 'chat-folha-cab');
      const leitura = lerFiltros(r.titulo);
      if (r.titulo) cab.append(el('h2', 'chat-folha-titulo', maiuscula(leitura.titulo)));
      etiquetasLeitura(cab, leitura.filtros, r.periodo, pergunta);
      art.append(cab);

      art.classList.add('chat-folha--painel');
      const linhasKpis = desenharKpis(r, cab, art);
      const serie = lerSerie(r);

      r.avisos.forEach((a, n) => {
        const div = el('div', 'chat-aviso');
        div.setAttribute('role', 'note');
        const texto = el('div');
        const span = el('span');
        span.append(el('strong', '', 'Atenção: '), a);
        texto.append(span);
        if (n === 0 && serie && serie.justa) {
          const j = el('p', 'chat-justa');
          j.append(`Comparando só os meses completos, ${serie.justa.de} a ${serie.justa.ate}:`,
                   chipDelta(serie.justa.delta, serie.anoAnt ? ' vs ' + serie.anoAnt : ''));
          texto.append(j);
        }
        div.append(icone('M8 1.8 15 14H1L8 1.8ZM8 6v3.5M8 11.5v.5'), texto);
        art.append(div);
      });

      r.paragrafos.forEach(p => art.append(el('p', 'chat-texto', p)));

      // Valores negativos (devoluções, notas de crédito) vão para um bloco à parte
      const devolucoes = r.itens.filter(i => i.num < 0);
      const itens = r.itens.filter(i => !(i.num < 0));
      // Um só valor (ex.: um dia) já está nos cartões: não desenha uma barra sozinha
      const soUm = itens.length === 1 && !devolucoes.length && !!art.querySelector('.chat-kpis');

      const destaque = serie ? '' : fraseDestaque(itens);
      if (destaque) art.append(el('p', 'chat-destaque', destaque));

      let imagem = null;   // gráfico que o botão "Guardar imagem" redesenha em PNG
      const periodo = r.periodo ? 'Período: ' + r.periodo : '';
      if (serie) { desenharSerie(serie, art); imagem = () => pngSerie(serie, r.titulo, periodo); }
      else if (soUm) { /* nada a acrescentar */ }
      else if (itens.length && itens.every(i => i.num > 0 && !i.rotulo) && eParteDoTodo(itens)) { desenharCirculo(itens, art, r.titulo); imagem = () => pngCirculo(itens, r.titulo, periodo); }
      else if (itens.length && itens.every(i => i.num > 0 && !i.rotulo)) { desenharRanking(itens, art, r.titulo); imagem = () => pngRanking(itens, r.titulo, periodo); }
      else if (itens.length) desenharLista(itens, art);
      if (devolucoes.length) desenharLista(devolucoes, art, 'Devoluções e notas de crédito');

      const linhas = !r.itens.length ? linhasKpis
        : serie ? [['Mês', serie.anoAtual, serie.anoAnt || '', 'Variação (%)', 'Valor médio'],
                   ...r.itens.map(i => [i.nome, i.valor, i.anterior ? i.anterior.valor : '', i.delta == null ? '' : String(i.delta).replace('.', ','), i.media])]
        : [['Posição', 'Grupo', 'Nome', rotuloSub(r.titulo), 'Código', 'Valor', '%', 'Média'],
           ...r.itens.map(i => [i.pos, i.grupo || '', i.nome, i.sub || '', i.codigo, i.valor, i.pct, i.media])];
      rodapeDados(art, linhas, r.titulo, r.nota, imagem);
      if (r.itens.length) art.sugestoes = perguntasSeguintes(r, itens, serie);   // lidas em mostrarItem
      // Resposta de faturação de um período que o Resumo tem: o balão de sugestão propõe o resumo desse período
      art.periodoResumo = /^vendas por/i.test(r.titulo) ? periodoEmPalavras(r.periodo) : null;
      // ... e o próprio resultado também, no rodapé (passa por perguntar: fica a pergunta e a resposta na conversa)
      if (art.periodoResumo) {
        const p = art.periodoResumo, rod = art.querySelector('.chat-folha-rodape');
        const b = botaoEl('chat-acao', `Ver o resumo das vendas ${p}`, () => { if (!ocupado) perguntar(`Resumo das vendas ${p}`); });
        if (rod) rod.insertBefore(b, rod.querySelector('.chat-folha-meta')); else art.append(b);
      }
      return art;
    }

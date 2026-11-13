    /* ---------- Números, variações e meses ---------- */
    const MESES = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'];
    const ICONES = {
      total: 'M11.5 4.2A4.8 4.8 0 1 0 11.5 11.8M2.5 6.8h6.5M2.5 9.2h6.5',
      recibo: 'M3.5 1.8h9v12.4l-2.2-1.4-2.3 1.4-2.3-1.4-2.2 1.4V1.8ZM6 5.5h4M6 8.5h4',
      subida: 'M2 12.5 6 8l3 3 5-6M10.5 5H14v3.5',
      barras: 'M3 13V8M8 13V3M13 13V6'
    };
    const euros = n => Math.round(n).toLocaleString('pt-PT') + ' €';   // sem cêntimos: lê-se melhor
    const milhares = n => n >= 1e6 ? (n / 1e6).toLocaleString('pt-PT', { maximumFractionDigits: 1 }) + ' M €' : Math.round(n / 1000) + ' mil €';
    const textoDelta = d => (d >= 0 ? '+' : '−') + Math.abs(d).toFixed(1).replace('.', ',') + '%';
    function chipDelta(d, sufixo = '') {
      const c = el('span', 'chat-delta ' + (d >= 0 ? 'chat-delta--sobe' : 'chat-delta--desce'));
      c.append(icone(d >= 0 ? 'M2 12 6.5 7.5l3 3L14 5M10 5h4v4' : 'M2 4l4.5 4.5 3-3L14 11M10 11h4V7', 12), textoDelta(d), el('span', 'chat-delta-sufixo', sufixo));
      return c;
    }
    // Sem variação calculável (não vendeu no ano anterior, ou vendeu 0): cinzento, para a linha não ficar sem indicador
    const chipNeutro = (texto, sufixo = '') => { const c = el('span', 'chat-delta', texto); c.append(el('span', 'chat-delta-sufixo', sufixo)); return c; };

    // Resumo e factos -> cartões (Total, Valor médio, Melhor mês…)
    function desenharKpis(r, cab, art) {
      const cartoes = [], ficha = [];
      let resumoTexto = '';
      if (r.resumo) {
        const [principal, ...resto] = r.resumo.split(/\.\s+(?=\S)/);
        if (resto.length) cab.append(el('p', 'chat-folha-meta', resto.join('. ')));
        const m = semPonto(principal).match(/^(?:total\s+)?(-?\d[\d\s.\u00a0\u202f]*(?:,\d+)?\s*€)\s*(.*)$/i);
        if (m) cartoes.push({ rotulo: 'Total', valor: euros(paraNumero(m[1])), detalhe: maiuscula(m[2].replace(/^em\s+/i, '')), icone: ICONES.total });
        else if (/^\d/.test(principal)) {
          // "379 clientes em 9 grupos": "379 clientes" no valor, "9 grupos" por baixo, para caber numa linha
          const [, valor, detalhe] = semPonto(principal).match(/^([\d\s.]*\d\s+\S+)\s*(?:em\s+)?(.*)$/) || [, semPonto(principal), ''];
          cartoes.push({ rotulo: 'Total', valor, detalhe: maiuscula(detalhe), icone: ICONES.barras });
        }
        else resumoTexto = maiuscula(semPonto(principal));
      }
      // Um número com, no máximo, uma palavra ou € ou %: "1 cliente", "58,54 €", "0,0%"
      const eNumeroCurto = v => /^[-−+]?[\d\s.,  ]+\s*(€|%|\p{L}+)?$/u.test(v);
      for (const [k, v] of r.factos) {
        if (/^lista completa/i.test(k)) continue;   // não acrescenta informação
        const c = k.match(/^comparado com\s.*?(\d{4})$/i);
        const p = v.match(/\(([+\-−]?[\d.,]+)\s*%\)/);
        if (c && p && cartoes[0] && cartoes[0].rotulo === 'Total') {
          cartoes[0].delta = parseFloat(p[1].replace('−', '-').replace(',', '.'));
          cartoes[0].vs = 'vs ' + c[1];
          continue;
        }
        const mes = v.match(/^(\d{4})-(\d{2})\s+[–-]\s+(.+)$/);   // "2026-07 – 278 519,07 €"
        // "10 130,5 unidades (268 120,74 €)" (em quantidade): fica tal como vem, não é um valor em euros
        if (mes) { cartoes.push({ rotulo: k, valor: MESES[+mes[2] - 1], detalhe: /unidades/.test(mes[3]) ? mes[3] : euros(paraNumero(mes[3])), icone: ICONES.subida }); continue; }
        // Texto (nome do cliente, condição de pagamento): vai para a ficha, não para um cartão grande
        if (!eNumeroCurto(v) || /^\d{6,}$/.test(v)) { ficha.push([k, v]); continue; }   // NIF e códigos não são totais
        cartoes.push({ rotulo: k, valor: v, icone: /m[ée]dio/i.test(k) ? ICONES.recibo : ICONES.barras });
      }
      if (resumoTexto) art.append(el('p', 'chat-folha-resumo', resumoTexto));
      // Só um total de contagem ("5 clientes"): vai para o cabeçalho, sem cartão
      if (cartoes.length === 1 && cartoes[0].icone === ICONES.barras && cartoes[0].delta == null) {
        const total = el('p', 'chat-folha-total', cartoes[0].valor);
        if (cartoes[0].detalhe) total.append(el('span', '', ' · ' + cartoes[0].detalhe));
        cab.append(total);
      } else if (cartoes.length) {
        const grelha = el('div', 'chat-kpis');
        for (const c of cartoes) {
          const k = el('div', 'chat-kpi');
          const topo = el('div', 'chat-kpi-cab');
          const ic = el('span', 'chat-kpi-icone');
          ic.append(icone(c.icone, 14));
          topo.append(el('span', '', c.rotulo), ic);
          const linha = el('div', 'chat-kpi-linha');
          linha.append(el('span', 'chat-kpi-valor', c.valor));
          if (c.delta != null) linha.append(chipDelta(c.delta, ' ' + c.vs));
          k.append(topo, linha);
          if (c.detalhe) k.append(el('span', 'chat-kpi-detalhe', c.detalhe));
          grelha.append(k);
        }
        art.append(grelha);
      }
      if (ficha.length) {
        const dl = el('dl', 'chat-ficha');
        for (const [k, v] of ficha) dl.append(el('dt', '', rotuloCampo(k)), el('dd', '', v));
        art.append(dl);
      }
      return [['Indicador', 'Valor', 'Detalhe'],
              ...cartoes.map(c => [c.rotulo, c.valor, c.detalhe || '']),
              ...ficha.map(([k, v]) => [rotuloCampo(k), v, ''])];
    }

    // Lista em que todas as linhas são meses "AAAA-MM" -> série para o gráfico
    function lerSerie(r) {
      // Em unidades: o gráfico e a tabela falam em euros, por isso fica a lista com barras.
      if (r.itens.length < 2 || r.itens.some(i => i.unidades) || !r.itens.every(i => /^\d{4}-\d{2}$/.test(i.nome))) return null;
      const meses = r.itens.map(i => {
        const [a, m] = i.nome.split('-').map(Number);
        return { a, m, atual: paraNumero(i.valor), anterior: i.anterior ? paraNumero(i.anterior.valor) : null,
                 delta: i.delta, anoAnt: i.anterior ? i.anterior.ano : null, media: i.media };
      });
      // O mês da última venda registada (aviso "Atenção") está incompleto
      const ultima = r.avisos.join(' ').match(/(\d{2})-(\d{2})-(\d{4})/);
      meses.forEach(x => { x.parcial = !!ultima && x.atual != null && x.a === +ultima[3] && x.m === +ultima[2]; });
      // Período a começar a meio do mês (ex.: últimos 90 dias): o 1.º mês só tem alguns dias
      if (r.periodo && !r.periodo.startsWith('01-') && meses[0].atual != null) meses[0].parcial = true;
      const anoAnt = (meses.find(x => x.anoAnt) || {}).anoAnt;
      // Comparação justa: só os meses completos antes do último incompleto
      let justa = null;
      const ip = meses.findLastIndex(x => x.parcial);
      const completos = meses.slice(0, ip < 0 ? 0 : ip).filter(x => !x.parcial && x.atual != null && x.anterior != null);
      if (completos.length) {
        const sa = completos.reduce((s, x) => s + x.atual, 0), sb = completos.reduce((s, x) => s + x.anterior, 0);
        const curto = x => MESES[x.m - 1].slice(0, 3).toLowerCase();
        justa = { de: curto(completos[0]), ate: curto(completos[completos.length - 1]), delta: (sa / sb - 1) * 100 };
      }
      return { meses, anoAtual: meses[0].a, anoAnt, justa };
    }

    function desenharSerie(s, art) {
      const valores = s.meses.flatMap(x => [x.atual, x.anterior]).filter(v => v > 0);
      const max = Math.max(1, ...valores);   // 1: sem valores > 0 dava -Infinity
      const passo = Math.pow(10, Math.floor(Math.log10(max))) / 2;
      const topo = Math.ceil(max / passo) * passo;          // ex.: 278 519 -> 300 000
      const colunasCss = `repeat(${s.meses.length}, minmax(0, 1fr))`;
      const nomeMes = x => MESES[x.m - 1] + (x.a !== s.anoAtual ? ' ' + x.a : '');

      const sec = el('section', 'chat-bloco chat-grafico');
      const cab = el('div', 'chat-grafico-cab');
      const leg = el('div', 'chat-legenda');
      const legenda = (cls, txt) => { const i = el('span'); i.append(el('span', 'chat-legenda-cor ' + cls), txt); leg.append(i); };
      // Com 4 meses ou mais: área; com menos, barras (com poucos meses, uma linha engana)
      const comArea = s.meses.length >= 4;
      const tipo = comArea ? 'flow-legenda-linha' : 'chat-legenda-barra';
      legenda(tipo, String(s.anoAtual));
      if (s.anoAnt) legenda(tipo + ' ' + tipo + '--ant', s.anoAnt);
      if (s.meses.some(x => x.parcial)) legenda(tipo + ' ' + tipo + '--parcial', 'Mês incompleto');
      // Valores por cima dos pontos em milhares ("256"), como diz o subtítulo; abaixo de 10 000 € ficam em euros
      const emMil = topo >= 1e4;
      const rotuloValor = v => Math.round(emMil ? v / 1000 : v).toLocaleString('pt-PT');
      const cabTexto = el('div');
      cabTexto.append(el('h3', '', 'Evolução das vendas em ' + s.anoAtual),
        el('p', 'chat-grafico-sub', `Faturação por mês, em ${emMil ? 'milhares de euros' : 'euros'}${s.anoAnt ? ', comparada com ' + s.anoAnt : ''}.`));
      cab.append(cabTexto, leg);

      const corpo = el('div', 'chat-grafico-corpo');
      const eixo = el('div', 'chat-eixo');
      eixo.setAttribute('aria-hidden', 'true');
      const ALT = 280;   // altura da área do gráfico em px (igual ao CSS de .chat-eixo, .chat-linha-area e .flow-area)
      [[topo, ALT], [topo / 2, ALT / 2], [0, 0]].forEach(([v, y]) => {
        const l = el('span', '', v ? milhares(v) : '0'); l.style.bottom = (y - 7) + 'px'; eixo.append(l);
      });
      const colunas = el('div', 'chat-colunas');
      // Gráfico de linhas: pontos no centro de cada coluna de mês
      const n = s.meses.length;
      const X = i => (i + 0.5) / n * 100;              // % da largura
      const Y = v => ALT - v / topo * ALT;            // px a partir do topo
      const barras = el('div', 'chat-linha-area');
      barras.setAttribute('role', 'group');
      barras.setAttribute('aria-label', 'Evolução das vendas por mês. Escolha um mês para ver os valores.');
      [ALT / 2, ALT].forEach(y => { const g = el('span', 'chat-guia'); g.setAttribute('aria-hidden', 'true'); g.style.bottom = y + 'px'; barras.append(g); });
      const marca = el('span', 'chat-linha-marca');
      marca.setAttribute('aria-hidden', 'true');
      barras.append(marca);
      const etiquetas = [];   // rótulo do valor de cada mês: o do mês escolhido fica destacado

      // REDESIGN 11/2026: uma barra por mês (ano anterior ao lado, mais clara), com o valor por cima
      const largura = 100 / n;
      s.meses.forEach((x, i) => {
        const duas = x.anterior != null;
        const w = largura * (duas ? 0.34 : 0.55);
        const barra = (v, cls, desvio) => {
          if (v == null || v <= 0) return;
          const b = el('span', 'chat-barra-mes ' + cls);
          b.setAttribute('aria-hidden', 'true');
          b.style.left = (X(i) + desvio) + '%'; b.style.width = w + '%'; b.style.height = Math.max(3, v / topo * ALT) + 'px';
          barras.append(b);
        };
        if (!comArea) {
          barra(x.anterior, 'chat-barra-mes--ant', duas ? -w / 2 - 0.4 : 0);
          barra(x.atual, x.parcial ? 'chat-barra-mes--parcial' : '', duas ? w / 2 + 0.4 : 0);
        }
        if (x.atual != null && x.atual > 0) {
          const r = el('span', 'chat-barra-valor', rotuloValor(x.atual));
          r.setAttribute('aria-hidden', 'true');
          r.style.left = (X(i) + (!comArea && duas ? w / 2 + 0.4 : 0)) + '%'; r.style.top = (Y(x.atual) - (comArea ? 10 : 3)) + 'px';
          // Mês incompleto abaixo do anterior: a ligação pontilhada desce por cima do ponto e tapava o valor; vai para baixo
          const anteriorMes = i > 0 ? s.meses[i - 1].atual : null;
          if (comArea && x.parcial && anteriorMes != null && anteriorMes > x.atual) {
            r.classList.add('chat-barra-valor--abaixo');
            r.style.top = Math.min(Y(x.atual) + 10, ALT - 14) + 'px';
          }
          barras.append(r);
          etiquetas[i] = r;
        }
      });

      // Área + linha do ano atual; ano anterior tracejado
      if (comArea) {
        const NS = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(NS, 'svg');
        svg.setAttribute('viewBox', '0 0 100 ' + ALT);      // 100 = largura em %, ALT = altura em px (igual a X e Y)
        svg.setAttribute('preserveAspectRatio', 'none');
        svg.setAttribute('aria-hidden', 'true');
        svg.classList.add('flow-area');
        const caminho = (d, cls) => {
          const p = document.createElementNS(NS, 'path');
          p.setAttribute('d', d);
          p.setAttribute('class', cls);
          p.setAttribute('vector-effect', 'non-scaling-stroke');   // a linha não estica com o gráfico
          svg.append(p);
        };
        const pontos = campo => s.meses.map((x, i) => ({ i, v: x[campo], parcial: x.parcial })).filter(p => p.v > 0);
        const traco = ps => ps.map((p, k) => (k ? 'L' : 'M') + X(p.i) + ' ' + Y(p.v)).join(' ');
        const completos = pontos('atual').filter(p => !p.parcial);
        const parcial = pontos('atual').find(p => p.parcial);
        if (s.anoAnt) caminho(traco(pontos('anterior')), 'flow-linha-ant');
        if (completos.length) {
          const ult = completos[completos.length - 1];
          caminho(traco(completos) + ` L${X(ult.i)} ${ALT} L${X(completos[0].i)} ${ALT} Z`, 'flow-area-fundo');
          caminho(traco(completos), 'flow-linha');
          // mês incompleto: só uma ligação pontilhada, para não parecer uma queda
          if (parcial) caminho(traco([ult, parcial]), 'flow-linha-parcial');
        }
        barras.prepend(svg);   // primeiro filho: fica por baixo dos pontos
      }

      const rotulos = el('div', 'chat-rotulos');
      rotulos.setAttribute('aria-hidden', 'true');
      rotulos.style.gridTemplateColumns = colunasCss;
      const leitura = el('section', 'chat-leitura');
      leitura.setAttribute('aria-live', 'polite');
      leitura.setAttribute('aria-label', 'Mês selecionado');
      if (comArea) { marca.classList.add('flow-marca-mes'); marca.style.width = (100 / n) + '%'; }   // faixa da coluna do mês

      const variacao = (x, curto) => x.atual == null ? el('span', 'chat-delta', 'Sem vendas registadas')
        : x.parcial ? el('span', 'chat-delta', curto ? 'Incompleto' : 'Mês incompleto')
        : x.delta != null ? chipDelta(x.delta, s.anoAnt && !curto ? ' vs ' + s.anoAnt : '') : el('span');
      const botoes = [];
      const escolher = i => {
        const x = s.meses[i];
        botoes.forEach(b => { b.setAttribute('aria-pressed', String(+b.dataset.i === i)); b.tabIndex = +b.dataset.i === i ? 0 : -1; });
        marca.style.left = X(i) + '%';
        [...rotulos.children].forEach((r, j) => r.classList.toggle('chat-rotulo--ativo', i === j));
        etiquetas.forEach((r, j) => r && r.classList.toggle('chat-barra-valor--ativo', i === j));
        // Cartão azul: mês, estado, valor, ano anterior, variação, diferença em euros e uma pergunta pronta
        const mes = MESES[x.m - 1];
        // Três zonas: mês (topo), valor em destaque com a variação (meio), comparação e pergunta (fundo)
        const topo = el('div', 'chat-leitura-topo');
        topo.append(el('h4', '', 'Mês selecionado'), el('strong', 'chat-leitura-nome', `${mes} ${x.a}`),
          el('small', '', x.atual == null ? 'Sem vendas registadas' : x.parcial ? 'Mês incompleto' : 'Mês completo'));
        const kpi = el('div', 'chat-leitura-kpi');
        kpi.append(el('span', 'chat-leitura-valor', x.atual == null ? '—' : rEur(x.atual)));
        if (x.atual != null && !x.parcial && x.delta != null) kpi.append(chipDelta(x.delta, s.anoAnt ? ' face a ' + s.anoAnt : ''));
        if (x.atual != null && !x.parcial && x.anterior != null) {
          const dif = x.atual - x.anterior;
          kpi.append(el('small', '', `${dif >= 0 ? '+' : '−'}${rEur(Math.abs(dif))} de diferença`));
        }
        const comp = el('dl', 'chat-leitura-comp');
        const par = (rotulo, valor) => { const d = el('div'); d.append(el('dt', '', rotulo), el('dd', '', valor)); comp.append(d); };
        if (s.anoAnt) par('Em ' + s.anoAnt, x.anterior == null ? '—' : rEur(x.anterior));
        if (x.media) par('Valor médio', x.media);
        leitura.replaceChildren(topo, kpi);
        if (comp.children.length) leitura.append(comp);
        const pergunta = `Vendas de ${mes.toLowerCase()} de ${x.a}` + (s.anoAnt ? ` comparadas com ${mes.toLowerCase()} de ${s.anoAnt}` : '');
        const perguntarMes = botaoEl('chat-leitura-btn', 'Perguntar sobre ' + mes.toLowerCase(), () => { if (!ocupado) { verConversa(true); perguntar(pergunta); } });
        perguntarMes.insertAdjacentHTML('afterbegin', svgIc('brilho', 15));
        leitura.append(perguntarMes);
      };
      s.meses.forEach((x, i) => {
        rotulos.append(el('span', '', MESES[x.m - 1].slice(0, 3).toLowerCase()));
        if (x.atual == null) return;                    // mês sem vendas: sem ponto
        const b = botaoEl('chat-ponto-mes' + (x.parcial ? ' chat-ponto-mes--parcial' : ''), '', () => escolher(i));
        b.dataset.i = i;
        b.style.left = X(i) + '%';
        b.style.top = Y(x.atual) + 'px';
        b.append(el('span'));
        b.setAttribute('aria-label', nomeMes(x) + ': ' + euros(x.atual) + (x.anterior != null ? ', ' + s.anoAnt + ': ' + euros(x.anterior) : '')
          + (x.parcial ? ', mês incompleto' : x.delta != null ? ', ' + textoDelta(x.delta) : ''));
        botoes.push(b);
        barras.append(b);
      });
      // Uma só paragem do Tab no gráfico: as setas, Home e End mudam de mês
      barras.addEventListener('keydown', e => {
        const k = botoes.findIndex(b => b.tabIndex === 0);
        const novo = { ArrowRight: k + 1, ArrowLeft: k - 1, Home: 0, End: botoes.length - 1 }[e.key];
        if (novo == null || novo < 0 || novo >= botoes.length) return;
        e.preventDefault();
        botoes[novo].click();
        botoes[novo].focus();
      });
      colunas.append(barras, rotulos);
      // Com a área, a variação de cada mês só se via escolhendo o mês: faixa com a diferença face ao ano anterior, sempre à vista
      if (comArea && s.anoAnt) {
        const faixa = el('div', 'flow-dif');
        faixa.setAttribute('aria-hidden', 'true');   // a variação já está no nome de cada ponto
        faixa.style.gridTemplateColumns = colunasCss;
        const maxDif = Math.max(1, ...s.meses.map(x => x.parcial ? 0 : Math.abs(x.delta || 0)));
        s.meses.forEach(x => {
          const c = el('div', 'flow-dif-mes');
          if (x.delta == null || x.parcial) c.append(el('span', 'flow-dif-nada', x.atual != null && x.parcial ? 'incomp.' : '—'));
          else {
            const h = Math.max(2, Math.abs(x.delta) / maxDif * 30), sobe = x.delta >= 0;
            const b = el('i', sobe ? 'flow-dif-sobe' : 'flow-dif-desce');
            b.style.height = h + 'px';
            const t = el('span', sobe ? 'flow-dif-sobe' : 'flow-dif-desce', textoDelta(x.delta));
            t.style[sobe ? 'bottom' : 'top'] = (51 + h + 2) + 'px';   // na ponta da barra (linha do zero a 50px)
            c.append(b, t);
          }
          faixa.append(c);
        });
        colunas.append(faixa, el('p', 'flow-dif-titulo', 'Diferença face a ' + s.anoAnt + ' (meses completos)'));
      }
      corpo.append(eixo, colunas);
      // Cartão branco com o gráfico; o cartão azul do mês fica fora, ao lado
      const linhaGrafico = el('div', 'flow-linha-grafico');
      const ladoGrafico = el('div', 'flow-grafico-lado');
      ladoGrafico.append(cab, corpo);
      linhaGrafico.append(ladoGrafico, leitura);
      sec.append(linhaGrafico);
      art.append(sec);

      // Começa no melhor mês
      let melhor = 0;
      s.meses.forEach((x, i) => { if ((x.atual || 0) > (s.meses[melhor].atual || 0)) melhor = i; });
      escolher(melhor);

      // Tabela de detalhe: os mesmos números, para ler ou copiar
      const tab = el('section', 'chat-bloco chat-meses');
      const grelha = el('div');
      grelha.setAttribute('role', 'table');
      grelha.setAttribute('aria-label', 'Detalhe por mês');
      // Colunas opcionais: ano anterior só com comparação, valor médio só quando vem em cada mês
      const comMedia = s.meses.some(x => x.media);
      const linha = (celulas, cabecalho) => {
        const l = el('div', 'chat-meses-linha' + (cabecalho ? ' chat-meses-cab' : ''));
        l.setAttribute('role', 'row');
        l.style.gridTemplateColumns = `minmax(0, 1fr) repeat(${celulas.length - 1}, minmax(80px, auto))`;
        celulas.forEach(c => { c.setAttribute('role', cabecalho ? 'columnheader' : 'cell'); l.append(c); });
        grelha.append(l);
      };
      linha([el('span', '', 'Mês'), el('span', '', String(s.anoAtual)), ...(s.anoAnt ? [el('span', '', s.anoAnt)] : []),
             ...(comMedia ? [el('span', '', 'Valor médio')] : []), el('span', '', 'Variação')], true);
      s.meses.filter(x => x.atual != null).forEach(x => {
        const v = el('span'); v.append(variacao(x, true));   // na tabela o cabeçalho já diz "Variação"
        linha([el('span', '', nomeMes(x) + (x.parcial ? ' (incompleto)' : '')), el('span', 'chat-atual', euros(x.atual)),
               ...(s.anoAnt ? [el('span', 'chat-ant', x.anterior == null ? '—' : euros(x.anterior))] : []),
               ...(comMedia ? [el('span', '', x.media || '—')] : []), v]);
      });
      tab.append(el('h3', '', 'Detalhe por mês'), grelha);
      art.append(tab);

      // Vista: gráfico ou tabela, com os mesmos números
      tab.hidden = true;
      const controlos = el('div', 'chat-controlos');
      const vista = el('div', 'chat-seg');
      vista.setAttribute('role', 'group');
      vista.setAttribute('aria-label', 'Vista');
      const mudarVista = grafico => {
        sec.hidden = !grafico;
        tab.hidden = grafico;
        [...vista.children].forEach((b, k) => b.setAttribute('aria-pressed', String((k === 0) === grafico)));
      };
      vista.append(botaoEl('', 'Gráfico', () => mudarVista(true)), botaoEl('', 'Tabela', () => mudarVista(false)));
      controlos.append(vista);
      sec.before(controlos);
      mudarVista(true);

      // O cartão "Melhor mês" mostra esse mês no gráfico
      const cartao = [...art.querySelectorAll('.chat-kpi')].find(k => /melhor m[eê]s/i.test(k.textContent));
      const pontoMelhor = botoes.find(b => +b.dataset.i === melhor);
      if (cartao && pontoMelhor) {
        cartao.classList.add('chat-kpi--acao');
        cartao.tabIndex = 0;
        cartao.setAttribute('role', 'button');
        cartao.setAttribute('aria-label', cartao.querySelector('.chat-kpi-valor').textContent + ', melhor mês. Mostrar no gráfico');
        const ir = () => { mudarVista(true); pontoMelhor.click(); pontoMelhor.focus(); };
        cartao.addEventListener('click', ir);
        cartao.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); ir(); } });
      }
    }

    // Perguntas sobre uma linha de um ranking, conforme o que a lista compara ("por localidade", "por marca")
    // "por zona," -> "zona"; "da zona Balcão" / "do artigo X": o agente sabe em que campo procurar
    const FEMININO = /^(zona|localidade|marca|fam[ií]lia|a?c?tividade|categoria|regi[ãa]o)/i;
    // De que se fala: "Vendas por zona" -> "zona"; sem "por", a primeira palavra conhecida ("A marca que mais faturou…" -> "marca")
    const dimensao = titulo => ((titulo || '').match(/\bpor\s+(\S+)/i) || [])[1]
      || (((titulo || '').match(/\b(marca|artigo|fam[ií]lia|zona|localidade|cliente|vendedor)s?\b/i) || [])[1] || '').toLowerCase();
    const limparDim = dim => (dim || '').replace(/[^\p{L}]/gu, '').toLowerCase();
    const daDim = (dim, nome) => limparDim(dim) ? `${FEMININO.test(limparDim(dim)) ? 'da' : 'do'} ${limparDim(dim)} ${nome}` : `de ${nome}`;
    const naDim = (dim, nome) => limparDim(dim) ? `${FEMININO.test(limparDim(dim)) ? 'na' : 'no'} ${limparDim(dim)} ${nome}` : `em ${nome}`;

    // O servidor só filtra vendas por cliente e pelas colunas da ficha (zona, localidade...), não por artigo,
    // marca ou família: para esses só há perguntas sobre o ranking inteiro, senão a sugestão dava erro.
    const filtravel = dim => /^(zona|localidade|distrito|vendedor|a?c?tividade|cliente)/.test(limparDim(dim));
    const perguntasDoRanking = dim => {
      const d = limparDim(dim) === 'familia' ? 'família' : limparDim(dim);
      const [o, s] = FEMININO.test(d) ? ['a', 'as'] : ['o', 'os'];
      return [`Qual foi ${o} ${d} mais vendid${o} em cada distrito?`, `Comparar as vendas por ${d} com o ano passado`,
              `Quais ${d === 'família' ? 'as famílias' : `${s} ${d}s`} que venderam menos este ano?`];
    };

    function perguntasDoItem(nome, dim, quando = 'este ano') {
      if (/^\(/.test(nome) || /^(m[eê]s|trimestre)/.test(limparDim(dim))) return [];   // "(sem localidade)", "2026-03" não dão boa pergunta
      if (!filtravel(dim)) return /artigo|marca|fam[ií]lia/i.test(dim) ? perguntasDoRanking(dim) : [];
      const n = nomeProprio(nome);
      return [
        `Evolução mensal das vendas ${daDim(dim, n)} ${quando}`,
        `Comparar as vendas ${daDim(dim, n)} com o ano passado`,
        `Que artigos venderam mais ${naDim(dim, n)} ${quando}?`
      ];
    }

    /* =========================================================
       Componentes partilhados: indicador, painel, tabela, barra, gráfico por mês,
       painel lateral, menu Exportar, aviso temporário e estados. Criados uma vez, usados em todos os ecrãs.
       ========================================================= */
    const variacaoHtml = n => { const v = fmt.variacao(n); return v.sobe == null ? '<span class="muted">–</span>' : `<span class="${v.sobe ? 'up' : 'dn'}">${v.texto}</span>`; };
    const faceA = (n, ano) => n == null ? '' : `${variacaoHtml(n)} face a ${esc(ano)}`;

    // Indicador: valor a azul, rótulo e nota. nome=true: o valor é um nome (20px, pode quebrar linha)
    const indicador = ({ valor, rotulo, nota = '', nome = false }) =>
      `<div class="k"><div class="k-valor${nome ? ' k-valor--nome' : ''}">${valor}</div><div class="k-rotulo">${esc(rotulo)}</div>${nota ? `<div class="k-nota">${nota}</div>` : ''}</div>`;
    const indicadores = lista => `<div class="kpis">${lista.map(indicador).join('')}</div>`;

    // Painel: contorno, raio 12px, cabeçalho com título e uma ligação à direita
    const painel = ({ titulo = '', extra = '', corpo, id = '' }) =>
      `<section class="painel"${id ? ` id="${id}"` : ''}>${titulo || extra ? `<div class="painel-cab">${titulo ? `<h2>${titulo}</h2>` : ''}${extra}</div>` : ''}${corpo}</section>`;

    // Tabela: colunas [{ t, dir }]; linhas [{ c: [html], attrs, rotulo }] (com attrs a linha é clicável e abre com Enter)
    function tabela({ colunas, linhas, vazio = 'Nenhum resultado com os filtros atuais.', legenda = '' }) {
      const cab = colunas.map(c => `<th scope="col"${c.dir ? ' class="dir"' : ''}${c.largura ? ` style="width:${c.largura}"` : ''}>${c.t ? esc(c.t) : `<span class="so-leitor">${esc(c.oculto || '')}</span>`}</th>`).join('');
      const corpo = linhas.length ? linhas.map(l => `<tr${l.attrs ? ` class="clicavel" tabindex="0" ${l.attrs}${l.rotulo ? ` aria-label="${esc(l.rotulo)}"` : ''}` : ''}${l.desligada ? ' class="desligado"' : ''}>${
        l.c.map((v, i) => `<td${colunas[i].dir ? ' class="dir num"' : colunas[i].num ? ' class="num"' : ''}>${v}</td>`).join('')}</tr>`).join('')
        : `<tr><td colspan="${colunas.length}" class="vazio">${esc(vazio)}</td></tr>`;
      return `<div class="tabela-rolar"><table>${legenda ? `<caption class="so-leitor">${esc(legenda)}</caption>` : ''}<thead><tr>${cab}</tr></thead><tbody>${corpo}</tbody></table></div>`;
    }

    // Barra horizontal: proporção (0–100); neg = queda forte, a preto
    const barra = (pct, neg = false) => `<div class="hbar${neg ? ' hbar--neg' : ''}" aria-hidden="true"><i style="width:${Math.max(0.6, Math.min(100, pct || 0)).toFixed(1)}%"></i></div>`;

    const seg = (opcoes, ativo, atributo, rotulo) =>
      `<div class="seg" role="group" aria-label="${esc(rotulo)}">${opcoes.map(([v, t]) => `<button type="button" ${atributo}="${esc(v)}" aria-pressed="${v === ativo}">${esc(t)}</button>`).join('')}</div>`;
    const chips = (opcoes, ativo, atributo, rotulo) =>
      `<div class="ferramentas" role="group" aria-label="${esc(rotulo)}">${opcoes.map(([v, t, n]) => `<button type="button" class="chip" ${atributo}="${esc(v)}" aria-pressed="${v === ativo}">${esc(t)}${n != null ? ` <span class="muted">${n}</span>` : ''}</button>`).join('')}</div>`;
    const interruptor = (ligado, rotulo, atributos) => `<button type="button" class="sw" role="switch" aria-checked="${ligado}" aria-label="${esc(rotulo)}" ${atributos}><i></i></button>`;

    const aCarregar = (texto = 'A carregar os dados…') => `<div class="estado" role="status"><span class="roda" aria-hidden="true"></span>${esc(texto)}</div>`;
    const comErro = (texto = ERRO_SIMPLES, atributo = 'data-repetir') => `<div class="estado-erro" role="alert"><span>${esc(texto)}</span>${atributo ? `<button type="button" ${atributo}>Tentar outra vez</button>` : ''}</div>`;

    /* ---------- Gráfico de linhas por mês ----------
       Ano atual a azul contínuo, ano anterior cinzento tracejado. O mês incompleto é um ponto solto (círculo vazio)
       com "até dia X", sem linha a ligá-lo, para não parecer uma queda.
       meses: [{ a, m, atual, anterior, parcial }]; previsao: { valores, banda: [[min, max]] } a seguir aos meses. */
    function escalaRedonda(max) {
      if (!(max > 0)) return { topo: 1, passo: 1 };
      let passo = Math.pow(10, Math.floor(Math.log10(max)));
      while (max / passo > 4) passo *= 2;
      while (max / passo < 2) passo /= 2;
      return { topo: Math.ceil(max / passo) * passo, passo };
    }
    function graficoMeses({ meses, sel = -1, ultimaData = '', previsao = null, rotulo = 'Vendas por mês', interativo = true }) {
      const W = 900, H = 250, L = 64, R = 24, T = 18, B = 34;
      const nPrev = previsao ? previsao.valores.length : 0, n = meses.length + nPrev;
      const valores = [...meses.flatMap(x => [x.atual, x.anterior]), ...(previsao ? previsao.banda.flat() : [])].filter(v => v > 0);
      const { topo, passo } = escalaRedonda(Math.max(...valores, 0));
      const x = i => n === 1 ? (L + W - R) / 2 : L + i * (W - L - R) / (n - 1), y = v => T + (1 - Math.max(0, v) / topo) * (H - T - B);
      const linha = pts => pts.map(([i, v]) => `${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' ');
      const meio = Math.min(52, (W - L - R) / Math.max(1, n - 1) * 0.9);
      let s = '';
      for (let g = 0; g <= topo + 1e-9; g += passo) s += `<line class="g-grelha" x1="${L}" x2="${W - R}" y1="${y(g)}" y2="${y(g)}"/><text class="g-eixo" x="${L - 10}" y="${y(g) + 4}" text-anchor="end">${g ? esc(fmt.curto(g)) : '0'}</text>`;
      if (sel >= 0) s += `<rect class="g-faixa" x="${x(sel) - meio / 2}" y="${T}" width="${meio}" height="${H - T - B}" rx="6"/>`;
      if (previsao) {
        const ini = meses.length, cima = previsao.banda.map((b, k) => [ini + k, b[1]]), baixo = previsao.banda.map((b, k) => [ini + k, b[0]]).reverse();
        const ult = meses.findLastIndex(m => m.atual != null);
        s += `<polygon class="g-banda" points="${linha([[ult, meses[ult].atual], ...cima, ...baixo, [ult, meses[ult].atual]])}"/>`;
      }
      const ant = meses.map((m, i) => [i, m.anterior]).filter(p => p[1] != null);
      if (ant.length > 1) s += `<polyline class="g-ant" points="${linha(ant)}"/>`;
      const completos = meses.map((m, i) => [i, m.atual, m.parcial]).filter(p => p[1] != null && !p[2]);
      if (completos.length > 1) s += `<polyline class="g-atual" points="${linha(completos)}"/>`;
      if (previsao && completos.length) {
        const u = completos[completos.length - 1];
        s += `<polyline class="g-prev" points="${linha([[u[0], u[1]], ...previsao.valores.map((v, k) => [meses.length + k, v])])}"/>`;
        previsao.valores.forEach((v, k) => { s += `<circle class="g-ponto--prev" cx="${x(meses.length + k)}" cy="${y(v)}" r="3.5"/>`; });
      }
      completos.forEach(([i, v]) => { s += `<circle class="g-ponto${i === sel ? ' g-ponto--sel' : ''}" cx="${x(i)}" cy="${y(v)}" r="${i === sel ? 6 : 4}"/>`; });
      const ultima = fmt.lerData(ultimaData);
      meses.forEach((m, i) => {
        if (!m.parcial || m.atual == null) return;
        const dia = ultima && ultima.a === m.a && ultima.m === m.m ? `até dia ${ultima.d}` : 'mês incompleto';
        s += `<circle class="g-parcial" cx="${x(i)}" cy="${y(m.atual)}" r="${i === sel ? 6 : 4}"/><text class="g-parcial-txt" x="${x(i) - 8}" y="${y(m.atual) - 12}" text-anchor="end">${dia}</text>`;
      });
      const rotuloMes = i => i < meses.length ? fmt.mesCurto(meses[i].m) + (meses[i].parcial ? '*' : '') : fmt.mesCurto(previsao.meses[i - meses.length]);
      for (let i = 0; i < n; i++) s += `<text class="g-mes${i === sel ? ' g-mes--sel' : ''}" x="${x(i)}" y="${H - 10}" text-anchor="middle">${rotuloMes(i)}</text>`;
      if (interativo) meses.forEach((m, i) => { if (m.atual != null) s += `<rect class="g-zona" x="${x(i) - meio / 2}" y="${T}" width="${meio}" height="${H - T}" data-mes="${i}"><title>${MESES[m.m - 1]} ${m.a}</title></rect>`; });
      // Descrição em texto: o que o gráfico mostra, para quem não o vê
      const desc = meses.filter(m => m.atual != null || m.anterior != null).map(m => `${MESES[m.m - 1]} ${m.a}: ${fmt.eur(m.atual)}${m.anterior != null ? `, ano anterior ${fmt.eur(m.anterior)}` : ''}${m.parcial ? ', mês incompleto' : ''}`)
        .concat(previsao ? previsao.valores.map((v, k) => `${previsao.meses[k] ? MESES[previsao.meses[k] - 1] : ''}: previsão ${fmt.eur(v)}, entre ${fmt.eur(previsao.banda[k][0])} e ${fmt.eur(previsao.banda[k][1])}`) : []).join('; ');
      const svg = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="${esc(rotulo + '. ' + desc)}">${s}</svg>`;
      return interativo ? `<div class="grafico" tabindex="0" data-grafico aria-label="${esc(rotulo)}: use as setas para escolher o mês">${svg}</div>` : `<div class="grafico">${svg}</div>`;
    }
    // Setas no gráfico: o mês anterior ou o seguinte com dados
    function mesVizinho(meses, atual, passo) {
      for (let i = atual + passo; i >= 0 && i < meses.length; i += passo) if (meses[i].atual != null) return i;
      return atual;
    }

    /* ---------- Painel lateral (drawer) ----------
       Abre da direita sobre um fundo escurecido; fecha com Esc, com o × e ao clicar fora.
       O foco fica dentro enquanto está aberto e volta ao botão de origem quando fecha. */
    const drawer = {
      aberto: null,
      abrir({ topo, html, pe = '', largo = false, form = false, aoFechar = null }) {
        if (this.aberto) this.fechar(false);
        const root = document.getElementById('drawer-root'), origem = document.activeElement, tag = form ? 'form' : 'div';
        root.innerHTML = `<div class="drawer-veu" data-fechar></div><${tag} class="drawer${largo ? ' drawer--largo' : ''}" role="dialog" aria-modal="true" aria-labelledby="dr-t"${form ? ' novalidate' : ''}>
          <div class="drawer-cab"><span class="muted">${esc(topo)}</span><button type="button" class="botao-icone" data-fechar aria-label="Fechar">${ic('fechar')}</button></div>
          <div class="drawer-corpo">${html}</div>${pe ? `<div class="drawer-pe">${pe}</div>` : ''}</${tag}>`;
        document.querySelector('.app').inert = true;
        this.aberto = { root, origem, aoFechar, el: root.querySelector('.drawer') };
        root.querySelectorAll('[data-fechar]').forEach(b => b.addEventListener('click', () => this.fechar()));
        (root.querySelector('[autofocus]') || root.querySelector('#dr-t') || root.querySelector('.drawer-cab button')).focus();
        return this.aberto.el;
      },
      fechar(devolverFoco = true) {
        const a = this.aberto;
        if (!a) return;
        this.aberto = null;
        a.root.innerHTML = '';
        document.querySelector('.app').inert = false;
        a.aoFechar?.();
        if (devolverFoco && a.origem?.isConnected) a.origem.focus();
      }
    };
    document.addEventListener('keydown', e => {
      const a = drawer.aberto;
      if (!a) return;
      if (e.key === 'Escape') { e.preventDefault(); drawer.fechar(); return; }
      if (e.key !== 'Tab') return;
      const focaveis = [...a.el.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')].filter(x => !x.disabled && x.offsetParent !== null);
      if (!focaveis.length) return;
      const [primeiro, ultimo] = [focaveis[0], focaveis[focaveis.length - 1]];
      if (e.shiftKey && document.activeElement === primeiro) { e.preventDefault(); ultimo.focus(); }
      else if (!e.shiftKey && document.activeElement === ultimo) { e.preventDefault(); primeiro.focus(); }
      else if (!a.el.contains(document.activeElement)) { e.preventDefault(); primeiro.focus(); }
    });

    /* ---------- Aviso temporário ---------- */
    let tempoAviso;
    function aviso(texto) {
      document.querySelectorAll('.toast').forEach(t => t.remove());
      const t = el('div', 'toast', texto);
      t.setAttribute('role', 'status');
      document.body.append(t);
      clearTimeout(tempoAviso);
      tempoAviso = setTimeout(() => t.remove(), 4000);
    }

    /* ---------- Menu Exportar: Excel, PDF, CSV ---------- */
    const FORMATOS = [['xlsx', 'Excel (.xlsx)'], ['pdf', 'PDF'], ['csv', 'CSV']];
    function abrirMenuExportar(botao, aoEscolher) {
      fecharMenu();
      const r = botao.getBoundingClientRect(), root = document.getElementById('menu-root');
      root.innerHTML = `<div class="menu" role="menu" aria-label="Exportar" style="top:${r.bottom + 6}px;left:${Math.max(8, Math.min(r.left, innerWidth - 216))}px">${FORMATOS.map(([f, t]) => `<button type="button" role="menuitem" data-formato="${f}">${t}</button>`).join('')}</div>`;
      botao.setAttribute('aria-expanded', 'true');
      const itens = [...root.querySelectorAll('button')];
      itens.forEach(b => b.addEventListener('click', e => { e.stopPropagation(); fecharMenu(true); aoEscolher(b.dataset.formato); }));
      root.querySelector('.menu').addEventListener('keydown', e => {
        const i = itens.indexOf(document.activeElement);
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); itens[(i + (e.key === 'ArrowDown' ? 1 : itens.length - 1)) % itens.length].focus(); }
        if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); fecharMenu(true); }
        if (e.key === 'Tab') fecharMenu();
      });
      menuAberto = botao;
      itens[0].focus();
    }
    let menuAberto = null;
    function fecharMenu(focar = false) {
      document.getElementById('menu-root').innerHTML = '';
      if (menuAberto) { menuAberto.setAttribute('aria-expanded', 'false'); if (focar) menuAberto.focus(); }
      menuAberto = null;
    }
    document.addEventListener('click', e => { if (menuAberto && !e.target.closest('.menu') && e.target !== menuAberto) fecharMenu(); });

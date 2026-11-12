    function resDestaques(sobe, desce, anoAnt, nome = i => i.nome) {
      if (!sobe && !desce) return '';
      const caixa = (i, cls, txt) => i ? `<div class="res-destaque"><span class="${cls}">${txt}</span><strong>${rEsc(nome(i))}</strong>${rVar(i.delta)} face a ${rEsc(anoAnt)}</div>` : '';
      return `<div class="res-destaques">${caixa(sobe, 'res-sobe', '▲ MAIOR SUBIDA')}${caixa(desce, 'res-desce', '▼ MAIOR DESCIDA')}</div>`;
    }

    // Vista Alertas (como na demonstração): lista com separadores e, ao lado, o detalhe do escolhido e as regras
    function alertasHtml(D) {
      const todos = alertasAtivos(D), lista = alertasVisiveis(D);
      const sel = lista.find(a => a.id === resumoEstado.alertaSel) || lista[0];
      const nomeVista = id => SECOES.find(s => s[0] === id)[1];
      const separadores = FILTROS_ALERTA.map(([nome, f]) => `<button type="button" class="res-pilula" data-alerta-filtro="${nome}" aria-pressed="${resumoEstado.alertaFiltro === nome}">${nome}<span>${todos.filter(f).length}</span></button>`).join('');
      const linhas = lista.map((a, i) => `<button type="button" class="res-al-linha ${alertaPorLer(a) ? 'res-al-linha--nova' : ''}" data-alerta="${i}" aria-pressed="${a === sel}">
          <span class="res-al-ic ${SEV_CLASSE[a.sev]}">${svgIc('aviso', 16)}</span>
          <span class="res-al-txt"><b>${rEsc(a.titulo)}</b><span>${rEsc(a.texto)}</span><small>${rEsc(a.cat)}${alertaPorLer(a) ? ', por ler' : ''}</small></span>
          <span class="res-al-ponto" aria-hidden="true"></span></button>`).join('');
      const detalhe = sel ? `<section class="res-cartao" data-bloco="al-sel">
          <div class="res-al-sel"><span class="res-chip-sev ${SEV_CLASSE[sel.sev]}">${sel.sev}</span>${rEsc(sel.cat)}</div>
          <span class="res-al-titulo">${rEsc(sel.titulo)}</span>
          <p class="res-al-desc">${rEsc(sel.texto)}</p>
          <button type="button" class="res-btn-negro" data-alerta-abrir>${svgIc('seta', 15)}Ver em ${rEsc(nomeVista(sel.ver))}</button>
          ${sel.pergunta ? `<button type="button" class="res-btn-flow" data-perguntar="${rEsc(sel.pergunta)}">${svgIc('brilho', 15)}Perguntar ao Flow</button>` : ''}
          <button type="button" class="res-btn-discreto" data-alerta-ler>${alertaPorLer(sel) ? 'Marcar como lido' : 'Marcar como por ler'}</button>
        </section>` : '';
      return `<section class="res-cartao res-al-lista" data-bloco="alertas">
          <div class="res-pilulas" role="group" aria-label="Filtrar alertas">${separadores}<button type="button" class="res-al-todas" data-alerta-todos>Marcar todos como lidos</button></div>
          ${linhas || '<p class="res-nota">Não há alertas neste separador. Escolha outro ou ligue mais regras.</p>'}
        </section>
        <div class="res-al-lado">${detalhe}
          <section class="res-cartao" data-bloco="al-regras">
            <h3>Regras de alerta</h3>
            ${REGRAS_ALERTA.map(([id, nome, sub]) => `<button type="button" class="res-regra" role="switch" data-regra="${id}" aria-checked="${regrasAlerta[id] !== false}"><span>${nome}<small>${sub}</small></span><i aria-hidden="true"></i></button>`).join('')}
          </section>
        </div>`;
    }

    // Uma página de cada vez. Limita e guarda: com outro filtro a lista pode ter menos páginas.
    // aria-disabled e não disabled: o botão fica focável e quem usa teclado não perde o lugar na última página.
    function paginar(lista, chave, atributo, rotulo) {
      const POR_PAGINA = 15, ultima = Math.max(0, Math.ceil(lista.length / POR_PAGINA) - 1);
      const pag = resumoEstado[chave] = Math.min(Math.max(0, resumoEstado[chave]), ultima);
      const itens = lista.slice(pag * POR_PAGINA, (pag + 1) * POR_PAGINA);
      return { itens, html: lista.length > POR_PAGINA ? `<nav class="res-pag" aria-label="${rotulo}"><span>${pag * POR_PAGINA + 1}–${pag * POR_PAGINA + itens.length} de ${lista.length}</span>
        <button type="button" class="res-btn" ${atributo}="-1" aria-disabled="${pag === 0}">Anterior</button>
        <button type="button" class="res-btn" ${atributo}="1" aria-disabled="${pag === ultima}">Seguinte</button></nav>` : '' };
    }

    const rNeg = v => `<span class="res-neg">${svgIc('descida', 13)}${rEur(v)}</span>`;

    // Barras empilhadas em euros: F e V acima do zero, N e C abaixo, e a linha do líquido. Cada mês abre a dica com o rato ou o teclado.
    function graficoFaturacaoHtml(ms) {
      const max = Math.max(1, ...ms.map(x => x.total.bruto)), min = Math.min(0, ...ms.map(x => x.total.creditos));
      const pct = v => v / (max - min) * 100, zero = pct(-min), n = ms.length;
      const barra = (cls, base, altura) => `<i class="${cls}" style="bottom:${base.toFixed(2)}%;height:${altura.toFixed(2)}%"></i>`;
      const colunas = ms.map((x, i) => {
        const [F, V, N, C] = [x.fat.bruto, x.rec.bruto, x.fat.creditos, x.rec.creditos].map(v => pct(Math.abs(v)));
        const nome = `${MESES[x.m - 1]} ${x.a}`;
        const linhas = [[TIPOS_DOC.F, x.fat.bruto], [TIPOS_DOC.V, x.rec.bruto], [TIPOS_DOC.N, x.fat.creditos], [TIPOS_DOC.C, x.rec.creditos], ['Faturação líquida', x.total.liquido]];
        return `<div class="res-fat-col${x.parcial ? ' res-fat-col--parcial' : ''}${i >= n / 2 ? ' res-fat-col--esq' : ''}" tabindex="0" role="img" aria-label="${rEsc(`${nome}${x.parcial ? ', mês incompleto' : ''}: ${linhas.map(([a, b]) => `${a} ${rEur(b)}`).join(', ')}`)}">
          ${barra('f', zero, F)}${barra('v', zero + F, V)}${barra('n', zero - N, N)}${barra('c', zero - N - C, C)}
          <span class="res-fat-dica" aria-hidden="true"><b>${nome}${x.parcial ? ' (incompleto)' : ''}</b>${linhas.map(([a, b]) => `<span>${a}<strong>${rEur(b)}</strong></span>`).join('')}</span></div>`;
      }).join('');
      const X = i => (i + .5) / n * 100, Y = x => zero + pct(x.total.liquido);
      const grelha = `grid-template-columns:repeat(${n},minmax(0,1fr))`;
      return `<div class="res-legenda"><span><i class="f"></i>${TIPOS_DOC.F}</span><span><i class="v"></i>${TIPOS_DOC.V}</span><span><i class="n"></i>${TIPOS_DOC.N}</span><span><i class="c"></i>${TIPOS_DOC.C}</span><span><i class="liq"></i>Faturação líquida</span></div>
        <div class="res-fat" role="group" aria-label="Faturação por mês, em euros: use a tecla Tab para ouvir cada mês" style="${grelha}">
          ${colunas}<span class="res-fat-zero" style="bottom:${zero.toFixed(2)}%"></span>
          <svg viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true"><path d="${ms.map((x, i) => `${i ? 'L' : 'M'}${X(i).toFixed(2)} ${(100 - Y(x)).toFixed(2)}`).join(' ')}" fill="none" stroke="#0b0f17" stroke-width="2" vector-effect="non-scaling-stroke"/></svg>
          ${ms.map((x, i) => `<b class="res-fat-ponto" style="left:${X(i).toFixed(2)}%;bottom:${Y(x).toFixed(2)}%"></b>`).join('')}
        </div>
        <div class="res-fat-rot" aria-hidden="true" style="${grelha}">${ms.map(x => `<span>${MESES[x.m - 1].slice(0, 3).toLowerCase()}${x.parcial ? '*' : ''}</span>`).join('')}</div>`;
    }

    // Cascata: do bruto ao líquido, com os mesmos números dos cartões
    function cascataHtml(f) {
      const topo = Math.max(1, f.total.bruto);
      const coluna = (nome, v, cls, base, altura) => `<div>${v < 0 ? `<strong class="res-neg">${svgIc('descida', 13)}${rEur(v)}</strong>` : `<strong>${rEur(v)}</strong>`}
        <div class="res-casc-barra"><i class="${cls}" style="bottom:${(base / topo * 100).toFixed(2)}%;height:${(altura / topo * 100).toFixed(2)}%"></i></div><span>${nome}</span></div>`;
      let acumulado = 0;
      const passos = [['Bruto F', f.fat.bruto, 'f'], ['Bruto V', f.rec.bruto, 'v'], ['Menos N', f.fat.creditos, 'n'], ['Menos C', f.rec.creditos, 'c']].map(([nome, v, cls]) => {
        const base = Math.min(acumulado, acumulado + v);
        acumulado += v;
        return coluna(nome, v, cls, base, Math.abs(v));
      });
      const descricao = `Bruto de faturas ${rEur(f.fat.bruto)}, bruto de faturas-recibo ${rEur(f.rec.bruto)}, notas de crédito ${rEur(f.fat.creditos)}, créditos ${rEur(f.rec.creditos)}, faturação líquida ${rEur(f.total.liquido)}`;
      return `<div class="res-casc" role="img" aria-label="${rEsc(descricao)}">${passos.join('')}${coluna('Faturação líquida', f.total.liquido, 'liq', 0, Math.max(0, f.total.liquido))}</div>`;
    }

    // Lista de créditos: a página, a pesquisa e os contadores vêm do servidor. O que já veio fica em credRes para o painel se poder redesenhar.
    const CRED_POR_PAGINA = 15, DOC_ID = /^[A-Za-z0-9]{1,10}-[A-Za-z0-9]{1,10}-[0-9]{4}-[0-9]{1,12}$/;
    const SEM_ORIGEM = 'A base de dados ainda não indica de que fatura vem este crédito.';
    const credRes = { dados: null, erro: false, chave: null, pedidos: 0 };
    let credEspera;
    const docId = doc => doc.replace(' ', '-').replaceAll('/', '-');
    const credTotal = () => { const c = credRes.dados?.contadores; return !c ? 0 : resumoEstado.credTipo === 'Todos' ? c.N + c.C : c[resumoEstado.credTipo]; };

    function creditosHtml() {
      // Outro período, distrito ou cliente: é outra lista. Depois do desenho, porque o pedido escreve no corpo da tabela.
      const chave = [resumoEstado.periodo, resumoEstado.zona, resumoEstado.cliente].join('|');
      if (credRes.chave !== chave) { credRes.chave = chave; credRes.dados = null; resumoEstado.credPag = 0; queueMicrotask(carregarCreditos); }
      const aviso = credRes.erro ? `<p class="res-nota" role="alert">Não foi possível carregar os créditos. <button type="button" class="doc-link" data-cred-repetir>Tentar de novo</button>; se continuar, avise a equipa técnica.</p>` : '';
      if (!credRes.dados) return aviso || '<p class="res-nota" role="status">A carregar os créditos…</p>';

      const tipo = resumoEstado.credTipo, { itens, contadores: c } = credRes.dados, total = credTotal(), pag = resumoEstado.credPag;
      const filtros = [['Todos', 'Todos', c.N + c.C], ['N', TIPOS_DOC.N, c.N], ['C', TIPOS_DOC.C, c.C]].map(([id, nome, n]) =>
        `<button type="button" class="res-pilula" data-cred-tipo="${id}" aria-pressed="${tipo === id}">${nome}<span>${n}</span></button>`).join('');
      const ultima = Math.max(0, Math.ceil(total / CRED_POR_PAGINA) - 1);
      const navegar = total > CRED_POR_PAGINA ? `<nav class="res-pag" aria-label="Páginas de créditos"><span>${pag * CRED_POR_PAGINA + 1}–${pag * CRED_POR_PAGINA + itens.length} de ${total}</span>
        <button type="button" class="res-btn" data-cred-pag="-1" aria-disabled="${pag === 0}">Anterior</button>
        <button type="button" class="res-btn" data-cred-pag="1" aria-disabled="${pag >= ultima}">Seguinte</button></nav>` : '';
      return `${aviso}<div class="res-pilulas" role="group" aria-label="Filtrar por tipo de crédito">${filtros}</div>
        ${itens.length ? `<div class="res-tab-linha res-tab-cab res-tab--cred" aria-hidden="true"><span>Documento</span><span>Data</span><span>Cliente</span><span>Valor</span><span>Documento de origem</span></div>
          ${itens.map(x => { const id = docId(x.doc); return `<div class="res-tab-linha res-tab--cred" data-doc-linha="${rEsc(id)}"><span><button type="button" class="doc-link res-tab-doc" data-doc-abrir="${rEsc(id)}" aria-label="Abrir o documento ${rEsc(x.doc)}">${rEsc(x.doc)}</button></span><span>${rEsc(x.data)}</span><span class="res-tab-nome"><button type="button" class="doc-link" data-doc-cliente="${rEsc(x.clienteId)}" data-doc-nome="${rEsc(x.cliente)}" title="${rEsc(x.cliente)}" aria-label="Abrir a ficha de ${rEsc(nomeProprio(x.cliente))}">${rEsc(nomeProprio(x.cliente))}</button></span>${rNeg(x.total)}<span title="${SEM_ORIGEM}">—</span></div>`; }).join('')}`
          : `<p class="res-nota">${resumoEstado.credBusca.trim() ? 'Nenhum crédito com esta pesquisa. Experimente só o número ou o início do nome do cliente.' : 'Nenhum crédito com estes filtros. Experimente outro tipo ou um período maior.'}</p>`}
        ${navegar}`;
    }
    const desenharCreditos = () => { const corpo = document.getElementById('res-creditos-corpo'); if (corpo) corpo.innerHTML = creditosHtml(); };

    // Enquanto espera, a lista anterior fica no ecrã: o foco de quem usa teclado não se perde. foco: o controlo a focar no fim.
    async function carregarCreditos(foco) {
      const n = ++credRes.pedidos, e = resumoEstado;
      const q = new URLSearchParams({ periodo: e.periodo, page: e.credPag + 1 });
      if (e.credTipo !== 'Todos') q.set('tipo', e.credTipo);
      if (e.credBusca.trim()) q.set('q', e.credBusca.trim());
      if (e.zona) q.set('zona', e.zona);
      if (e.cliente) q.set('cliente', e.cliente);
      try {
        const r = await fetch('/api/creditos?' + q);
        if (!r.ok) throw new Error('HTTP ' + r.status);
        const j = await r.json();
        if (n !== credRes.pedidos) return;
        credRes.dados = { itens: j.itens.map(c => ({ doc: c.documento, tipo: c.tipo, data: c.data, clienteId: c.clienteId, cliente: c.cliente, total: c.total })), contadores: j.contadores };
        credRes.erro = false;
      } catch (erro) {
        if (n !== credRes.pedidos) return;
        console.error('Falha a carregar os créditos:', erro);
        credRes.erro = true;
      }
      desenharCreditos();
      if (typeof foco === 'string') painelEl.querySelector(foco)?.focus();
    }

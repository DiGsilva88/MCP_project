    function ligarResumo(raiz) {
      const redesenhar = () => painelEl.replaceChildren(folhaResumo());
      raiz.addEventListener('submit', e => {
        e.preventDefault();
        pedirResumo(null, String(new FormData(e.target).get('cliente') || '').replace(/\s+/g, ' ').trim());
      });
      // Só redesenha o corpo da tabela: o campo de pesquisa mantém o foco e o cursor
      raiz.addEventListener('input', e => {
        if (e.target.id !== 'res-cred-busca') return;
        Object.assign(resumoEstado, { credBusca: e.target.value, credPag: 0 });
        // Espera que pare de escrever: cada letra seria uma consulta à BD
        clearTimeout(credEspera);
        credEspera = setTimeout(carregarCreditos, 250);
      });
      raiz.addEventListener('click', e => {
        const b = e.target.closest('button');
        if (!b) { const linha = e.target.closest('[data-doc-linha]'); if (linha) abrirDoc(linha.dataset.docLinha); return; }
        const d = b.dataset;
        if (d.docAbrir) { abrirDoc(d.docAbrir); return; }
        if (d.docCliente) { abrirFicha(d.docCliente, d.docNome); return; }
        if ('credRepetir' in d) { credRes.erro = false; carregarCreditos(); return; }
        if (d.credPag) { if (b.getAttribute('aria-disabled') === 'true') return; resumoEstado.credPag += +d.credPag; carregarCreditos(`[data-cred-pag="${d.credPag}"]`); return; }
        if (d.credTipo) { Object.assign(resumoEstado, { credTipo: d.credTipo, credPag: 0 }); carregarCreditos(`[data-cred-tipo="${d.credTipo}"]`); return; }
        if (d.menu) { resumoEstado.menu = resumoEstado.menu === d.menu ? null : d.menu; redesenhar(); painelEl.querySelector(`[data-menu="${d.menu}"]`)?.focus(); return; }
        if ('inicio' in d) { boasVindasAberta = true; desenharPainel(); painelEl.querySelector('input')?.focus(); return; }
        if (d.verSecao) { secao = d.verSecao; verInicio(); return; }
        if (d.ficha) { abrirFicha(d.ficha, d.fichaNome); return; }
        // Redesenha e devolve o foco ao mesmo controlo: quem usa teclado não perde o lugar
        const atualizar = seletor => { redesenhar(); desenharNav(); painelEl.querySelector(seletor)?.focus(); };
        if (d.cliSel) { resumoEstado.cliSel = d.cliSel; atualizar(`[data-cli-sel="${CSS.escape(d.cliSel)}"]`); return; }
        if (d.cliPag) { resumoEstado.cliPag += +d.cliPag; atualizar(`[data-cli-pag="${d.cliPag}"]`); return; }
        if (d.cliSeg) { resumoEstado.cliSeg = d.cliSeg; resumoEstado.cliPag = 0; atualizar(`[data-cli-seg="${d.cliSeg}"]`); return; }
        if (d.alertaFiltro) { resumoEstado.alertaFiltro = d.alertaFiltro; resumoEstado.alertaSel = null; atualizar(`[data-alerta-filtro="${d.alertaFiltro}"]`); return; }
        if (d.alerta != null) {
          const a = alertasVisiveis(resumoEstado.dados)[+d.alerta];
          resumoEstado.alertaSel = a.id; alertasLidos.add(a.id); guardarLidos();
          // No separador "Por ler" o alerta sai da lista ao ser lido: o foco vai para a primeira linha
          atualizar(`.res-al-linha[aria-pressed="true"], .res-al-linha`);
          return;
        }
        if ('alertaTodos' in d) { alertasAtivos(resumoEstado.dados).forEach(a => alertasLidos.add(a.id)); guardarLidos(); atualizar('.res-al-todas'); return; }
        if ('alertaLer' in d || 'alertaAbrir' in d) {
          const lista = alertasVisiveis(resumoEstado.dados), a = lista.find(x => x.id === resumoEstado.alertaSel) || lista[0];
          if ('alertaAbrir' in d) { abrirAlerta(a); return; }
          if (alertasLidos.has(a.id)) alertasLidos.delete(a.id); else alertasLidos.add(a.id);
          guardarLidos(); atualizar('[data-alerta-ler]');
          return;
        }
        if (d.regra) {
          regrasAlerta[d.regra] = regrasAlerta[d.regra] === false;
          guardarValor('localStorage', 'flow-alertas-regras', regrasAlerta);
          atualizar(`[data-regra="${d.regra}"]`);
          return;
        }
        if (d.mes != null) { resumoEstado.mes = +d.mes; redesenhar(); painelEl.querySelector(`[data-mes="${d.mes}"]`)?.focus(); return; }
        if ('fechar' in d) { resumoEstado.menu = null; redesenhar(); painelEl.querySelector('[data-menu]')?.focus(); return; }
        if (d.periodo) {
          resumoEstado.periodo = d.periodo;
          carregarResumo();
          return;
        }
        if ('zona' in d) { resumoEstado.zona = !d.zona || d.zona === resumoEstado.zona ? null : d.zona; carregarResumo(); return; }
        if (d.cliente) { pedirResumo(null, d.cliente); return; }
        if ('semCliente' in d) { pedirResumo(null); return; }
        if ('repetir' in d) { carregarResumo(); return; }
        if ('limpar' in d) { Object.assign(resumoEstado, { zona: null, cliente: null, periodo: 'AnoAtual' }); carregarResumo(); return; }
        if (d.perguntar) { verConversa(true); prepararPergunta(d.perguntar); }
      });
    }

    // "Ver em…" de um alerta: leva à vista certa, já com o distrito filtrado ou o cliente escolhido
    function abrirAlerta(a) {
      alertasLidos.add(a.id); guardarLidos();
      secao = a.ver;
      if (a.zona) { pedirResumo(null, null, a.zona); return; }
      if (a.cliente) Object.assign(resumoEstado, { cliSel: a.cliente, cliSeg: 'Todos', cliPag: 0 });
      verInicio();
    }

    // Botão "Ficha do cliente": mostra o que a ferramenta consultar devolve para esse cliente (CSV: cabeçalho e linha)
    const dlgFicha = document.getElementById('dlg-ficha'), dlgFichaCorpo = document.getElementById('dlg-ficha-corpo');
    let fichaAberta = null, pedidosFicha = 0;
    async function abrirFicha(id, nome) {
      const n = ++pedidosFicha;
      fichaAberta = id;
      document.getElementById('dlg-ficha-titulo').textContent = nome ? nomeProprio(nome) : 'Ficha do cliente';
      document.getElementById('dlg-ficha-resumo').hidden = true;
      dlgFichaCorpo.textContent = 'A carregar a ficha…';
      dlgFicha.showModal();
      try {
        const r = await fetch('/api/clientes/' + encodeURIComponent(id));
        if (r.status === 404) { if (n === pedidosFicha) dlgFichaCorpo.textContent = 'Este cliente não tem ficha ou não pertence à sua carteira.'; return; }
        if (!r.ok) throw new Error('HTTP ' + r.status);
        const ls = (await r.text()).split(/\r?\n/).filter(l => l.trim() && !l.startsWith('#'));
        if (n !== pedidosFicha) return;
        if (!/^NomeCliente,/.test(ls[0] || '')) { dlgFichaCorpo.textContent = ls[0] || 'Este cliente não tem ficha.'; return; }
        const cab = partir(ls[0], ','), linhas = ls.slice(1).map(l => partir(l, ','));
        // O filtro pelo ID também apanha nomes começados por esses dígitos: fica a linha do cliente pedido
        const v = linhas.find(l => nome && semAcento(l[0]) === semAcento(nome)) || linhas[0];
        if (!nome) document.getElementById('dlg-ficha-titulo').textContent = nomeProprio(v[0]);
        const dl = el('dl', 'chat-ficha');
        cab.forEach((k, i) => { if (i) dl.append(el('dt', '', rotuloCampo(k)), el('dd', '', v[i] ?? '')); });
        dlgFichaCorpo.replaceChildren(dl);
        document.getElementById('dlg-ficha-resumo').hidden = resumoEstado.cliente === id;
      } catch (erro) {
        if (n !== pedidosFicha) return;
        console.error('Falha a abrir a ficha:', erro);
        dlgFichaCorpo.textContent = 'Não foi possível abrir a ficha. Tente de novo; se continuar, o detalhe está na consola do agente.';
      }
    }
    dlgFicha.querySelector('form').addEventListener('submit', e => {
      if (e.submitter?.value === 'resumo') { if (dlgDoc.open) dlgDoc.close(); pedirResumo(null, fichaAberta); }
    });

    // Painel do documento. A navegação pela origem empilha; as setas passam ao lado e limpam a pilha.
    const dlgDoc = document.getElementById('dlg-doc'), dlgDocCorpo = document.getElementById('dlg-doc-corpo');
    const docNav = { atual: null, pilha: [], pedidos: 0 };
    const docAbreviado = id => id.replace('-', ' ').replaceAll('-', '/');

    function docHtml(d, urlPdf) {
      const t = d.totais, nome = nomeProprio(d.cliente.nome), url = `mailto:?subject=${encodeURIComponent('Documento ' + d.documento)}`;
      const origem = d.origem
        ? `<button type="button" class="doc-link" data-doc-origem="${rEsc(docId(d.origem))}">${rEsc(d.origem)}</button>`
        : `<span title="${SEM_ORIGEM}">Sem referência</span>`;
      const previa = urlPdf ? `<iframe class="doc-pdf" src="${rEsc(urlPdf)}" title="PDF do documento ${rEsc(d.documento)}"></iframe>` : `
        <p class="doc-aviso" role="note"><strong>Pré-visualização, não é o documento fiscal.</strong> Gerada a partir dos dados. O documento oficial é o PDF emitido pela aplicação de faturação.</p>
        <div class="doc-previa">
          <h3>${rEsc(d.documento)} · ${rEsc(d.data)}</h3>
          <p class="res-nota">Cliente: ${rEsc(nome)}</p>
          <table class="doc-tabela"><caption class="so-leitor">Linhas do documento</caption>
            <thead><tr><th scope="col">Artigo</th><th scope="col">Qtd.</th><th scope="col">Líquido</th><th scope="col">IVA</th></tr></thead>
            <tbody>${d.linhas.map(l => `<tr><td>${rEsc(l.artigo)} <span class="res-nota">${rEsc(l.artigoId)}</span></td><td>${rDec(l.quantidade, 2)}</td><td>${rEur2(l.liquido)}</td><td>${rEur2(l.iva)}</td></tr>`).join('')}</tbody>
            <tfoot>
              <tr><th scope="row" colspan="3">Ilíquido</th><td>${rEur2(t.iliquido)}</td></tr>
              <tr><th scope="row" colspan="3">Desconto</th><td>${rEur2(t.desconto)}</td></tr>
              <tr><th scope="row" colspan="3">Líquido</th><td>${rEur2(t.liquido)}</td></tr>
              <tr><th scope="row" colspan="3">IVA</th><td>${rEur2(t.iva)}</td></tr>
              <tr><th scope="row" colspan="3">Total</th><td>${rEur2(t.total)}</td></tr>
            </tfoot>
          </table>
          <p class="res-nota">ATCUD: ${rEsc(d.atcud || 'indisponível nestes dados')} · QR: ${d.qr ? 'disponível' : 'só no documento emitido'}</p>
        </div>`;
      return `<dl class="doc-resumo">
          <dt>Cliente</dt><dd><button type="button" class="doc-link" data-doc-cliente="${rEsc(d.cliente.id)}" data-doc-nome="${rEsc(d.cliente.nome)}">${rEsc(nome)}</button></dd>
          <dt>Data</dt><dd>${rEsc(d.data)}</dd>
          <dt>Valor</dt><dd>${rEur2(t.liquido)}</dd>
          <dt>Origem</dt><dd>${origem}</dd>
        </dl>
        ${d.origem ? '' : `<p class="res-nota">${SEM_ORIGEM} <button type="button" class="doc-link" data-doc-associar>Associar fatura</button> <span id="doc-associar-estado" role="status"></span></p>`}
        <div class="doc-acoes">
          ${urlPdf ? `<a class="res-btn" href="${rEsc(urlPdf)}" download>Descarregar PDF</a>` : `<button type="button" class="res-btn" aria-disabled="true" title="O PDF emitido ainda não está disponível aqui">Descarregar PDF</button>`}
          <a class="res-btn" href="${url}">Enviar por email</a>
          <button type="button" class="res-btn" data-doc-copiar="${rEsc(d.documento)}">Copiar nº</button>
          <span id="doc-copiar-estado" class="res-nota" role="status"></span>
        </div>
        ${previa}`;
    }

    async function mostrarDoc(id) {
      const n = ++docNav.pedidos, url = '/api/documentos/' + encodeURIComponent(id);
      docNav.atual = id;
      history.replaceState(null, '', '#' + id);
      document.getElementById('dlg-doc-titulo').textContent = docAbreviado(id);
      document.getElementById('dlg-doc-voltar').hidden = !docNav.pilha.length;
      painelEl.querySelectorAll('[data-doc-linha]').forEach(l => l.toggleAttribute('aria-current', false));
      painelEl.querySelector(`[data-doc-linha="${CSS.escape(id)}"]`)?.setAttribute('aria-current', 'true');
      dlgDocCorpo.textContent = 'A carregar o documento…';
      if (!dlgDoc.open) dlgDoc.showModal();
      try {
        const [r, pdf] = await Promise.all([fetch(url), fetch(url + '/pdf')]);
        if (n !== docNav.pedidos) return;
        if (r.status === 404) { dlgDocCorpo.textContent = 'Este documento não existe ou não pertence à sua carteira. Confirme o número na lista.'; return; }
        if (!r.ok) throw new Error('HTTP ' + r.status);
        const d = await r.json();
        if (n !== docNav.pedidos) return;
        document.getElementById('dlg-doc-titulo').textContent = d.documento;
        dlgDocCorpo.innerHTML = docHtml(d, pdf.ok ? url + '/pdf' : null);
      } catch (erro) {
        if (n !== docNav.pedidos) return;
        console.error('Falha a abrir o documento:', erro);
        dlgDocCorpo.innerHTML = '<p role="alert">Não foi possível abrir o documento. <button type="button" class="doc-link" data-doc-repetir>Tentar de novo</button>; se continuar, avise a equipa técnica.</p>';
      }
    }
    function abrirDoc(id) { docNav.pilha = []; mostrarDoc(id); }

    // Setas: o documento ao lado na lista, a passar de página quando chega à ponta. Aberto por link direto, não há lista onde andar.
    async function navegarDoc(passo) {
      const itens = credRes.dados?.itens || [], i = itens.findIndex(c => docId(c.doc) === docNav.atual);
      if (i < 0) return;
      if (itens[i + passo]) { abrirDoc(docId(itens[i + passo].doc)); return; }
      const pag = resumoEstado.credPag + passo;
      if (pag < 0 || pag * CRED_POR_PAGINA >= credTotal()) return;
      resumoEstado.credPag = pag;
      await carregarCreditos();
      const nova = credRes.dados?.itens || [], alvo = passo > 0 ? nova[0] : nova[nova.length - 1];
      if (alvo) abrirDoc(docId(alvo.doc));
    }

    dlgDoc.addEventListener('keydown', e => {
      if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && !e.target.closest('input, textarea, select')) { e.preventDefault(); navegarDoc(e.key === 'ArrowDown' ? 1 : -1); }
    });
    dlgDoc.addEventListener('click', async e => {
      const b = e.target.closest('button'); if (!b) return;
      const d = b.dataset;
      if (b.id === 'dlg-doc-fechar') { dlgDoc.close(); return; }
      if (b.id === 'dlg-doc-voltar') { mostrarDoc(docNav.pilha.pop()); return; }
      if ('docRepetir' in d) { mostrarDoc(docNav.atual); return; }
      if (d.docCliente) { abrirFicha(d.docCliente, d.docNome); return; }
      if (d.docOrigem) { docNav.pilha.push(docNav.atual); mostrarDoc(d.docOrigem); return; }
      if ('docAssociar' in d) { document.getElementById('doc-associar-estado').textContent = 'Simulação: nenhuma associação foi gravada.'; return; }
      if (d.docCopiar) {
        const estado = document.getElementById('doc-copiar-estado');
        // A área de transferência só funciona em HTTPS ou localhost
        try { await navigator.clipboard.writeText(d.docCopiar); estado.textContent = 'Número copiado.'; }
        catch { estado.textContent = 'Não foi possível copiar. Selecione o número no título.'; }
      }
    });
    dlgDoc.addEventListener('close', () => {
      const id = docNav.atual;
      docNav.atual = null; docNav.pedidos++;
      history.replaceState(null, '', location.pathname + location.search);
      painelEl.querySelectorAll('[aria-current][data-doc-linha]').forEach(l => l.removeAttribute('aria-current'));
      painelEl.querySelector(`[data-doc-abrir="${CSS.escape(id || '')}"]`)?.focus();
    });
    const abrirDoHash = () => { const id = location.hash.slice(1); if (DOC_ID.test(id) && id !== docNav.atual) abrirDoc(id); };
    window.addEventListener('hashchange', abrirDoHash);
    abrirDoHash();

    // Tooltip da previsão: rato e teclado
    function ligarPrevisao(raiz, P) {
      const tip = raiz.querySelector('#res-tooltip');
      if (!P || !tip) return;
      raiz.querySelectorAll('[data-prev]').forEach(z => {
        const mostrar = () => {
          const i = +z.dataset.prev, off = P.real.length - 1, real = i <= off;
          tip.style.left = Math.min(Math.max(parseFloat(z.style.left) + 4.6, 14), 80) + '%';
          tip.innerHTML = `<strong>${P.nomes[i]}</strong><span>${real ? 'Vendas: ' + rEur(P.real[i]) : 'Previsão: ' + rEur(P.previsto[i - off])}</span>
            <span style="color:var(--nota)">${real ? (i === off ? 'Último mês completo' : 'Valor real') : 'Intervalo provável: ' + rEur(P.inferior[i - off]) + ' a ' + rEur(P.superior[i - off])}</span>`;
          tip.hidden = false;
        };
        z.addEventListener('mouseenter', mostrar); z.addEventListener('focus', mostrar);
        z.addEventListener('mouseleave', () => { tip.hidden = true; }); z.addEventListener('blur', () => { tip.hidden = true; });
      });
    }

    // Esc fecha o menu do Resumo
    document.addEventListener('keydown', e => {
      if (e.key === 'Escape' && resumoEstado.menu && vistaResumo) {
        resumoEstado.menu = null; painelEl.replaceChildren(folhaResumo());
        painelEl.querySelector('[data-menu]')?.focus();
      }
    });

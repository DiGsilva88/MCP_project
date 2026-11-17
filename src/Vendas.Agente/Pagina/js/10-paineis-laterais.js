    /* =========================================================
       Painéis laterais: Filtros, Briefing do cliente, Pré-visualização de documento, Agendar envio e Editar alerta.
       Todos só leem; nenhum escreve na base de dados do ERP.
       ========================================================= */

    /* ---------- Filtros: valem para todos os separadores e chegam às consultas do servidor ---------- */
    function abrirFiltros() {
      const zonas = (app.dados?.zonas || []).filter(z => !z.sem).map(z => [z.id, z.nome]).sort((a, b) => a[1].localeCompare(b[1], 'pt'));
      if (app.filtros.zona && !zonas.some(z => z[0] === app.filtros.zona)) zonas.unshift([app.filtros.zona, nomeZona(app.filtros.zona)]);
      const form = drawer.abrir({ topo: 'Aplica-se a todos os separadores', form: true,
        html: `<h3 id="dr-t" tabindex="-1">Filtros</h3><div class="form">
          <label>Período<select name="periodo">${PERIODOS.map(([v]) => `<option value="${v}"${v === app.periodo ? ' selected' : ''}>${esc(nomePeriodo(v))}</option>`).join('')}</select></label>
          <label>Cliente<input name="cliente" maxlength="100" autocomplete="off" value="${esc(app.filtros.cliente)}" placeholder="Código ou início do nome"></label>
          <fieldset><legend>Zona</legend><div class="opcoes">${[['', 'Todas as zonas'], ...zonas].map(([v, t]) =>
            `<label class="opcao"><input type="radio" name="zona" value="${esc(v)}"${v === (app.filtros.zona || '') ? ' checked' : ''}> ${esc(t)}</label>`).join('')}</div></fieldset>
        </div>`,
        pe: '<button type="button" class="btn" data-limpar>Limpar filtros</button><button type="submit" class="btn btn-pri">Aplicar filtros</button>' });
      form.querySelector('[data-limpar]').addEventListener('click', () => {
        form.elements.periodo.value = 'AnoAtual'; form.elements.cliente.value = '';
        form.querySelector('input[name="zona"][value=""]').checked = true;
      });
      form.addEventListener('submit', e => {
        e.preventDefault();
        const d = new FormData(form), novos = filtros.limpos({ cliente: d.get('cliente'), zona: d.get('zona') });
        drawer.fechar();
        aplicarFiltros(novos, d.get('periodo'));
        const n = filtros.ativos(novos).length;
        aviso(n ? `${n} ${n > 1 ? 'filtros aplicados' : 'filtro aplicado'} a todos os separadores.` : 'Filtros limpos.');
      });
    }
    function aplicarFiltros(F, periodo = app.periodo) {
      Object.assign(app, { filtros: F, periodo, mes: null, credPag: 0 });
      carregarResumo();
      guardarFiltrosPrefs();   // voltam na próxima sessão, por utilizador
    }

    /* ---------- Briefing antes da visita ---------- */
    let pedidoBriefing = 0;
    async function abrirBriefing(id, nome) {
      const n = ++pedidoBriefing, c = (app.dados?.carteira || []).find(x => String(x.codigo) === String(id)), D = app.dados;
      const titulo = nomeProprio(nome || c?.nome || id);
      const resumoTexto = !c ? 'Cliente fora dos 100 com mais faturação no período.'
        : estadoCliente(c) === 'sem' ? `Sem compras recentes. Última compra a ${fmt.dataLonga(c.ultima)}.`
        : estadoCliente(c) === 'desvio' ? `Compra com regularidade, mas está ${fmt.pct(Math.abs(c.delta))} abaixo de ${D.anoAnt}. Última compra a ${fmt.dataLonga(c.ultima)}.`
        : c.delta != null ? `Cliente ativo. Faturação ${c.delta >= 0 ? 'acima' : 'abaixo'} de ${D.anoAnt} (${fmt.pct(Math.abs(c.delta))}).${c.ultima ? ` Última compra a ${fmt.dataLonga(c.ultima)}.` : ''}`
        : `Cliente ativo${c.ultima ? `. Última compra a ${fmt.dataLonga(c.ultima)}` : ''}.`;
      const ano = D?.datas?.[1] ? D.datas[1].slice(-4) : anoAtual(), espera = '<span class="muted">A carregar…</span>';
      const factos = [...(c ? [['Zona', esc(nomeProprio(c.zona || '—'))], [app.periodo === 'AnoAtual' ? `Faturação em ${ano}` : `Faturação (${nomePeriodo(app.periodo).toLowerCase()})`, fmt.eur(c.num)],
        [D.anoAnt ? 'Face a ' + D.anoAnt : 'Variação', c.novo ? 'Novo' : variacaoHtml(c.delta)], ['Última compra', esc(c.ultima ? fmt.dataCurta(c.ultima) : '—')]] : []),
        ['Artigo mais comprado', espera, 'b-artigo'], ['Faturas por pagar', espera, 'b-aberto']];
      const el = drawer.abrir({ topo: 'Briefing antes da visita',
        html: `<h3 id="dr-t" tabindex="-1">${esc(titulo)}</h3><p>${esc(resumoTexto)}</p>
          <ul class="factos">${factos.map(([k, v, id]) => `<li><span>${esc(k)}</span><b class="num"${id ? ` id="${id}"` : ''}>${v}</b></li>`).join('')}</ul>
          <h4 class="bloco-t">Artigos que deixou de comprar <span class="muted" id="b-n"></span></h4><div id="b-parados">${aCarregar('A procurar os artigos habituais…')}</div>`,
        pe: `<button type="button" class="btn" data-fechar>Fechar</button>${app.vendedor ? '' : `<button type="button" class="btn btn-pri" data-perguntar-briefing>${ic('flow')}Perguntar ao Flow</button>`}` });
      el.querySelector('[data-fechar]').addEventListener('click', () => drawer.fechar());
      el.querySelector('[data-perguntar-briefing]')?.addEventListener('click', () => { drawer.fechar(false); perguntarFlow(`O que devo saber antes de visitar o cliente ${titulo}?`); });
      el.addEventListener('click', e => { const b = e.target.closest('[data-perguntar-artigo]'); if (b) { drawer.fechar(false); perguntarFlow(`Porque é que o cliente ${titulo} deixou de comprar ${b.dataset.perguntarArtigo}?`); } });
      const aqui = () => n === pedidoBriefing && document.getElementById('b-parados');
      try {
        const r = await fetch(`/api/clientes/${encodeURIComponent(id)}/briefing`);
        if (!aqui()) return;
        if (r.status === 404) throw Object.assign(new Error('404'), { semCliente: true });
        if (!r.ok) throw new Error('HTTP ' + r.status);
        const b = await r.json();
        if (!aqui()) return;
        const a = b.emAberto, m = b.maisComprado;
        document.getElementById('b-artigo').textContent = m ? nomeProprio(m.artigo) : 'Sem compras nos últimos 12 meses';
        document.getElementById('b-aberto').innerHTML = !a.documentos ? 'Nenhuma'
          : `${fmt.inteiro(a.documentos)} ${a.documentos > 1 ? 'documentos' : 'documento'}, ${fmt.eur2(a.total)}${a.vencido > 0 ? `<span class="celula-sub">${fmt.eur2(a.vencido)} vencidos</span>` : ''}`;
        document.getElementById('b-n').textContent = b.deixouDeComprar.length;
        document.getElementById('b-parados').innerHTML = b.deixouDeComprar.length
          ? `<ul class="factos" style="margin-top:8px">${b.deixouDeComprar.map(x => `<li><div><b style="font-weight:500">${esc(nomeProprio(x.artigo))}</b>
              <div class="muted" style="font-size:12.5px;margin-top:2px">Comprava ${esc(fmt.decimal(x.mediaMes, x.mediaMes % 1 ? 1 : 0))} por mês, em média. Última compra a ${esc(fmt.dataCurta(x.ultima))}.</div></div>
              ${app.vendedor ? '' : `<button type="button" class="ligacao" data-perguntar-artigo="${esc(nomeProprio(x.artigo).toLowerCase())}">Perguntar</button>`}</li>`).join('')}</ul>`
          : '<p class="muted" style="margin-top:8px">Continua a comprar os artigos habituais.</p>';
      } catch (e) {
        if (!aqui()) return;
        console.error('Falha a carregar o briefing:', e);
        ['b-artigo', 'b-aberto'].forEach(i => { document.getElementById(i).textContent = '—'; });
        document.getElementById('b-parados').innerHTML = e.semCliente ? '<p class="muted" style="margin-top:8px">Este cliente não pertence à sua carteira.</p>'
          : comErro('Não foi possível carregar o briefing. Tente outra vez.', '');
      }
    }

    /* ---------- Pré-visualização de documento (só leitura) ---------- */
    const DOC_ID = /^[A-Za-z0-9]{1,10}-[A-Za-z0-9]{1,10}-[0-9]{4}-[0-9]{1,12}$/;
    let pedidoDoc = 0;
    async function abrirDocumento(id) {
      const n = ++pedidoDoc, numero = id.replace('-', ' ').replace(/-/g, '/');
      const el = drawer.abrir({ topo: 'Pré-visualização', largo: true, aoFechar: () => { if (location.hash) history.replaceState(null, '', location.pathname + location.search); },
        html: `<h3 id="dr-t" tabindex="-1" class="so-leitor">Documento ${esc(numero)}</h3><div id="doc-corpo">${aCarregar('A carregar o documento…')}</div>`,
        pe: `<button type="button" class="btn" data-fechar>Fechar</button><button type="button" class="btn" data-doc-pdf disabled>${ic('exportar')}Exportar PDF</button>${app.vendedor ? '' : `<button type="button" class="btn btn-pri" data-doc-flow disabled>${ic('flow')}Perguntar ao Flow</button>`}` });
      history.replaceState(null, '', '#' + id);
      el.querySelector('[data-fechar]').addEventListener('click', () => drawer.fechar());
      const corpo = () => n === pedidoDoc ? document.getElementById('doc-corpo') : null;
      try {
        const r = await fetch('/api/documentos/' + encodeURIComponent(id));
        if (!corpo()) return;
        if (r.status === 404) { corpo().innerHTML = '<p class="muted">Este documento não existe ou não pertence à sua carteira.</p>'; return; }
        if (!r.ok) throw new Error('HTTP ' + r.status);
        const d = await r.json(), t = d.totais;
        if (!corpo()) return;
        document.getElementById('dr-t').textContent = `${TIPOS_DOC[d.tipo] || 'Documento'} ${d.documento}`;
        corpo().innerHTML = `<div class="folha">
            <div class="folha-topo"><div><h3>${esc((TIPOS_DOC[d.tipo] || 'Documento').replace(/\s*\(.\)$/, ''))}</h3><div class="muted num" style="margin-top:6px;font-size:12.5px">N.º ${esc(d.documento)}<br>Data: ${esc(fmt.dataLonga(d.data, true))}</div></div><span style="color:var(--blue)">${logo()}</span></div>
            <div class="folha-para"><span class="muted">Cliente</span><b style="font-weight:500">${esc(nomeProprio(d.cliente.nome))}</b><span class="muted">${esc(d.cliente.id)}</span></div>
            ${tabela({ legenda: 'Linhas do documento', colunas: [{ t: 'Artigo' }, { t: 'Qtd.', dir: true }, { t: 'Preço', dir: true }, { t: 'Total', dir: true }],
              linhas: d.linhas.map(l => ({ c: [`${esc(l.artigo)}<span class="celula-sub">${esc(l.artigoId)}</span>`, fmt.decimal(l.quantidade, Number.isInteger(l.quantidade) ? 0 : 2),
                l.quantidade ? fmt.eur2(l.liquido / l.quantidade) : '—', fmt.eur2(l.liquido)] })) })}
            <ul class="folha-somas"><li><span>Total sem IVA</span><b class="num">${fmt.eur2(t.liquido)}</b></li><li><span>IVA</span><b class="num">${fmt.eur2(t.iva)}</b></li><li class="total"><span>Total</span><b class="num">${fmt.eur2(t.total)}</b></li></ul>
          </div><p class="nota-pe">Pré-visualização gerada a partir dos dados, não é o documento fiscal. Só se pode consultar.</p>`;
        const pdf = el.querySelector('[data-doc-pdf]'), flow = el.querySelector('[data-doc-flow]');
        pdf.disabled = false;
        pdf.addEventListener('click', () => aviso(exportar('pdf', { titulo: d.documento }, true)));
        if (flow) { flow.disabled = false; flow.addEventListener('click', () => { drawer.fechar(false); perguntarFlow(`O que comprou o cliente ${nomeProprio(d.cliente.nome)} no documento ${d.documento}?`); }); }
      } catch (e) {
        console.error('Falha a abrir o documento:', e);
        if (corpo()) corpo().innerHTML = comErro('Não foi possível abrir o documento. Tente outra vez.', '');
      }
    }

    /* ---------- Agendar envio: o envio real que já existe (resumo por email às segundas, só para endereços da empresa) ---------- */
    const EMAIL_EMPRESA = 'inforap.pt';
    const emailValido = e => new RegExp(`^[^\\s@<>]+@${EMAIL_EMPRESA.replace('.', '\\.')}$`, 'i').test(e);
    const fraseEnvio = (freq, para) => `${freq === 'mensal' ? 'Na primeira segunda-feira de cada mês' : 'Todas as segundas-feiras'}, de manhã, envia o resumo das vendas ${freq === 'mensal' ? 'do mês anterior' : 'da semana anterior'} para ${para}.`;
    async function abrirAgendar() {
      const st = { freq: 'semanal', para: [] };
      const form = drawer.abrir({ topo: 'Agendar envio', form: true,
        html: `<h3 id="dr-t" tabindex="-1">Enviar o resumo por email</h3><div class="form">
          <label>O que enviar<input readonly value="Resumo das vendas (no corpo do email)"></label>
          <div class="campo"><label for="ag-email">Para</label><div class="emails" id="ag-emails"><input id="ag-email" type="email" autocomplete="off" placeholder="Escreva um email @${EMAIL_EMPRESA} e carregue em Enter" aria-describedby="ag-erro"></div>
            <span class="erro-campo" id="ag-erro" role="alert"></span></div>
          <label>Repetir<select name="freq"><option value="semanal">Todas as semanas</option><option value="mensal">Todos os meses</option></select></label></div>
          <div class="frase"><span class="muted">O envio fica assim</span><p id="ag-frase"></p></div>
          <h4 class="bloco-t">Envios agendados <span class="muted" id="ag-n"></span></h4><div id="ag-lista">${aCarregar('A carregar os envios agendados…')}</div>`,
        pe: `<button type="button" class="btn" data-fechar>Cancelar</button><button type="submit" class="btn btn-pri">${ic('calendario')}Agendar envio</button>` });
      const $ = s => form.querySelector(s), entrada = $('#ag-email'), erro = $('#ag-erro');
      const pintar = () => {
        $('#ag-frase').textContent = fraseEnvio(st.freq, st.para.length ? (st.para.length === 1 ? st.para[0] : st.para.length + ' pessoas') : 'quem escolher');
        form.querySelectorAll('.etiqueta').forEach(x => x.remove());
        st.para.forEach((m, i) => entrada.insertAdjacentHTML('beforebegin', `<span class="etiqueta">${esc(m)}<button type="button" aria-label="Remover ${esc(m)}" data-tirar="${i}">${ic('fechar', 11)}</button></span>`));
      };
      const acrescentar = () => {
        const v = entrada.value.trim().replace(/[,;]$/, '');
        if (!v) return true;
        if (!emailValido(v)) { erro.textContent = `Este email não é válido. Os dados de vendas só podem ir para endereços @${EMAIL_EMPRESA}.`; entrada.setAttribute('aria-invalid', 'true'); return false; }
        if (!st.para.some(p => p.toLowerCase() === v.toLowerCase())) st.para.push(v);
        entrada.value = ''; erro.textContent = ''; entrada.removeAttribute('aria-invalid');
        pintar();
        return true;
      };
      entrada.addEventListener('keydown', e => {
        if (e.key === 'Enter' || e.key === ',' || e.key === ';') { e.preventDefault(); acrescentar(); }
        else if (e.key === 'Backspace' && !entrada.value && st.para.length) { st.para.pop(); pintar(); }
      });
      entrada.addEventListener('blur', acrescentar);
      $('#ag-emails').addEventListener('click', e => {
        const t = e.target.closest('[data-tirar]');
        if (t) { st.para.splice(+t.dataset.tirar, 1); pintar(); entrada.focus(); return; }
        entrada.focus();
      });
      form.elements.freq.addEventListener('change', e => { st.freq = e.target.value; pintar(); });
      form.querySelector('[data-fechar]').addEventListener('click', () => drawer.fechar());
      const enviar = (freq, email) => fetch('/agendamentos', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ freq, email }) })
        .then(async r => ({ ok: r.ok, texto: (await r.text()).trim() }), () => ({ ok: false, texto: 'O servidor não respondeu.' }));
      async function listar() {
        const alvo = form.querySelector('#ag-lista');
        if (!alvo) return;
        try {
          const lista = await pedirJson('/api/agendamentos');
          if (!form.isConnected) return;
          form.querySelector('#ag-n').textContent = lista.length || '';
          alvo.innerHTML = lista.length ? `<ul class="factos">${lista.map(a => `<li><span style="color:var(--black);line-height:1.45">${esc(fraseEnvio(a.freq, a.email))}</span><button type="button" class="ligacao ligacao-discreta" data-cancelar="${esc(a.email)}">Cancelar</button></li>`).join('')}</ul>`
            : '<p class="muted" style="margin-top:8px">Ainda não agendou nenhum envio.</p>';
        } catch (e) { console.error(e); alvo.innerHTML = comErro('Não foi possível ler os envios agendados.', ''); }
      }
      form.querySelector('#ag-lista').addEventListener('click', async e => {
        const b = e.target.closest('[data-cancelar]');
        if (!b) return;
        b.disabled = true;
        const r = await enviar('nenhum', b.dataset.cancelar);
        aviso(r.ok ? 'Envio agendado cancelado.' : r.texto || 'Não foi possível cancelar.');
        listar();
      });
      form.addEventListener('submit', async e => {
        e.preventDefault();
        if (!acrescentar()) return;
        if (!st.para.length) { erro.textContent = 'Indique pelo menos um email.'; entrada.focus(); return; }
        const botao = form.querySelector('[type="submit"]');
        botao.disabled = true;
        const resultados = await Promise.all(st.para.map(m => enviar(st.freq, m)));
        botao.disabled = false;
        const falhas = resultados.map((r, i) => [r, st.para[i]]).filter(([r]) => !r.ok);
        if (!falhas.length) { drawer.fechar(); aviso(fraseEnvio(st.freq, st.para.length === 1 ? st.para[0] : st.para.length + ' pessoas').replace(/\.$/, '') + '. Envio agendado.'); return; }
        st.para = falhas.map(f => f[1]);
        pintar();
        erro.textContent = falhas.map(([r, m]) => `${m}: ${r.texto || 'não foi possível agendar.'}`).join(' ');
        listar();
      });
      pintar();
      entrada.focus();
      listar();
    }

    /* ---------- Editar um alerta personalizado: limite, frase e o que encontraria hoje ---------- */
    function abrirEditarAlerta(id) {
      const r = alertasRegras.regra(app.prefs.alertas, id), D = app.dados;
      const encontra = limite => {
        if (!D?.alertas) return 'Os dados ainda não carregaram.';
        const l = D.alertas.filter(a => a.regra === id && alertasRegras.conta({ ...app.prefs.alertas, [id]: { ligada: true, limite } }, a));
        return l.length ? `${l.length} ${l.length > 1 ? 'resultados' : 'resultado'}: ${l.slice(0, 6).map(a => a.titulo).join('; ')}${l.length > 6 ? '…' : '.'}` : 'Nada, por agora.';
      };
      const form = drawer.abrir({ topo: 'Alerta personalizado', form: true,
        html: `<h3 id="dr-t" tabindex="-1">${esc(r.nome)}</h3><div class="form">
          ${r.limite != null ? `<label>Limite<span class="em-linha"><input name="limite" type="number" inputmode="numeric" min="${LIMITE_MIN}" max="${LIMITE_MAX}" step="1" value="${r.limite}" aria-describedby="al-erro"><span class="muted">% de queda</span></span></label><span class="erro-campo" id="al-erro" role="alert"></span>` : '<p class="muted">Este alerta usa os 90 dias definidos no servidor.</p>'}
          <label class="opcao"><input type="checkbox" name="ligada"${r.ligada ? ' checked' : ''}> Alerta ativo</label></div>
          <div class="frase"><span class="muted">O alerta fica assim</span><p id="al-frase">${esc(r.frase(r.limite))}</p></div>
          <div class="frase frase--contorno"><span class="muted">Com os dados de hoje, encontraria</span><p id="al-prev">${esc(encontra(r.limite))}</p></div>`,
        pe: '<button type="button" class="btn" data-fechar>Cancelar</button><button type="submit" class="btn btn-pri">Guardar alerta</button>' });
      form.querySelector('[data-fechar]').addEventListener('click', () => drawer.fechar());
      const campo = form.elements.limite;
      campo?.addEventListener('input', () => {
        const v = Number(campo.value), ok = alertasRegras.limiteValido(v);
        form.querySelector('#al-erro').textContent = ok ? '' : `Escreva um número inteiro entre ${LIMITE_MIN} e ${LIMITE_MAX}.`;
        campo.setAttribute('aria-invalid', String(!ok));
        if (ok) { form.querySelector('#al-frase').textContent = r.frase(v); form.querySelector('#al-prev').textContent = encontra(v); }
      });
      form.addEventListener('submit', async e => {
        e.preventDefault();
        const limite = campo ? Number(campo.value) : null;
        if (campo && !alertasRegras.limiteValido(limite)) { campo.focus(); return; }
        const antes = app.prefs.alertas[id];
        app.prefs.alertas = { ...app.prefs.alertas, [id]: { ligada: form.elements.ligada.checked, ...(campo ? { limite } : {}) } };
        try { await guardarPrefs(); drawer.fechar(); aviso(`Alerta "${r.nome}" guardado.`); redesenhar(); }
        catch { app.prefs.alertas[id] = antes; form.querySelector('#al-erro')?.replaceChildren('Não foi possível guardar. Tente outra vez.'); }
      });
    }

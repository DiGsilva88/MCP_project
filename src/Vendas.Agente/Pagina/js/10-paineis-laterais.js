    /* =========================================================
       Painéis laterais: Filtros, Briefing do cliente, Pré-visualização de documento, Agendar envio e Editar alerta.
       Todos só leem; nenhum escreve na base de dados do ERP.
       ========================================================= */

    /* ---------- Filtros: valem para todos os separadores e chegam às consultas do servidor ---------- */
    const hojeIso = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; };
    function abrirFiltros() {
      const F = app.filtros, D = app.dados;
      // As opções vêm do servidor (zonas e marcas com vendas no período); as já escolhidas ficam sempre na lista
      const comEscolhidas = (opcoes, escolhidas, nome) => [...opcoes, ...escolhidas.filter(v => !opcoes.some(o => o[0] === v)).map(v => [v, nome(v)])].sort((a, b) => a[1].localeCompare(b[1], 'pt'));
      const zonas = comEscolhidas((D?.zonas || []).map(z => [z.id, z.nome]), F.zonas, nomeZona);
      const marcas = comEscolhidas((D?.marcas || []).map(m => [m.nome, nomeProprio(m.nome)]), F.marcas, nomeProprio);
      const caixas = (nome, opcoes, escolhidas) => opcoes.length ? `<div class="opcoes">${opcoes.map(([v, t]) =>
        `<label class="opcao"><input type="checkbox" name="${nome}" value="${esc(v)}"${escolhidas.includes(v) ? ' checked' : ''}> ${esc(t)}</label>`).join('')}</div>` : '<p class="muted">Sem valores no período.</p>';
      const form = drawer.abrir({ topo: 'Aplica-se a todos os separadores', form: true,
        html: `<h3 id="dr-t" tabindex="-1">Filtros</h3><div class="form">
          <div class="linha2"><label>De<input type="date" name="de" value="${esc(F.de)}" min="2000-01-01" max="${hojeIso()}" aria-describedby="fl-erro"></label>
            <label>Até<input type="date" name="ate" value="${esc(F.ate)}" min="2000-01-01" max="${hojeIso()}" aria-describedby="fl-erro"></label></div>
          <span class="erro-campo" id="fl-erro" role="alert"></span>
          <label>Comparar com<select name="comparar"><option value="ano"${F.comparar !== 'nenhum' ? ' selected' : ''}>Mesmo período do ano anterior</option><option value="nenhum"${F.comparar === 'nenhum' ? ' selected' : ''}>Sem comparação</option></select></label>
          <label>Cliente<input name="cliente" maxlength="100" autocomplete="off" value="${esc(F.cliente)}" placeholder="Código ou início do nome"></label>
          <fieldset><legend>Zona</legend>${caixas('zonas', zonas, F.zonas)}</fieldset>
          <fieldset><legend>Marca</legend>${caixas('marcas', marcas, F.marcas)}</fieldset>
          <fieldset><legend>Tipo de documento</legend>${caixas('tipos', Object.entries(TIPOS_DOC), F.tipos)}</fieldset>
        </div>`,
        pe: '<button type="button" class="btn" data-limpar>Limpar filtros</button><button type="submit" class="btn btn-pri">Aplicar filtros</button>' });
      form.querySelector('[data-limpar]').addEventListener('click', () => {
        form.elements.de.value = form.elements.ate.value = form.elements.cliente.value = '';
        form.elements.comparar.value = 'ano';
        form.querySelectorAll('input[type="checkbox"]').forEach(c => { c.checked = false; });
        form.querySelector('#fl-erro').textContent = '';
      });
      form.addEventListener('submit', e => {
        e.preventDefault();
        const d = new FormData(form), erro = filtros.validarDatas(d.get('de'), d.get('ate'), hojeIso());
        form.querySelector('#fl-erro').textContent = erro;
        ['de', 'ate'].forEach(k => form.elements[k].setAttribute('aria-invalid', String(!!erro)));
        if (erro) { form.elements.de.focus(); return; }
        const novos = filtros.limpos({ de: d.get('de'), ate: d.get('ate'), comparar: d.get('comparar'), cliente: d.get('cliente'), zonas: d.getAll('zonas'), marcas: d.getAll('marcas'), tipos: d.getAll('tipos') });
        drawer.fechar();
        aplicarFiltros(novos);
        const n = filtros.ativos(novos).length;
        aviso(n ? `${n} ${n > 1 ? 'filtros aplicados' : 'filtro aplicado'} a todos os separadores.` : 'Filtros limpos.');
      });
    }
    function aplicarFiltros(F, periodo = app.periodo) {
      Object.assign(app, { filtros: F, periodo, mes: null, credPag: 0, pag: { artigos: 0, clientes: 0, marcas: 0, zonas: 0 } });
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
      const factos = [...(c ? [['Zona', esc(nomeProprio(c.zona || '—'))], [emAno() ? `Faturação em ${ano}` : `Faturação (${descricaoPeriodo().toLowerCase()})`, fmt.eur(c.num)],
        [D.anoAnt ? 'Face a ' + D.anoAnt : 'Variação', c.novo ? 'Novo' : variacaoHtml(c.delta)], ['Última compra', esc(c.ultima ? fmt.dataCurta(c.ultima) : '—')]] : []),
        ['Artigo mais comprado', espera, 'b-artigo'], ['Faturas por pagar (com IVA)', espera, 'b-aberto'], ['Notas de crédito por abater', espera, 'b-credito']];
      const el = drawer.abrir({ topo: 'Briefing antes da visita',
        html: `<h3 id="dr-t" tabindex="-1">${esc(titulo)}</h3><p>${esc(resumoTexto)}</p>
          <ul class="factos">${factos.map(([k, v, id]) => `<li><span>${esc(k)}</span><b class="num"${id ? ` id="${id}"` : ''}>${v}</b></li>`).join('')}</ul>
          <h4 class="bloco-t">Artigos que deixou de comprar <span class="muted" id="b-n"></span></h4><div id="b-parados">${aCarregar('A procurar os artigos habituais…')}</div>`,
        pe: `<button type="button" class="btn" data-fechar>Fechar</button>${app.vendedor ? '' : `<button type="button" class="btn btn-pri" data-perguntar-briefing disabled>${ic('flow')}Perguntar ao Flow</button>`}` });
      el.querySelector('[data-fechar]').addEventListener('click', () => drawer.fechar());
      // O Flow responde com os dados deste briefing, sem o modelo (o modelo confundia a pergunta com um pedido de dados do cliente)
      let briefing = null;
      el.querySelector('[data-perguntar-briefing]')?.addEventListener('click', () => {
        if (!briefing) return;
        drawer.fechar(false);
        responderLocal(`O que devo saber antes de visitar o cliente ${titulo}?`, analiseBriefing(briefing, c, D));
      });
      el.addEventListener('click', e => { const b = e.target.closest('[data-perguntar-artigo]'); if (b) { drawer.fechar(false); perguntarFlow(`Porque é que o cliente ${titulo} deixou de comprar ${b.dataset.perguntarArtigo}?`); } });
      const aqui = () => n === pedidoBriefing && document.getElementById('b-parados');
      try {
        const r = await fetch(`/api/clientes/${encodeURIComponent(id)}/briefing`);
        if (!aqui()) return;
        if (r.status === 404) throw Object.assign(new Error('404'), { semCliente: true });
        if (!r.ok) throw new Error('HTTP ' + r.status);
        const b = await r.json();
        if (!aqui()) return;
        briefing = b;
        el.querySelector('[data-perguntar-briefing]')?.removeAttribute('disabled');
        const a = b.emAberto, m = b.maisComprado;
        document.getElementById('b-artigo').textContent = m ? nomeProprio(m.artigo) : 'Sem compras nos últimos 12 meses';
        document.getElementById('b-aberto').innerHTML = !a.documentos ? 'Nenhuma'
          : `${fmt.inteiro(a.documentos)} ${a.documentos > 1 ? 'documentos' : 'documento'}, ${fmt.eur2(a.total)}${a.vencido > 0 ? `<span class="celula-sub">${fmt.eur2(a.vencido)} vencidos</span>` : ''}`;
        // Notas de crédito por abater: à parte (não abatem às faturas nem entram no vencido); só aparecem se existirem
        const credito = document.getElementById('b-credito');
        credito.textContent = `${fmt.inteiro(a.documentosCredito)} ${a.documentosCredito > 1 ? 'documentos' : 'documento'}, ${fmt.eur2(a.creditos)}`;
        credito.closest('li').hidden = !a.documentosCredito;
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
        document.getElementById('b-credito').closest('li').hidden = true;
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

    /* ---------- Criar ou editar um alerta personalizado ----------
       Nome, o que vigiar, limite, zona, frequência e email opcional. A frase do alerta e o que encontraria com os dados de hoje
       atualizam-se enquanto se preenche. O envio por email ainda não é automático: o aviso aparece no painel. */
    function abrirCriarAlerta({ tipo = 'zona', id = null } = {}) {
      const existentes = app.prefs.alertasPers || [], atual = id ? existentes.find(a => a.id === id) : null;
      const m0 = alertasPers.modelo(atual ? atual.tipo : tipo) || MODELOS_ALERTA[0];
      const st = atual || { id: null, nome: m0.nome, tipo: m0.tipo, limite: m0.omissao, zona: '', freq: FREQ_ALERTA[1], mail: false, email: '', ligada: true, ultimo: '' };
      const modelos = MODELOS_ALERTA.filter(m => !(m.tipo === 'fatura' && app.vendedor));
      const zonas = (app.dados?.zonas || []).filter(z => !z.sem).map(z => [z.id, z.nome]);
      if (st.zona && !zonas.some(z => z[0] === st.zona)) zonas.unshift([st.zona, nomeZona(st.zona)]);
      const form = drawer.abrir({ topo: atual ? 'Editar alerta' : 'Novo alerta', form: true,
        html: `<h3 id="dr-t" tabindex="-1">${atual ? 'Editar alerta' : 'Criar alerta'}</h3><div class="form">
          <label>Nome<input name="nome" maxlength="60" autocomplete="off" value="${esc(st.nome)}" required aria-describedby="al-erro"></label>
          <label>O que vigiar<select name="tipo">${modelos.map(m => `<option value="${m.tipo}"${m.tipo === m0.tipo ? ' selected' : ''}>${esc(m.nome)}</option>`).join('')}</select></label>
          <label>Limite<span class="em-linha"><input name="limite" type="number" inputmode="numeric" step="1" value="${esc(st.limite)}" required aria-describedby="al-erro"><span class="muted" id="al-un"></span></span></label>
          <label id="al-zona">Zona<select name="zona"><option value="">Todas as zonas</option>${zonas.map(([v, t]) => `<option value="${esc(v)}"${v === st.zona ? ' selected' : ''}>${esc(t)}</option>`).join('')}</select></label>
          <label>Verificar<select name="freq">${FREQ_ALERTA.map(f => `<option${f === st.freq ? ' selected' : ''}>${esc(f)}</option>`).join('')}</select></label>
          <label class="opcao"><input type="checkbox" name="mail"${st.mail ? ' checked' : ''}> Enviar também por email</label>
          <label id="al-email">Email<input name="email" type="email" autocomplete="off" value="${esc(st.email)}" placeholder="nome@${EMAIL_EMPRESA}" aria-describedby="al-erro"></label>
          <span class="muted" id="al-email-nota" style="font-size:12.5px">O aviso aparece sempre no painel. O envio automático por email ainda não está ativo.</span>
          <span class="erro-campo" id="al-erro" role="alert"></span></div>
          <div class="frase"><span class="muted">O alerta fica assim</span><p id="al-frase"></p></div>
          <div class="frase frase--contorno"><span class="muted">Com os dados de hoje, encontraria</span><p id="al-prev" aria-live="polite"></p></div>`,
        pe: `${atual ? '<button type="button" class="btn" data-apagar>Apagar alerta</button>' : '<button type="button" class="btn" data-fechar>Cancelar</button>'}<button type="submit" class="btn btn-pri">${atual ? 'Guardar alerta' : 'Criar alerta'}</button>` });
      const $ = s => form.querySelector(s), E = form.elements;
      const ler = () => ({ id: st.id, ligada: st.ligada, ultimo: st.ultimo, nome: E.nome.value, tipo: E.tipo.value, limite: E.limite.value, zona: E.zona.value, freq: E.freq.value, mail: E.mail.checked, email: E.email.value });
      let atrasosPrevia = null, aConsultar = false, espera;
      const pintar = () => {
        const a = ler(), m = alertasPers.modelo(a.tipo), n = Number(a.limite), valido = Number.isInteger(n) && n >= m.min && n <= m.max;
        $('#al-un').textContent = m.un;
        $('#al-zona').hidden = a.tipo !== 'zona';
        $('#al-email').hidden = $('#al-email-nota').hidden = !a.mail;
        $('#al-frase').textContent = valido ? alertasPers.frase({ ...a, limite: n }, nomeZona) : `Escreva um limite inteiro entre ${fmt.inteiro(m.min)} e ${fmt.inteiro(m.max)}.`;
        if (!valido) { $('#al-prev').textContent = '—'; return; }
        if (a.tipo === 'fatura' && !atrasosPrevia) { $('#al-prev').textContent = aConsultar ? 'A consultar as faturas por pagar…' : 'Não foi possível consultar as faturas por pagar.'; return; }
        const r = alertasPers.avaliar({ ...a, limite: n, id: 'previa' }, app.dados, atrasosPrevia);
        $('#al-prev').textContent = !app.dados ? 'Os dados ainda não carregaram.' : r.length ? `${fmt.inteiro(r.length)} ${r.length > 1 ? 'resultados' : 'resultado'}: ${r.slice(0, 5).map(x => x.titulo).join('; ')}${r.length > 5 ? '…' : '.'}` : 'Nada, por agora.';
      };
      // Faturas em atraso: a pré-visualização pede ao servidor com o limite escrito (sem pedir a cada tecla)
      const consultarAtrasos = () => {
        clearTimeout(espera);
        const n = Number(E.limite.value);
        if (E.tipo.value !== 'fatura' || !Number.isInteger(n) || n < 1 || n > 3650) return;
        aConsultar = true; atrasosPrevia = null; pintar();
        espera = setTimeout(async () => {
          try { atrasosPrevia = await pedirJson('/api/atrasos?dias=' + n); } catch (e) { console.error(e); atrasosPrevia = null; }
          aConsultar = false;
          if (form.isConnected) pintar();
        }, 400);
      };
      E.tipo.addEventListener('change', () => {
        const acompanha = !E.nome.value.trim() || modelos.some(m => m.nome === E.nome.value);   // o nome ainda é o de um modelo: acompanha a troca
        const m = alertasPers.modelo(E.tipo.value);
        if (acompanha) E.nome.value = m.nome;
        E.limite.value = m.omissao;
        pintar(); consultarAtrasos();
      });
      E.limite.addEventListener('input', () => { pintar(); consultarAtrasos(); });
      [E.zona, E.mail, E.nome, E.email].forEach(c => c.addEventListener('input', pintar));
      E.freq.addEventListener('change', pintar);
      form.querySelector('[data-fechar]')?.addEventListener('click', () => drawer.fechar());

      const guardar = async nova => {
        const antes = app.prefs.alertasPers || [];
        app.prefs.alertasPers = nova;
        try { await guardarPrefs(); return true; } catch { app.prefs.alertasPers = antes; return false; }
      };
      const depois = mensagem => {
        drawer.fechar(false);
        aviso(mensagem);
        Object.assign(app, { separador: 'alertas', alertaAba: 'personalizados', resposta: null });
        redesenhar(true); desenharFlow();
        carregarAtrasos();
      };
      let confirmar = false;
      form.querySelector('[data-apagar]')?.addEventListener('click', async e => {
        if (!confirmar) { confirmar = true; e.currentTarget.textContent = 'Confirmar: apagar'; return; }
        if (await guardar(existentes.filter(a => a.id !== atual.id))) depois(`Alerta "${atual.nome}" apagado.`);
        else $('#al-erro').textContent = 'Não foi possível apagar. Tente outra vez.';
      });
      form.addEventListener('submit', async e => {
        e.preventDefault();
        const r = alertasPers.limpar(ler(), existentes);
        ['nome', 'limite', 'email'].forEach(k => E[k].removeAttribute('aria-invalid'));
        $('#al-erro').textContent = r.erro || '';
        if (r.erro) { const c = E[r.campo]; c?.setAttribute('aria-invalid', 'true'); c?.focus(); return; }
        const botao = form.querySelector('[type="submit"]');
        botao.disabled = true;
        const ok = await guardar(atual ? existentes.map(a => a.id === atual.id ? r.alerta : a) : [r.alerta, ...existentes]);
        botao.disabled = false;
        if (ok) depois(`Alerta "${r.alerta.nome}" ${atual ? 'guardado' : 'criado'}.`);
        else $('#al-erro').textContent = 'Não foi possível guardar. Tente outra vez.';
      });
      pintar(); consultarAtrasos();
      E.nome.focus();
    }

    /* =========================================================
       Flow: o chat ao lado do painel. É a única parte que passa pelo modelo (POST /perguntar).
       ========================================================= */
    const SUGESTOES = {
      resumo: ['Como correu o último mês?', 'Qual foi a faturação por zona este ano?', 'Quanto faturámos no último trimestre?'],
      alertas: ['Clientes que não compram há 90 dias?', 'Que zonas venderam menos que no ano passado?', 'Que artigos venderam menos que no ano passado?'],
      clientes: ['Que clientes compraram menos que no ano passado?', 'Quem são os 10 maiores clientes este ano?', 'Clientes que não compram há 90 dias?'],
      faturacao: ['Quanto faturámos no último trimestre?', 'Quantas notas de crédito emitimos este ano?', 'Qual foi o ticket médio em cada mês deste ano?'],
      artigos: ['Quais os artigos mais vendidos no mês passado?', 'Que artigos venderam menos que no ano passado?', 'Que marca faturou mais este ano?'],
      zonas: ['Qual foi a faturação por zona este ano?', 'Que zona cresceu mais face ao ano passado?', 'Qual foi o artigo mais vendido em cada distrito?'],
      previsoes: ['Como evoluíram as vendas mês a mês este ano?', 'Como está a faturação deste ano face ao ano passado?', 'Que meses venderam menos este ano?'],
      guardados: ['Como evoluíram as vendas mês a mês este ano?', 'Quais são os 5 melhores clientes este ano?', 'Clientes que não compram há 90 dias?']
    };
    const conversa = [];   // { id, pergunta, texto, erro, pendente, analise, segundos, fila, guardada }
    let emCurso = null;    // { item, controlo }
    const flowCorpo = document.getElementById('flow-corpo'), flowCampo = document.getElementById('flow-campo'), flowEnviar = document.getElementById('flow-enviar');

    // Pedido de resumo escrito na caixa ("resumo das vendas deste mês"): abre o Resumo, sem passar pelo modelo
    function intencaoResumo(texto) {
      const t = semAcento(texto);
      if (!/\b(resumo|relatorio|visao geral|panorama|como (estao|esta|vao|vai) a?s? ?(vendas|faturacao))\b/.test(t)) return null;
      if (/\b(hoje|por|cada|face|compar\w*|top|melhor\w*|artigos?|familias?|marcas?|zonas?|localidades?|distritos?|vendedor\w*)\b/.test(t)) return null;
      const cliente = (texto.match(/\bcliente\s+["“«]?(.+?)["”»]?\s*\??$/i) || [])[1] || null;
      if (/\bclientes?\b/.test(t) && !cliente) return null;
      const periodo = /\bano passado\b/.test(t) ? 'AnoAnterior' : /\b12 meses\b/.test(t) ? 'Ultimos12Meses' : /\bsemana\b/.test(t) ? 'Ultimos7Dias'
        : /\btrimestre\b/.test(t) ? 'TrimestreAtual' : /\bmes\b/.test(t) ? 'MesAtual' : 'AnoAtual';
      return { periodo, cliente };
    }

    // O Flow abre com a análise do último mês completo (só números do servidor)
    function analiseUltimoMes() {
      const D = app.dados, s = D && !D.erro ? D.serie : null;
      if (!s) return '';
      const i = s.meses.findLastIndex(x => x.atual != null && !x.parcial);
      if (i < 0) return '';
      const x = s.meses[i], mes = MESES[x.m - 1], dif = x.anterior != null ? x.atual - x.anterior : null;
      const quedas = alertasAtivos(D).filter(a => a.tipo === 'zonas' && a.delta <= QUEDA_FORTE);
      return `<div class="resp"><h2>${esc(mes)} de ${x.a}</h2>
        <p>${esc(mes)} fechou com <b>${fmt.eur(x.atual)}</b>${x.delta != null ? `, ${x.delta >= 0 ? 'mais' : 'menos'} ${fmt.pct(Math.abs(x.delta))} que em ${mes.toLowerCase()} de ${esc(s.anoAnt)}` : ''}. No período, a faturação vai em <b>${fmt.eur(D.kpis.faturacao.valor)}</b>.</p>
        <div class="metricas">${[[`${mes} ${x.a}`, fmt.eur(x.atual)], [s.anoAnt ? `${mes} ${s.anoAnt}` : 'Ano anterior', fmt.eur(x.anterior)],
          ['Diferença', dif == null ? '—' : `<span class="${dif >= 0 ? 'up' : 'dn'}">${dif >= 0 ? '+' : MENOS}${fmt.eur(Math.abs(dif))}</span>`], ['Clientes ativos', fmt.inteiro(D.kpis.clientes.valor)]]
          .map(([r, v]) => `<div class="m"><div class="m-r">${esc(r)}</div><div class="m-v">${v}</div></div>`).join('')}</div>
        ${quedas.length ? `<p>${quedas.length === 1 ? 'Uma zona caiu' : 'Zonas que caíram'} muito no período: ${quedas.map(a => `${esc(nomeZona(a.zona))}, menos ${fmt.pct(Math.abs(a.delta))}`).join('; ')}.</p>` : ''}</div>`;
    }

    function blocoSugestoes(lista) {
      if (app.vendedor || !lista.length) return '';
      const sep = app.resposta ? 'esta resposta' : nomeSeparador(app.separador).toLowerCase();
      return `<div class="mais"><h3>Pode perguntar sobre ${esc(sep)}</h3>${lista.slice(0, 3).map(p => `<button type="button" data-perguntar="${esc(p)}"${emCurso ? ' disabled' : ''}>${esc(p)}</button>`).join('')}</div>`;
    }

    function respostaHtml(item) {
      if (item.pendente) return `<p class="resp-espera" role="status"><span class="roda" aria-hidden="true"></span><span>${item.fila ? 'Na fila: há outra pergunta em curso. A sua começa a seguir.' : 'A consultar as vendas…'} ${item.segundos ? item.segundos + ' s' : ''}</span></p>`;
      if (item.erro) return `<div class="resp-erro" role="alert">${esc({ vazio: 'O Flow não devolveu resposta. Tente outra vez.', parado: 'Parou a resposta antes de terminar.', ligacao: 'Sem ligação ao servidor de vendas. Verifique a ligação e tente outra vez.' }[item.erro] || item.texto || 'Não foi possível obter a resposta. Tente outra vez ou reformule a pergunta.')}
        <button type="button" data-repetir-pergunta="${item.id}">Tentar outra vez</button></div>`;
      if (item.resumo) return `<div class="resp"><p>${esc(item.texto)}</p></div>`;
      const a = item.analise;
      return `<div class="resp">${a.titulo ? `<h2>${esc(a.titulo)}</h2>` : ''}
        ${a.paragrafos.slice(0, 2).map(p => `<p class="resp-texto">${esc(p)}</p>`).join('')}
        ${a.metricas.length ? `<div class="metricas">${a.metricas.map(m => `<div class="m"><div class="m-r">${esc(m.r)}</div><div class="m-v">${esc(m.v)}</div>${m.delta != null ? `<div class="k-nota">${faceA(m.delta, m.ano)}</div>` : ''}</div>`).join('')}</div>` : ''}
        ${a.avisos.map(t => `<p class="aviso-dados">${ic('info')}<span>${esc(t)}</span></p>`).join('')}
        ${a.opcoes.length ? `<div class="ferramentas" style="margin-top:12px">${a.opcoes.map(o => `<button type="button" class="chip" ${o.escrever ? `data-escrever="${esc(o.escrever)}"` : `data-perguntar="${esc(o.perguntar)}"`}>${esc(o.t)}${o.n != null ? ` <span class="muted">${o.n}</span>` : ''}</button>`).join('')}</div>` : ''}
        <div class="resp-acoes">${a.painel ? `<button type="button" class="btn" data-ver-resposta="${item.id}">${ic('abrir')}${app.resposta === item ? 'Aberta no painel' : 'Ver no painel'}</button>` : ''}
          <button type="button" class="btn" data-guardar-resposta="${item.id}" aria-pressed="${!!item.guardada}">${ic('guardados')}${item.guardada ? 'Guardada' : 'Guardar resposta'}</button>
          <button type="button" class="btn" data-copiar-resposta="${item.id}">${ic('copiar')}Copiar</button></div>
        ${item.avaliacao ? `<p class="avaliar">${esc(item.avaliacao)}</p>` : `<div class="avaliar"><span>Esta resposta está certa?</span><button type="button" class="chip" data-avaliar="${item.id}|sim">Sim</button><button type="button" class="chip" data-avaliar="${item.id}|nao" aria-expanded="${!!item.motivos}">Não</button>
          ${item.motivos ? ['Números errados', 'Não respondeu à pergunta', 'Difícil de ler'].map(m => `<button type="button" class="chip" data-avaliar="${item.id}|nao|${esc(m)}">${esc(m)}</button>`).join('') : ''}</div>`}</div>`;
    }

    function desenharFlow() {
      document.getElementById('flow-ctx').textContent = 'A ver: ' + (app.resposta ? 'Resposta do Flow' : nomeSeparador(app.separador));
      let html = '';
      if (!conversa.length) {
        html = `<div class="ola"><b>Olá${primeiroNome() ? ' ' + esc(primeiroNome()) : ''}, sou o Flow.</b>${app.vendedor ? 'O Flow não está disponível para o perfil de vendedor. Os números do painel continuam disponíveis.'
          : `Estou a ver <b class="dentro">${esc(nomeSeparador(app.separador))}</b>. Pergunte o que quiser sobre esta vista.`}</div>`;
        if (app.separador === 'resumo' && !app.vendedor) html += analiseUltimoMes();
      }
      html += conversa.map(item => `<div class="troca"><div class="q"><span class="so-leitor">Pergunta: </span>${esc(item.pergunta)}</div>${respostaHtml(item)}</div>`).join('');
      const ultima = conversa[conversa.length - 1];
      html += blocoSugestoes(ultima?.analise?.sugestoes?.length ? ultima.analise.sugestoes : SUGESTOES[app.separador] || []);
      flowCorpo.innerHTML = html;
      flowCorpo.setAttribute('aria-busy', String(!!emCurso));
      flowEnviar.classList.toggle('enviar--parar', !!emCurso);
      flowEnviar.setAttribute('aria-label', emCurso ? 'Parar a resposta' : 'Enviar pergunta');
      flowEnviar.innerHTML = ic(emCurso ? 'parar' : 'enviar');
      flowCampo.disabled = app.vendedor;
      flowEnviar.disabled = app.vendedor;
    }
    const rolarFlow = () => { flowCorpo.scrollTop = flowCorpo.scrollHeight; };

    // Os botões "Perguntar" espalhados pelo painel enviam uma pergunta já escrita
    function perguntarFlow(texto) {
      abrirFlow(true);
      perguntar(texto);
    }
    async function perguntar(texto, repetir = null) {
      texto = String(texto || '').replace(/\s+/g, ' ').trim();
      if (!texto || emCurso || app.vendedor) return;
      const pedido = intencaoResumo(texto);
      if (pedido && !repetir) {
        conversa.push({ id: Date.now(), pergunta: texto, resumo: pedido, texto: `Abri o resumo das vendas (${nomePeriodo(pedido.periodo).toLowerCase()}${pedido.cliente ? ', cliente ' + pedido.cliente : ''}) no painel.` });
        Object.assign(app, { resposta: null, separador: 'resumo' });
        aplicarFiltros({ ...app.filtros, cliente: pedido.cliente || app.filtros.cliente }, pedido.periodo);
        anunciar('Resumo aberto no painel.');
        rolarFlow();
        return conversa[conversa.length - 1];
      }
      if (repetir) conversa.splice(conversa.indexOf(repetir), 1);
      const item = { id: Date.now(), pergunta: texto, texto: '', erro: null, pendente: true, segundos: 0 };
      conversa.push(item);
      const controlo = new AbortController(), inicio = Date.now();
      emCurso = { item, controlo };
      const relogio = setInterval(() => { item.segundos = Math.round((Date.now() - inicio) / 1000); const s = flowCorpo.querySelector('.resp-espera span:last-child'); if (s) s.textContent = `${item.fila ? 'Na fila: há outra pergunta em curso. A sua começa a seguir.' : 'A consultar as vendas…'} ${item.segundos} s`; }, 1000);
      desenharFlow(); rolarFlow();
      try {
        const r = await fetch('/perguntar', { method: 'POST', body: texto, signal: controlo.signal });
        if (!r.ok) { item.texto = (await r.text()).trim(); item.erro = 'recusado'; return; }
        const leitor = r.body.getReader(), dec = new TextDecoder();
        for (;;) {
          const { done, value } = await leitor.read();
          if (done) break;
          // Avisos da fila do servidor ("\u0001fila" / "\u0001vez"): mudam a espera, não entram na resposta
          const parte = dec.decode(value, { stream: true }).replace(/\u0001(fila|vez)\n/g, (_, a) => { item.fila = a === 'fila'; return ''; });
          item.texto += parte;
        }
        item.texto += dec.decode();
        if (!item.texto.trim()) item.erro = 'vazio';
      } catch (e) {
        item.erro = e.name === 'AbortError' ? 'parado' : 'ligacao';
      } finally {
        clearInterval(relogio);
        emCurso = null;
        item.pendente = false;
        if (!item.erro) {
          try { item.analise = analisarResposta(item.texto, texto); }
          catch (e) { console.error('Falha a ler a resposta:', e); item.analise = { titulo: '', paragrafos: [item.texto.trim()], metricas: [], avisos: [], opcoes: [], painel: null, linhas: null, sugestoes: [] }; }
          // Com dados (tabela, gráfico, indicadores), a resposta abre no painel principal
          if (item.analise.painel) abrirResposta(item, false);
        }
        desenharFlow(); rolarFlow();
        anunciar(item.erro ? 'A resposta falhou.' : 'Resposta pronta.');
        if (document.activeElement === document.body || document.activeElement === flowEnviar) flowCampo.focus();
      }
      return item;
    }
    function pararPergunta() {
      if (!emCurso) return;
      emCurso.controlo.abort();
      fetch('/parar', { method: 'POST' }).catch(() => { });   // abortar só o pedido deixava o modelo a gerar
    }
    function abrirResposta(item, desenhar = true) {
      app.resposta = { item, pergunta: item.pergunta, texto: item.texto, analise: item.analise };
      if (desenhar) { desenharFlow(); }
      redesenhar(true);
    }

    async function guardarResposta(item) {
      if (item.guardada) { aviso('Esta resposta já está em Guardados.'); return; }
      const { lista, jaExistia } = guardados.adicionar(app.prefs.guardados || [], { id: 'r' + item.id, tipo: 'resposta', nome: item.analise.titulo || item.pergunta, pergunta: item.pergunta, texto: item.texto, quando: new Date().toISOString() });
      if (jaExistia) { item.guardada = true; desenharFlow(); aviso('Esta resposta já está em Guardados.'); return; }
      const antes = app.prefs.guardados;
      app.prefs.guardados = lista;
      try { await guardarPrefs(); item.guardada = true; aviso('Resposta guardada em Guardados.'); }
      catch { app.prefs.guardados = antes; aviso('Não foi possível guardar a resposta. Tente outra vez.'); }
      desenharFlow(); desenharMenu();
    }
    async function copiarResposta(item) {
      const a = item.analise;
      const texto = [item.pergunta, '', a.titulo, ...a.paragrafos, ...a.metricas.map(m => `${m.r}: ${m.v}`), ...(a.linhas ? ['', ...a.linhas.map(l => l.join('\t'))] : [])].filter(x => x != null).join('\n').trim();
      aviso(await copiarTexto(texto) ? 'Resposta copiada.' : 'Não foi possível copiar neste navegador.');
    }
    // Avaliação: só a avaliação e o motivo (a pergunta e a resposta podem ter nomes de clientes)
    async function avaliar(item, avaliacao, motivo) {
      if (avaliacao === 'nao' && !motivo) { item.motivos = true; desenharFlow(); flowCorpo.querySelector(`[data-avaliar^="${item.id}|nao|"]`)?.focus(); return; }
      try {
        const r = await fetch('/feedback', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ avaliacao, motivo: motivo || '' }) });
        if (!r.ok) throw new Error('HTTP ' + r.status);
        item.avaliacao = 'Obrigado. A avaliação foi enviada.';
      } catch { item.avaliacao = 'Não foi possível enviar a avaliação.'; }
      desenharFlow();
      anunciar(item.avaliacao);
    }

    // Abaixo de 1280px o Flow abre por cima do conteúdo
    const flowEl = document.getElementById('flow'), flowAbrir = document.getElementById('flow-abrir');
    const flowSobreposto = () => matchMedia('(max-width: 1280px)').matches;
    function abrirFlow(sim) {
      if (!flowSobreposto()) { if (sim) flowCampo.focus({ preventScroll: true }); return; }
      flowEl.classList.toggle('aberto', sim);
      flowAbrir.setAttribute('aria-expanded', String(sim));
      flowEl.toggleAttribute('aria-modal', sim);
      flowEl.setAttribute('role', sim ? 'dialog' : 'complementary');
      (sim ? flowCampo : flowAbrir).focus();
    }

    flowCorpo.addEventListener('click', e => {
      const b = e.target.closest('button');
      if (!b) return;
      const d = b.dataset, item = id => conversa.find(i => String(i.id) === String(id));
      if (d.perguntar) perguntar(d.perguntar);
      else if (d.escrever) { flowCampo.value = d.escrever; flowCampo.focus(); }
      else if (d.repetirPergunta) { const i = item(d.repetirPergunta); if (i) perguntar(i.pergunta, i); }
      else if (d.verResposta) abrirResposta(item(d.verResposta));
      else if (d.guardarResposta) guardarResposta(item(d.guardarResposta));
      else if (d.copiarResposta) copiarResposta(item(d.copiarResposta));
      else if (d.avaliar) { const [id, av, motivo] = d.avaliar.split('|'); avaliar(item(id), av, motivo); }
    });
    document.getElementById('flow-form').addEventListener('submit', e => {
      e.preventDefault();
      if (emCurso) { pararPergunta(); return; }
      const t = flowCampo.value;
      flowCampo.value = '';
      perguntar(t);
    });
    flowAbrir.addEventListener('click', () => abrirFlow(!flowEl.classList.contains('aberto')));
    document.getElementById('flow-fechar').addEventListener('click', () => abrirFlow(false));
    flowEl.addEventListener('keydown', e => { if (e.key === 'Escape' && flowEl.classList.contains('aberto') && !drawer.aberto) abrirFlow(false); });

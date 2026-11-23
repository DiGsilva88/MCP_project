    /* =========================================================
       Estrutura do painel: menu, barra de topo, navegação, eventos e arranque. Tem de ser o último ficheiro.
       ========================================================= */
    const principal = document.getElementById('principal'), totop = document.getElementById('totop');

    function desenharMenu() {
      const novos = nAlertasNovos(), nGuardados = (app.prefs.guardados || []).length;
      document.getElementById('rail-lista').innerHTML = SEPARADORES.map(([id, nome, icone]) => {
        const n = id === 'alertas' ? novos : id === 'guardados' ? nGuardados : 0;
        const rotulo = nome + (n ? id === 'alertas' ? `, ${n} ${n > 1 ? 'novos' : 'novo'}` : `, ${n}` : '');
        return `<button type="button" class="rail-item" data-ir="${id}" aria-label="${esc(rotulo)}"${!app.resposta && app.separador === id ? ' aria-current="page"' : ''}>${ic(icone, 18)}${n ? `<span class="rail-n${id === 'guardados' ? ' rail-n--escuro' : ''}" aria-hidden="true">${n}</span>` : ''}<span class="rail-dica" aria-hidden="true">${esc(nome)}</span></button>`;
      }).join('');
      const sino = document.getElementById('sino');
      sino.setAttribute('aria-label', novos ? `Alertas, ${novos} ${novos > 1 ? 'novos' : 'novo'}` : 'Alertas');
      sino.querySelector('.rail-n').textContent = novos;
      sino.querySelector('.rail-n').hidden = !novos;
      // "Dados até …": a data mais recente que o servidor devolve; o mês em curso fica dito como incompleto
      const D = app.dados, fresco = document.getElementById('fresco'), parcial = D?.serie?.meses.find(x => x.parcial);
      fresco.hidden = !D?.ate;
      if (D?.ate) fresco.lastChild.textContent = `Dados até ${fmt.dataLonga(D.ate)}${parcial ? `. ${MESES[parcial.m - 1]} ainda incompleto` : ''}`;
    }

    // Redesenha o conteúdo principal; focar: leva o foco ao título (mudar de separador)
    function redesenhar(focar = false) {
      const foco = !focar && document.activeElement && principal.contains(document.activeElement) ? seletorDe(document.activeElement) : null;
      principal.innerHTML = app.resposta ? vistaResposta() : VISTAS[app.separador]();
      document.title = 'Flow · ' + (app.resposta ? 'Resposta do Flow' : nomeSeparador(app.separador));
      desenharMenu();
      if (!conversa.length) desenharFlow();
      if (focar) { principal.scrollTop = 0; totop.classList.remove('on'); principal.querySelector('h1')?.focus({ preventScroll: true }); }
      else if (foco) principal.querySelector(foco)?.focus({ preventScroll: true });
    }
    // Para devolver o foco ao mesmo controlo depois de redesenhar (quem usa teclado não perde o lugar)
    function seletorDe(e) {
      if (e.id) return '#' + CSS.escape(e.id);
      const a = [...e.attributes].find(x => x.name.startsWith('data-'));
      return a ? `[${a.name}="${CSS.escape(a.value)}"]` : null;
    }
    function irPara(id) {
      if (!VISTAS[id]) id = 'resumo';
      Object.assign(app, { separador: id, resposta: null });
      fecharMenu();
      redesenhar(true);
      desenharFlow();
    }

    async function guardarVista() {
      if (app.separador !== 'guardados' && (!app.dados || app.dados.erro)) { aviso('Espere que os dados carreguem para guardar a vista.'); return; }
      const nome = `${nomeSeparador(app.separador)}, ${descricaoFiltros()}`;
      const { lista, jaExistia } = guardados.adicionar(app.prefs.guardados || [], { id: 'v' + Date.now(), tipo: 'vista', nome, separador: app.separador, periodo: app.periodo, filtros: { ...app.filtros }, quando: new Date().toISOString() });
      if (jaExistia) { aviso('Esta vista já está em Guardados.'); return; }
      const antes = app.prefs.guardados;
      app.prefs.guardados = lista;
      try { await guardarPrefs(); aviso(`Vista "${nome}" guardada em Guardados.`); }
      catch { app.prefs.guardados = antes; aviso('Não foi possível guardar a vista. Tente outra vez.'); }
      desenharMenu();
    }
    function abrirGuardado(g) {
      if (g.tipo === 'vista') { Object.assign(app, { separador: VISTAS[g.separador] ? g.separador : 'resumo', resposta: null }); aplicarFiltros(filtros.limpos(g.filtros || {}), g.periodo); desenharFlow(); principal.querySelector('h1')?.focus(); return; }
      let item = conversa.find(i => i.texto === g.texto && i.pergunta === g.pergunta);
      if (!item) { item = { id: Date.now(), pergunta: g.pergunta, texto: g.texto, analise: analisarResposta(g.texto, g.pergunta), guardada: true }; conversa.push(item); }
      if (item.analise.painel) abrirResposta(item); else desenharFlow();
      rolarFlow();
      abrirFlow(true);
    }
    function exportarGuardado(g, botao) {
      abrirMenuExportar(botao, async f => {
        try {
          if (g.tipo === 'resposta') { const a = analisarResposta(g.texto, g.pergunta); aviso(exportar(f, { titulo: g.nome, linhas: a.linhas || [['Resposta'], [g.texto]] })); return; }
          if (f === 'pdf') { abrirGuardado(g); aviso('Vista aberta. Use Exportar, PDF, quando os dados carregarem.'); return; }
          const antes = { periodo: app.periodo, filtros: app.filtros };
          Object.assign(app, { periodo: g.periodo, filtros: filtros.limpos(g.filtros || {}) });
          const q = new URLSearchParams(consultaAtual());
          let D;
          try { D = montarResumo(await pedirJson('/resumo?' + q)); } finally { Object.assign(app, antes); }
          if (D.erro) { aviso(D.erro); return; }
          aviso(exportar(f, { titulo: g.nome, linhas: linhasDe(g.separador, D) }));
        } catch (e) { console.error(e); aviso('Não foi possível exportar. Tente outra vez.'); }
      });
    }
    async function ligarAlerta(id) {
      // Um alerta personalizado: liga ou desliga na lista de cada utilizador
      const meus = app.prefs.alertasPers || [], proprio = meus.find(a => a.id === id);
      if (proprio) {
        app.prefs.alertasPers = meus.map(a => a.id === id ? { ...a, ligada: !a.ligada } : a);
        try { await guardarPrefs(); aviso(`Alerta "${proprio.nome}" ${proprio.ligada ? 'desativado' : 'ativado'}.`); }
        catch { app.prefs.alertasPers = meus; aviso('Não foi possível guardar. Tente outra vez.'); }
        redesenhar();
        principal.querySelector(`[data-ligar-alerta="${id}"]`)?.focus();
        carregarAtrasos();
        return;
      }
      const r = alertasRegras.regra(app.prefs.alertas, id), antes = app.prefs.alertas[id];
      app.prefs.alertas = { ...app.prefs.alertas, [id]: { ...(antes || {}), ligada: !r.ligada } };
      try { await guardarPrefs(); aviso(`Alerta "${r.nome}" ${r.ligada ? 'desativado' : 'ativado'}.`); }
      catch { app.prefs.alertas[id] = antes; aviso('Não foi possível guardar. Tente outra vez.'); }
      redesenhar();
      principal.querySelector(`[data-ligar-alerta="${id}"]`)?.focus();
    }
    async function removerGuardado(x) {
      const antes = app.prefs.guardados;
      app.prefs.guardados = guardados.remover(antes, x.id);
      try { await guardarPrefs(); aviso(`"${x.nome}" removido de Guardados.`); }
      catch { app.prefs.guardados = antes; aviso('Não foi possível remover. Tente outra vez.'); }
      redesenhar();
      principal.querySelector('[data-filtro-guardados][aria-pressed="true"]')?.focus();
    }
    // Filtros e período por utilizador: voltam na próxima sessão
    function guardarFiltrosPrefs() {
      app.prefs.filtros = { periodo: app.periodo, ...app.filtros };
      guardarPrefs().catch(e => console.error('Falha a guardar os filtros:', e));
    }

    /* ---------- Cliques no conteúdo principal ---------- */
    principal.addEventListener('click', e => {
      const b = e.target.closest('button, [data-mes], tr.clicavel');
      if (!b) return;
      const d = b.dataset, g = id => (app.prefs.guardados || []).find(x => x.id === id);
      // Mês, Trimestre ou Ano: substitui as datas De/Até que houvesse nos filtros
      if (d.periodo) aplicarFiltros({ ...app.filtros, de: '', ate: '' }, d.periodo);
      else if ('filtros' in d) abrirFiltros();
      else if (d.tirarFiltro) aplicarFiltros(filtros.remover(app.filtros, d.tirarFiltro, d.valor));
      else if ('limparFiltros' in d) { aplicarFiltros(filtros.vazios()); aviso('Filtros limpos.'); }
      else if ('exportar' in d) abrirMenuExportar(b, f => { const x = exportacaoAtual(); aviso(x ? exportar(f, x) : 'Ainda não há dados para exportar.'); });
      else if ('agendar' in d) abrirAgendar();
      else if ('guardar' in d) app.resposta ? guardarResposta(app.resposta.item) : guardarVista();
      else if (d.ir) irPara(d.ir);
      else if (d.perguntar) perguntarFlow(d.perguntar);
      else if (d.cliente) abrirBriefing(d.cliente, d.nome);
      else if (d.doc) abrirDocumento(d.doc);
      else if (d.mes) { app.mes = +d.mes; carregarMes(); redesenhar(); }
      else if ('repetirMes' in d) { carregarMes(); redesenhar(); }
      else if (d.abaAlertas) { app.alertaAba = d.abaAlertas; redesenhar(); principal.querySelector(`[data-aba-alertas="${d.abaAlertas}"]`)?.focus(); }
      else if (d.filtroAlertas) { app.alertaFiltro = d.filtroAlertas; redesenhar(); }
      else if (d.novoAlerta) abrirCriarAlerta({ tipo: d.novoAlerta });
      else if (d.editarAlerta) REGRAS_ALERTA.some(r => r[0] === d.editarAlerta) ? abrirEditarAlerta(d.editarAlerta) : abrirCriarAlerta({ id: d.editarAlerta });
      else if (d.ligarAlerta) ligarAlerta(d.ligarAlerta);
      else if (d.verTodos) { app.ver[d.verTodos] = !app.ver[d.verTodos]; app.pag[d.verTodos] = 0; redesenhar(); principal.querySelector(`[data-ver-todos="${d.verTodos}"]`)?.focus({ preventScroll: true }); }
      else if (d.pag) { const [k, passo] = d.pag.split('|'); app.pag[k] = Math.max(0, (app.pag[k] || 0) + +passo); redesenhar(); }
      else if (d.estadoCli) { app.cliEstado = d.estadoCli; app.pag.clientes = 0; principal.querySelectorAll('[data-estado-cli]').forEach(x => x.setAttribute('aria-pressed', String(x === b))); document.getElementById('cli-tabela').innerHTML = tabelaClientes(app.dados); }
      else if (d.metricaArt) { app.artMetrica = d.metricaArt; app.pag.artigos = 0; if (d.metricaArt === 'unidades') carregarArtigosQtd(); redesenhar(); }
      else if (d.filtrarZona) { aplicarFiltros({ ...app.filtros, zonas: [d.filtrarZona] }); aviso(`Painel filtrado pela zona ${nomeZona(d.filtrarZona)}.`); }
      else if (d.cenario) { app.cenario = d.cenario; redesenhar(); }
      else if (d.filtroGuardados) { app.guardFiltro = d.filtroGuardados; redesenhar(); }
      else if (d.abrirGuardado) abrirGuardado(g(d.abrirGuardado));
      else if (d.exportarGuardado) exportarGuardado(g(d.exportarGuardado), b);
      else if (d.removerGuardado) removerGuardado(g(d.removerGuardado));
      else if ('fecharResposta' in d) { app.resposta = null; redesenhar(true); desenharFlow(); }
      else if ('repetir' in d) carregarResumo();
      else if (d.escolherCliente) aplicarFiltros({ ...app.filtros, cliente: d.escolherCliente });
    });
    principal.addEventListener('keydown', e => {
      const linha = e.target.closest('tr.clicavel');
      if (linha && e.target === linha && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); linha.click(); return; }
      // Gráfico: as setas escolhem o mês anterior ou o seguinte
      if (e.target.matches('[data-mes]') && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); e.target.dispatchEvent(new MouseEvent('click', { bubbles: true })); return; }
      if (e.target.closest('[data-grafico]') && (e.key === 'ArrowLeft' || e.key === 'ArrowRight') && app.dados?.serie) {
        e.preventDefault();
        app.mes = mesVizinho(app.dados.serie.meses, app.mes, e.key === 'ArrowRight' ? 1 : -1);
        carregarMes();
        redesenhar();
        principal.querySelector('[data-grafico]').focus({ preventScroll: true });
      }
      // Abas: as setas mudam de aba
      if (e.target.matches('[role="tab"]') && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) {
        const abas = [...principal.querySelectorAll('[role="tab"]')], i = abas.indexOf(e.target);
        abas[(i + (e.key === 'ArrowRight' ? 1 : abas.length - 1)) % abas.length].click();
      }
    });
    principal.addEventListener('input', e => {
      if (e.target.id === 'cli-q') { app.cliQ = e.target.value; app.pag.clientes = 0; document.getElementById('cli-tabela').innerHTML = tabelaClientes(app.dados); }
    });
    principal.addEventListener('scroll', () => totop.classList.toggle('on', principal.scrollTop > 80));
    totop.addEventListener('click', () => {
      principal.scrollTo({ top: 0, behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
      principal.querySelector('h1')?.focus({ preventScroll: true });
    });

    /* ---------- Menu e barra de topo ---------- */
    document.getElementById('rail-lista').addEventListener('click', e => { const b = e.target.closest('[data-ir]'); if (b) irPara(b.dataset.ir); });
    document.getElementById('sino').addEventListener('click', () => { app.alertaAba = 'recebidos'; irPara('alertas'); });
    // Pesquisa de clientes: Enter abre Clientes já filtrado (nos 100 maiores; senão, o servidor procura em todos)
    document.getElementById('pesquisa').addEventListener('submit', e => {
      e.preventDefault();
      const campo = document.getElementById('pesquisa-campo'), t = campo.value.replace(/\s+/g, ' ').trim().slice(0, 100);
      if (!t) return;
      campo.value = '';
      Object.assign(app, { cliQ: t, cliEstado: 'todos' });
      if (app.dados?.carteira && clientesFiltrados(app.dados).length) irPara('clientes');
      else { Object.assign(app, { separador: 'clientes', resposta: null, cliQ: '' }); aplicarFiltros({ ...app.filtros, cliente: t }); desenharFlow(); }
    });
    // Sair: volta ao ecrã de entrada (e esquece que já se entrou). O vendedor não tem entrada, por isso fecha-se a janela.
    document.getElementById('sair').addEventListener('click', () => {
      drawer.fechar(false);
      fecharMenu();
      abrirFlow(false);
      try { sessionStorage.removeItem(LUGAR); } catch { }
      if (app.vendedor) { window.close(); aviso('Pode fechar esta janela.'); return; }
      Object.assign(app, { separador: 'resumo', resposta: null });
      entrada.hidden = false;
      document.querySelector('.app').inert = true;
      entCampo.value = '';
      estadoEntrada('');
      entCampo.focus({ preventScroll: true });
    });
    window.addEventListener('hashchange', () => { const id = location.hash.slice(1); if (DOC_ID.test(id)) abrirDocumento(id); });

    /* ---------- Arranque ---------- */
    (async () => {
      const iniciais = nomeUtilizador.split(/\s+/).filter(Boolean);
      document.getElementById('avatar').textContent = iniciais.length ? (iniciais[0][0] + (iniciais.length > 1 ? iniciais.at(-1)[0] : '')).toUpperCase() : '';
      document.getElementById('avatar').title = nomeUtilizador;
      // A entrada só se salta ao recarregar o painel, num "#clientes" (atalho ou ligação direta) ou para o vendedor (que não pode perguntar).
      // Abrir o Flow de novo, mesmo na mesma janela, mostra a entrada: a marca de "já entrei" só vale para o reload.
      let marca = false, navegacao = '';
      try { marca = sessionStorage.getItem(LUGAR) === '1'; } catch { }
      try { navegacao = performance.getEntriesByType('navigation')[0]?.type || ''; } catch { }
      const ancora = location.hash.slice(1);
      if (VISTAS[ancora]) app.separador = ancora;
      if (arranque.saltarEntrada({ vendedor: app.vendedor, ancoraValida: !!VISTAS[ancora] || DOC_ID.test(ancora), navegacao, marca })) entrada.hidden = true;
      else { try { sessionStorage.removeItem(LUGAR); } catch { } document.querySelector('.app').inert = true; entCampo.focus({ preventScroll: true }); }
      desenharFlow();
      await carregarPrefs();
      const f = app.prefs.filtros;
      if (f) Object.assign(app, { periodo: PERIODOS.some(p => p[0] === f.periodo) ? f.periodo : 'AnoAtual', filtros: filtros.limpos(f) });
      carregarResumo();
      if (DOC_ID.test(location.hash.slice(1))) abrirDocumento(location.hash.slice(1));
    })();

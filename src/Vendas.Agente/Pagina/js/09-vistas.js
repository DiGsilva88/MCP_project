    /* =========================================================
       Separadores do painel. Cada um devolve o HTML do conteúdo principal; os cliques tratam-se
       por delegação em 13-app.js (data-*). Todos os números vêm de app.dados (servidor).
       ========================================================= */
    const SEPARADORES = [
      ['resumo', 'Resumo das vendas', 'resumo'], ['alertas', 'Alertas', 'alertas'], ['clientes', 'Clientes', 'clientes'], ['faturacao', 'Faturação', 'faturacao'],
      ['artigos', 'Artigos', 'artigos'], ['zonas', 'Zonas', 'zonas'], ['previsoes', 'Previsões', 'previsoes'], ['guardados', 'Guardados', 'guardados']
    ];
    const nomeSeparador = id => (SEPARADORES.find(s => s[0] === id) || [])[1] || '';
    const anoAtual = () => new Date().getFullYear();
    const nomePeriodo = p => p === 'AnoAtual' ? `Ano ${anoAtual()}` : p === 'AnoAnterior' ? `Ano ${anoAtual() - 1}` : (PERIODOS.find(x => x[0] === p) || [])[1] || '';
    const ola = () => { const h = new Date().getHours(); return h < 12 ? 'Bom dia' : h < 20 ? 'Boa tarde' : 'Boa noite'; };
    const primeiroNome = () => nomeUtilizador.split(/\s+/)[0] || '';
    const limiteCliente = () => alertasRegras.regra(app.prefs.alertas, 'cliente').limite;
    // Com datas De/Até nos filtros, as datas ganham ao período (Mês, Trimestre, Ano)
    const comDatas = () => !!(app.filtros.de && app.filtros.ate);
    const descricaoPeriodo = () => comDatas() ? `${fmt.dataLonga(app.filtros.de, true)} a ${fmt.dataLonga(app.filtros.ate, true)}` : nomePeriodo(app.periodo);
    const emAno = () => !comDatas() && app.periodo === 'AnoAtual' ? ' em ' + anoAtual() : '';
    const NOMES_FILTROS = { zona: nomeZona, marca: nomeProprio, tipo: t => TIPOS_DOC[t] || t, data: d => fmt.dataLonga(d, true) };
    const descricaoFiltros = () => [descricaoPeriodo(), ...filtros.ativos(app.filtros, NOMES_FILTROS).filter(a => a.chave !== 'datas').map(a => a.rotulo)].join(', ');

    // Cabeçalho comum: título, subtítulo, "Valores apresentados sem IVA." e as ações por esta ordem:
    // Guardar vista, período, Filtros, Exportar (e "Agendar envio" só no Resumo)
    function cabecalho(titulo, sub, o = {}) {
      const { iva = true, periodo = true, guardar = 'Guardar vista', comFiltros = true, exportar = 'Exportar', agendar = false, extra = '' } = o;
      const nF = filtros.ativos(app.filtros).length;
      const per = periodo ? seg(PERIODOS.slice(0, 3).map(([v, t]) => [v, v === 'AnoAtual' ? `Ano ${anoAtual()}` : t]), comDatas() ? '' : app.periodo, 'data-periodo', 'Período') : '';
      const acoes = [
        guardar ? `<button type="button" class="btn" data-guardar>${ic('guardados')}${guardar}</button>` : '', per,
        comFiltros ? `<button type="button" class="btn${nF ? ' btn-ativo' : ''}" data-filtros aria-haspopup="dialog">${ic('filtro')}Filtros${nF ? ` <span class="btn-n">${nF}</span>` : ''}</button>` : '',
        exportar ? `<button type="button" class="btn" data-exportar aria-haspopup="menu" aria-expanded="false">${ic('exportar')}${exportar}</button>` : '',
        agendar && !app.vendedor ? `<button type="button" class="btn" data-agendar aria-haspopup="dialog">${ic('calendario')}Agendar envio</button>` : '', extra
      ].join('');
      const outroPeriodo = periodo && (comDatas() || !PERIODOS.slice(0, 3).some(p => p[0] === app.periodo)) ? ` Período: ${descricaoPeriodo().toLowerCase()}.` : '';
      return `<div class="cabeca"><div><h1 tabindex="-1">${esc(titulo)}</h1>${sub || outroPeriodo ? `<p class="cabeca-sub">${esc(sub)}${esc(outroPeriodo)}</p>` : ''}${iva ? '<p class="cabeca-iva">Valores apresentados sem IVA.</p>' : ''}</div>
        <div class="acoes">${acoes}</div></div>${comFiltros ? barraFiltros() : ''}`;
    }
    function barraFiltros() {
      const a = filtros.ativos(app.filtros, NOMES_FILTROS);
      if (!a.length) return '';
      return `<div class="fbar"><span class="muted">Filtros:</span>${a.map(f => `<span class="etiqueta">${esc(f.rotulo)}<button type="button" data-tirar-filtro="${f.chave}" data-valor="${esc(f.valor)}" aria-label="Remover filtro ${esc(f.rotulo)}">${ic('fechar', 11)}</button></span>`).join('')}<button type="button" class="ligacao" data-limpar-filtros>Limpar tudo</button></div>`;
    }
    // O mesmo cabeçalho enquanto carrega ou quando falha: os botões continuam a funcionar
    function semDados(titulo, sub, o) {
      if (app.aCarregar || (!app.dados && !app.erro)) return cabecalho(titulo, sub, o) + aCarregar('A carregar os dados das vendas…');
      if (app.erro) return cabecalho(titulo, sub, o) + comErro();
      if (app.dados.erro) return cabecalho(titulo, sub, o) + `<div class="estado-erro" role="alert"><span>${esc(app.dados.erro)}</span></div>` +
        (app.dados.opcoes?.length ? chips(app.dados.opcoes.map(([id, nome]) => [id, `${nomeProprio(nome)} (${id})`]), '', 'data-escolher-cliente', 'Clientes encontrados') : '');
      return null;
    }
    // Uma lista longa: os primeiros (topo) com "Ver todos"; aberta (ou paginada=true), páginas de 15 com Anterior e Seguinte.
    function fatiaLista(chave, lista, topo = LISTA.topo, paginada = false) {
      const total = lista.length, aberta = paginada || !!app.ver[chave];
      if (!aberta) return { itens: lista.slice(0, topo), rodape: total > topo ? `<div class="paginas"><span>${fmt.inteiro(topo)} de ${fmt.inteiro(total)}</span><button type="button" class="btn" data-ver-todos="${chave}">Ver todos</button></div>` : '' };
      const p = listas.paginar(lista, app.pag[chave] || 0);
      app.pag[chave] = p.pag;
      return { itens: p.itens, rodape: total > LISTA.porPagina || !paginada ? `<nav class="paginas" aria-label="Páginas"><span>${fmt.inteiro(p.de)}–${fmt.inteiro(p.ate)} de ${fmt.inteiro(total)}</span>
        <button type="button" class="btn" data-pag="${chave}|-1"${p.pag === 0 ? ' disabled' : ''}>Anterior</button><button type="button" class="btn" data-pag="${chave}|1"${p.pag >= p.ultima ? ' disabled' : ''}>Seguinte</button>
        ${paginada ? '' : `<button type="button" class="btn" data-ver-todos="${chave}">Ver só os primeiros ${fmt.inteiro(topo)}</button>`}</nav>` : '' };
    }
    const kpiNota = (k, D) => faceA(k.variacao, D.anoAnt);
    const melhor = (itens, f = i => i.delta) => itens.filter(i => f(i) != null).sort((a, b) => f(b) - f(a));

    /* ---------- Resumo das vendas ---------- */
    // Cartão "Mês selecionado": os 4 indicadores do mês, do servidor (/api/mes), com os filtros do painel
    function cartaoMes(s, i) {
      const x = s.meses[i];
      if (!x) return '';
      const nome = MESES[x.m - 1], reg = indicadoresMes.get(chaveMes(x.a, x.m));
      const espera = () => indicadores([['Faturação'], ['Documentos'], ['Valor médio'], ['Clientes ativos']].map(([rotulo]) => ({ valor: '0', rotulo, nota: 'x' }))).replace(/class="k"/g, 'class="k k-espera"').replace('class="kpis"', 'class="mes-grelha" aria-busy="true"');
      const corpo = !reg || reg.estado === 'a-carregar' ? espera()
        : reg.estado === 'erro' ? '<div class="mes-erro" role="alert"><p>Não foi possível carregar os dados deste mês.</p><button type="button" class="btn" data-repetir-mes>Tentar outra vez</button></div>'
        : indicadores(blocosMes(reg.dados, x.parcial || reg.dados.incompleto)).replace('class="kpis"', 'class="mes-grelha"');
      return `<section class="painel mes-cartao" aria-labelledby="mes-t"><span class="mes-rotulo">Mês selecionado</span><h2 id="mes-t" aria-live="polite">${esc(nome)} ${x.a}</h2>${corpo}
        <p class="mes-ajuda">Clique noutro mês do gráfico para ver os indicadores desse mês.</p>
        <button type="button" class="btn btn-pri" data-perguntar="Como correu ${nome.toLowerCase()} de ${x.a}?">${ic('flow')}Perguntar ao Flow sobre ${nome.toLowerCase()}</button></section>`;
    }
    function vistaResumo() {
      const sub = `${ola()}${primeiroNome() ? ', ' + primeiroNome() : ''}.`;
      const espera = semDados('Resumo das vendas', sub, { agendar: true });
      if (espera) return espera;
      const D = app.dados, K = D.kpis, s = D.serie;
      let html = cabecalho('Resumo das vendas', sub, { agendar: true }) + indicadores([
        { valor: fmt.eur(K.faturacao.valor), rotulo: 'Faturação total', nota: kpiNota(K.faturacao, D) },
        { valor: fmt.inteiro(K.documentos.valor), rotulo: 'Documentos', nota: kpiNota(K.documentos, D) },
        { valor: fmt.eur2(K.medio.valor), rotulo: 'Valor médio', nota: kpiNota(K.medio, D) },
        { valor: fmt.inteiro(K.clientes.valor), rotulo: 'Clientes ativos', nota: kpiNota(K.clientes, D) }]);
      if (s && s.meses.length > 1) {
        if (app.mes == null || !s.meses[app.mes] || s.meses[app.mes].atual == null) app.mes = mesPorOmissao(s.meses);
        const parcial = s.meses.find(x => x.parcial);
        html += '<div class="mes-linha">' + painel({ titulo: 'Vendas por mês', extra: `<div class="legenda"><span><i></i>${s.anoAtual}</span>${s.anoAnt ? `<span><i class="ant"></i>${esc(s.anoAnt)}</span>` : ''}${parcial ? `<span><svg width="10" height="10" aria-hidden="true"><circle cx="5" cy="5" r="3.5" fill="none" stroke="currentColor" stroke-width="1.5"/></svg>${MESES[parcial.m - 1].slice(0, 3)}., parcial</span>` : ''}</div>`,
          corpo: graficoMeses({ meses: s.meses, sel: app.mes, ultimaData: D.ate, W: 640, H: 270 }) }) + cartaoMes(s, app.mes) + '</div>';
      }
      const top = (D.clientes || []).slice(0, 4), alertas = alertasAtivos(D);
      html += `<div class="dois">${painel({ titulo: 'Principais clientes', extra: '<button type="button" class="ligacao" data-ir="clientes">Ver todos</button>',
        corpo: tabela({ colunas: [{ t: 'Cliente' }, { t: 'Faturação', dir: true }, { t: D.anoAnt ? 'Face a ' + D.anoAnt : 'Variação', dir: true }],
          linhas: top.map(c => ({ attrs: `data-cliente="${esc(c.codigo || c.nome)}" data-nome="${esc(c.nome)}"`, rotulo: `Abrir o briefing de ${nomeProprio(c.nome)}`,
            c: [esc(nomeProprio(c.nome)), fmt.eur(c.num), variacaoHtml(c.delta)] })) }) })}
        ${painel({ titulo: `Precisa de atenção <span class="muted">${alertas.length}</span>`, extra: '<button type="button" class="ligacao" data-ir="alertas">Ver alertas</button>',
          corpo: alertas.length ? listaAlertas(alertas.slice(0, 3), true) : '<p class="vazio">Nada fora do habitual neste período.</p>' })}</div>`;
      return html;
    }

    /* ---------- Alertas ---------- */
    function listaAlertas(lista, curta = false) {
      return `<ul class="alertas">${lista.map(a => `<li><span class="alerta-ic">${ic(a.icone, 17)}</span><span class="alerta-t">${esc(a.titulo)}</span><span class="alerta-d">${esc(a.texto)}</span>
        <span class="alerta-acoes">${!curta && a.cliente ? `<button type="button" class="ligacao" data-cliente="${esc(a.cliente)}" data-nome="${esc(a.nome || '')}">Ver briefing</button>` : ''}${a.pergunta ? `<button type="button" class="ligacao" data-perguntar="${esc(a.pergunta)}">${curta ? 'Perguntar' : 'Perguntar ao Flow'}</button>` : ''}</span></li>`).join('')}</ul>`;
    }
    const TIPOS_ALERTA = [['todos', 'Todos'], ['zonas', 'Zonas'], ['clientes', 'Clientes'], ['artigos', 'Artigos']];
    function vistaAlertas() {
      const D = app.dados, todos = alertasAtivos(D), regras = REGRAS_ALERTA.map(r => alertasRegras.regra(app.prefs.alertas, r[0]));
      const sub = app.alertaAba === 'recebidos' ? `O que mudou face ao mesmo período${D?.anoAnt ? ' de ' + D.anoAnt : ' do ano anterior'}.` : 'Escolha o que o Flow deve vigiar por si. Os alertas só leem os dados, não alteram nada.';
      const meus = app.prefs.alertasPers || [];
      const abas = `<div class="abas" role="tablist">${[['recebidos', 'Alertas recebidos', todos.length], ['personalizados', 'Alertas personalizados', regras.filter(r => r.ligada).length + meus.filter(a => a.ligada).length + ' ativos']]
        .map(([k, t, n]) => `<button type="button" class="aba" role="tab" id="aba-${k}" aria-controls="aba-painel" data-aba-alertas="${k}" aria-selected="${app.alertaAba === k}">${t} <span class="muted">${n}</span></button>`).join('')}</div>`;
      const pers = app.alertaAba === 'personalizados';
      const o = { periodo: !pers, comFiltros: !pers, iva: false, extra: pers ? `<button type="button" class="btn btn-pri" data-novo-alerta="zona" aria-haspopup="dialog">${ic('mais')}Criar alerta</button>` : '' };
      if (pers) {
        // O vendedor não vê as faturas em atraso: não têm o vendedor do cliente, veria as dos clientes dos outros
        const modelos = MODELOS_ALERTA.filter(m => !(m.tipo === 'fatura' && app.vendedor));
        const proprios = meus.map(a => ({ desligada: !a.ligada, c: [
          `<button type="button" class="ligacao" data-editar-alerta="${esc(a.id)}">${esc(a.nome)}</button>${a.mail ? ' <span class="tag">Email</span>' : ''}`,
          `<span class="muted">${esc(alertasPers.frase(a, nomeZona))}</span>`, esc(a.freq), a.ultimo ? esc(fmt.dataLonga(a.ultimo, true)) : 'Ainda não avisou',
          interruptor(a.ligada, 'Ativar ' + a.nome, `data-ligar-alerta="${esc(a.id)}"`)] }));
        const predefinidos = regras.map(r => ({ desligada: !r.ligada, c: [
          `<button type="button" class="ligacao" data-editar-alerta="${r.id}">${esc(r.nome)}</button>`, `<span class="muted">${esc(r.frase(r.limite))}</span>`, 'Ao abrir o painel', '—',
          interruptor(r.ligada, 'Ativar ' + r.nome, `data-ligar-alerta="${r.id}"`)] }));
        return cabecalho('Alertas', sub, o) + abas + `<div id="aba-painel" role="tabpanel" aria-labelledby="aba-personalizados">
          ${painel({ titulo: 'Começar a partir de um modelo', corpo: `<div class="modelos">${modelos.map(m =>
            `<button type="button" class="modelo" data-novo-alerta="${m.tipo}" aria-haspopup="dialog"><span class="modelo-n">${esc(m.nome)}</span><span class="modelo-d">${esc(m.frase(m.omissao, ''))}</span><span class="modelo-u">Usar modelo</span></button>`).join('')}</div>` })}
          ${painel({ titulo: 'Os meus alertas', extra: '<span class="painel-nota">Avaliados com os dados do painel (período e filtros ativos) sempre que o abre.</span>',
            corpo: tabela({ colunas: [{ t: 'Alerta' }, { t: 'Condição' }, { t: 'Frequência' }, { t: 'Último aviso' }, { t: 'Ativo', dir: true }], linhas: [...proprios, ...predefinidos] }) })}</div>`;
      }
      const espera = semDados('Alertas', sub, o);
      if (espera) return cabecalho('Alertas', sub, o) + abas + espera.slice(cabecalho('Alertas', sub, o).length);
      marcarAlertasVistos();
      const contar = t => t === 'todos' ? todos.length : todos.filter(a => a.tipo === t).length;
      const lista = todos.filter(a => app.alertaFiltro === 'todos' || a.tipo === app.alertaFiltro);
      return cabecalho('Alertas', sub, o) + abas + `<div id="aba-painel" role="tabpanel" aria-labelledby="aba-recebidos">${chips(TIPOS_ALERTA.map(([k, t]) => [k, t, contar(k)]), app.alertaFiltro, 'data-filtro-alertas', 'Tipo de alerta')}
        ${painel({ corpo: lista.length ? listaAlertas(lista) : '<p class="vazio">Sem alertas deste tipo.</p>' })}</div>`;
    }

    /* ---------- Clientes ---------- */
    function estadoCliente(c) {
      if (c.estado === 'Perdido') return 'sem';
      if (c.delta != null && c.delta <= -limiteCliente()) return 'desvio';
      return c.estado === 'Em risco' ? 'risco' : 'ativo';
    }
    const TAG_ESTADO = { ativo: '<span class="tag tag-azul">Ativo</span>', desvio: '<span class="tag tag-preta">Com desvio</span>', risco: '<span class="tag">Em risco</span>', sem: '<span class="tag">Sem compras</span>' };
    const clientesFiltrados = D => (D.carteira || []).filter(c => {
      const q = semAcento(app.cliQ.trim());
      return (app.cliEstado === 'todos' || estadoCliente(c) === app.cliEstado) && (!q || semAcento(c.nome).includes(q) || semAcento(c.zona || '').includes(q) || String(c.codigo).includes(q));
    });
    function vistaClientes() {
      const sub = `Faturação por cliente${emAno()}${app.dados?.anoAnt ? ', face a ' + app.dados.anoAnt : ''}. Clique num cliente para ver o briefing antes da visita.`;
      const espera = semDados('Clientes', sub);
      if (espera) return espera;
      const D = app.dados, K = D.kpis, cart = D.carteira || [];
      const nDesvio = cart.filter(c => estadoCliente(c) === 'desvio').length;
      return cabecalho('Clientes', sub) + indicadores([
        { valor: fmt.inteiro(K.clientes.valor), rotulo: 'Clientes ativos', nota: kpiNota(K.clientes, D) },
        { valor: fmt.inteiro(D.parados), rotulo: 'Clientes sem compras', nota: 'Com histórico, sem compras há 90 dias' },
        { valor: fmt.eur(K.faturacao.valor != null && K.clientes.valor ? K.faturacao.valor / K.clientes.valor : null), rotulo: 'Faturação média por cliente', nota: 'Faturação total a dividir por clientes ativos' },
        { valor: fmt.inteiro(nDesvio), rotulo: 'Clientes com desvio', nota: `Compram mais de ${limiteCliente()}% abaixo de ${esc(D.anoAnt || 'o ano anterior')}` }]) +
        `<div class="ferramentas"><label class="campo-procurar">${ic('procurar')}<input id="cli-q" value="${esc(app.cliQ)}" placeholder="Procurar cliente ou zona" aria-label="Procurar cliente ou zona" autocomplete="off"></label>
          <div class="ferramentas" style="margin:0" role="group" aria-label="Estado">${[['todos', 'Todos'], ['ativo', 'Ativos'], ['desvio', 'Com desvio'], ['sem', 'Sem compras']]
            .map(([k, t]) => `<button type="button" class="chip" data-estado-cli="${k}" aria-pressed="${app.cliEstado === k}">${t}</button>`).join('')}</div></div>` +
        painel({ corpo: `<div id="cli-tabela">${tabelaClientes(D)}</div>` });
    }
    function tabelaClientes(D) {
      const { itens: lista, rodape } = fatiaLista('clientes', clientesFiltrados(D), LISTA.porPagina, true);
      return tabela({ legenda: 'Clientes', vazio: app.cliQ.trim() ? `Nenhum cliente encontrado para "${app.cliQ.trim()}".` : 'Nenhum resultado com os filtros atuais.',
        colunas: [{ t: 'Cliente' }, { t: 'Zona' }, { t: 'Faturação', dir: true }, { t: D.anoAnt ? 'Face a ' + D.anoAnt : 'Variação', dir: true }, { t: 'Última compra' }, { t: 'Estado' }],
        linhas: lista.map(c => ({ attrs: `data-cliente="${esc(c.codigo || c.nome)}" data-nome="${esc(c.nome)}"`, rotulo: `Abrir o briefing de ${nomeProprio(c.nome)}`,
          c: [esc(nomeProprio(c.nome)), esc(nomeProprio(c.zona || '—')), fmt.eur(c.num), c.novo ? `<span class="muted">Novo em ${anoAtual()}</span>` : variacaoHtml(c.delta),
            esc(c.ultima ? fmt.dataCurta(c.ultima) : '—'), TAG_ESTADO[estadoCliente(c)]] })) }) + rodape +
        (D.carteira && D.carteira.length >= 100 ? '<p class="nota-pe" style="padding:0 20px 14px">Os 100 clientes com mais faturação no período. Use a pesquisa no topo para outro cliente.</p>' : '');
    }

    /* ---------- Faturação ---------- */
    function vistaFaturacao() {
      const sub = `Documentos emitidos${emAno()}. Clique num documento para ver os seus artigos.`;
      const espera = semDados('Faturação', sub);
      if (espera) return espera;
      const D = app.dados, K = D.kpis, f = D.faturacao, ultimo = D.ultimosDocs?.[0];
      let html = cabecalho('Faturação', sub) + indicadores([
        { valor: fmt.eur(K.faturacao.valor), rotulo: 'Faturação total', nota: kpiNota(K.faturacao, D) },
        { valor: fmt.inteiro(K.documentos.valor), rotulo: 'Documentos', nota: kpiNota(K.documentos, D) },
        { valor: fmt.eur2(K.medio.valor), rotulo: 'Valor médio', nota: kpiNota(K.medio, D) },
        { valor: fmt.inteiro(f?.docCreditos), rotulo: 'Notas de crédito', nota: f && K.documentos.valor ? `${fmt.pct(f.docCreditos / (f.documentos + f.docCreditos) * 100)} dos documentos` : '' }]);
      if (D.tiposDoc) html += painel({ titulo: 'Por tipo de documento', corpo: tabela({
        colunas: [{ t: 'Tipo' }, { t: 'Documentos', dir: true }, { t: 'Faturação', dir: true }, { t: 'Peso na faturação', largura: '36%' }],
        linhas: D.tiposDoc.map(t => ({ c: [esc(t.nome), fmt.inteiro(t.docs), fmt.eur(t.valor), barra(Math.abs(t.valor) / Math.max(1, f.bruto) * 100, t.valor < 0)] })) }) });
      if (D.ultimosDocs) html += painel({ titulo: 'Últimos documentos', extra: ultimo ? `<span class="painel-nota">Até ${esc(fmt.dataLonga(ultimo.data))}.</span>` : '', corpo: tabela({
        colunas: [{ t: 'Data' }, { t: 'Documento' }, { t: 'Cliente' }, { t: 'Valor', dir: true }, { t: '', oculto: 'Artigos' }],
        linhas: D.ultimosDocs.map(d => ({ attrs: `data-doc="${esc(docId(d.documento))}"`, rotulo: `Ver os artigos do documento ${d.documento}`,
          c: [esc(fmt.dataCurta(d.data)), `<span class="num">${esc(d.documento)}</span>${/^[NC]$/.test(d.tipo) ? ' <span class="tag tag-preta">Crédito</span>' : ''}`, esc(nomeProprio(d.cliente)), fmt.eur2(d.total),
            `<button type="button" class="ligacao" data-doc="${esc(docId(d.documento))}" aria-label="Ver os artigos do documento ${esc(d.documento)} de ${esc(nomeProprio(d.cliente))}">Ver mais</button>`] })) }) });
      return html;
    }
    /* ---------- Artigos ---------- */
    function vistaArtigos() {
      const sub = `Artigos mais vendidos${emAno()}, por ${app.artMetrica === 'unidades' ? 'unidades' : 'faturação'}.`;
      const metrica = seg([['valor', 'Valor'], ['unidades', 'Unidades']], app.artMetrica, 'data-metrica-art', 'Ordenar artigos por');
      const espera = semDados('Artigos', sub, { extra: '' });
      if (espera) return espera;
      const D = app.dados, unidades = app.artMetrica === 'unidades';
      const lista = unidades ? D.artigosQtd : D.artigosTodos, topo = D.artigos[0];
      const marcas = D.marcas || [], subiu = melhor(marcas)[0], caiu = melhor(marcas).at(-1);
      let html = cabecalho('Artigos', sub) + indicadores([
        { valor: fmt.inteiro(D.artigosTotal), rotulo: 'Artigos vendidos', nota: 'Referências diferentes' },
        { valor: esc(topo ? nomeProprio(topo.nome) : '—'), rotulo: 'Artigo com mais faturação', nota: topo ? fmt.eur(topo.num) : '', nome: true },
        { valor: esc(subiu ? nomeProprio(subiu.nome) : '—'), rotulo: 'Marca que mais cresceu', nota: subiu ? faceA(subiu.delta, D.anoAnt) : '', nome: true },
        { valor: esc(caiu && caiu !== subiu ? nomeProprio(caiu.nome) : '—'), rotulo: 'Marca que mais caiu', nota: caiu && caiu !== subiu ? faceA(caiu.delta, D.anoAnt) : '', nome: true }]);
      const max = Math.max(1, ...(lista || []).map(i => i.num)), fatia = lista ? fatiaLista('artigos', lista) : null;
      html += painel({ titulo: 'Artigos', extra: metrica, corpo: !lista ? aCarregar('A carregar as unidades…') : tabela({
        colunas: [{ t: 'Artigo' }, { t: unidades ? 'Unidades' : 'Faturação', dir: true }, { t: '', oculto: 'Proporção', largura: '22%' }, { t: D.anoAnt ? 'Face a ' + D.anoAnt : 'Variação', dir: true }],
        linhas: fatia.itens.map(a => ({ c: [`${esc(nomeProprio(a.nome))}${a.codigo ? `<span class="celula-sub">${esc(a.codigo)}</span>` : ''}`, unidades ? esc(a.valor) : fmt.eur(a.num), barra(a.num / max * 100),
          a.novo ? '<span class="muted">Novo</span>' : variacaoHtml(a.delta)] })) }) + fatia.rodape });
      if (marcas.length) {
        const maxM = Math.max(1, ...marcas.map(m => m.num)), fm = fatiaLista('marcas', marcas, 6);
        html += painel({ titulo: 'Faturação por marca', corpo: tabela({ colunas: [{ t: 'Marca' }, { t: 'Faturação', dir: true }, { t: '', oculto: 'Proporção', largura: '30%' }, { t: D.anoAnt ? 'Face a ' + D.anoAnt : 'Variação', dir: true }],
          linhas: fm.itens.map(m => ({ c: [esc(nomeProprio(m.nome)), fmt.eur(m.num), barra(m.num / maxM * 100), variacaoHtml(m.delta)] })) }) + fm.rodape });
      }
      return html;
    }

    /* ---------- Zonas ---------- */
    const QUEDA_FORTE = -50;
    function vistaZonas() {
      const sub = `Faturação por zona${emAno()}${app.dados?.anoAnt ? ', face ao mesmo período de ' + app.dados.anoAnt : ''}. Clique numa zona para filtrar o painel.`;
      const espera = semDados('Zonas', sub);
      if (espera) return espera;
      const D = app.dados, zonas = (D.zonas || []).filter(z => z.fat > 0), reais = zonas.filter(z => !z.sem);
      const maior = [...reais].sort((a, b) => b.fat - a.fat)[0], ord = melhor(reais, z => z.variacao), subiu = ord[0], caiu = ord.at(-1);
      const max = Math.max(1, ...zonas.map(z => z.fat));
      let html = cabecalho('Zonas', sub) + indicadores([
        { valor: esc(maior ? maior.nome : '—'), rotulo: 'Zona com mais faturação', nota: maior ? fmt.eur(maior.fat) : '', nome: true },
        { valor: esc(subiu ? subiu.nome : '—'), rotulo: 'Zona que mais cresceu', nota: subiu ? faceA(subiu.variacao, D.anoAnt) : '', nome: true },
        { valor: esc(caiu && caiu !== subiu ? caiu.nome : '—'), rotulo: 'Zona que mais caiu', nota: caiu && caiu !== subiu ? faceA(caiu.variacao, D.anoAnt) : '', nome: true },
        { valor: fmt.inteiro(reais.length), rotulo: 'Zonas com vendas', nota: 'No período' }]);
      const fz = fatiaLista('zonas', zonas, 8);
      html += painel({ corpo: tabela({ legenda: 'Faturação por zona',
        colunas: [{ t: 'Zona' }, { t: 'Clientes', dir: true }, { t: 'Faturação', dir: true }, { t: '', oculto: 'Proporção', largura: '30%' }, { t: D.anoAnt ? 'Face a ' + D.anoAnt : 'Variação', dir: true }, { t: '', oculto: 'Ações' }],
        linhas: fz.itens.map(z => ({ attrs: z.sem ? '' : `data-filtrar-zona="${esc(z.id)}"`, rotulo: `Filtrar o painel pela zona ${z.nome}`,
          c: [esc(z.nome), fmt.inteiro(z.clientes), fmt.eur(z.fat), barra(z.fat / max * 100, z.variacao != null && z.variacao < QUEDA_FORTE), z.novo ? '<span class="muted">Nova</span>' : variacaoHtml(z.variacao),
            !z.sem && z.variacao != null && z.variacao < QUEDA_FORTE ? `<button type="button" class="ligacao" data-perguntar="Comparar as vendas do distrito ${esc(z.nome)} mês a mês com o ano passado">Perguntar</button>` : ''] })) }) + fz.rodape });
      if (D.localidades?.length) {
        const maxL = Math.max(1, ...D.localidades.map(l => l.num));
        html += painel({ titulo: 'Localidades com mais faturação', corpo: tabela({ colunas: [{ t: 'Localidade' }, { t: 'Faturação', dir: true }, { t: '', oculto: 'Proporção', largura: '30%' }, { t: D.anoAnt ? 'Face a ' + D.anoAnt : 'Variação', dir: true }],
          linhas: D.localidades.map(l => ({ c: [esc(l.nome), fmt.eur(l.num), barra(l.num / maxL * 100), l.novo ? '<span class="muted">Nova</span>' : variacaoHtml(l.delta)] })) }) });
      }
      return html;
    }

    /* ---------- Previsões ---------- */
    const CENARIOS = [['conservadora', 'Conservadora'], ['provavel', 'Provável'], ['otimista', 'Otimista']];
    // Cartão "Mês em curso" das Previsões: quanto do mês previsto já está faturado e se o ritmo chega. Só existe quando o mês incompleto
    // é o primeiro mês previsto. Conta dias de calendário até à última venda (não há dias úteis nos dados).
    function cartaoMesEmCurso(D, alvo, previsto) {
      const m = D.serie?.meses.find(x => x.parcial && x.atual != null);
      if (!m || alvo.a !== m.a || alvo.m !== m.m || !(previsto > 0)) return '';
      const u = fmt.lerData(D.ate), dia = u && u.a === m.a && u.m === m.m ? u.d : null, dias = new Date(m.a, m.m, 0).getDate(), nome = MESES[m.m - 1];
      const linha = (rotulo, texto, pct) => `<div class="mes-prog"><div class="mes-prog-l"><span>${rotulo}</span><b class="num">${texto}</b></div>${barra(pct)}</div>`;
      const blocos = dia ? [
        { valor: fmt.eur(m.atual / dia * dias), rotulo: 'Fecho ao ritmo atual', nota: `${variacaoHtml((m.atual / dia * dias - previsto) / previsto * 100)} face ao previsto` },
        ...(dia < dias ? [{ valor: fmt.eur((previsto - m.atual) / (dias - dia)), rotulo: 'Por dia para cumprir', nota: `Até agora: ${fmt.eur(m.atual / dia)} por dia` }] : [])] : [];
      return `<section class="painel mes-cartao" aria-labelledby="mes-t"><span class="mes-rotulo">Mês em curso</span><h2 id="mes-t">${esc(nome)} ${m.a}</h2>
        <div class="mes-progs">${linha('Faturado', `${fmt.eur(m.atual)} de ${fmt.eur(previsto)}`, m.atual / previsto * 100)}${dia ? linha('Dias decorridos', `${dia} de ${dias}`, dia / dias * 100) : ''}</div>
        ${blocos.length ? indicadores(blocos).replace('class="kpis"', 'class="mes-grelha"') : ''}
        ${dia && dia < dias ? `<p class="mes-ajuda">Ainda só passaram ${dia} dias de ${dias}: o ritmo pode mudar bastante.</p>` : '<p class="mes-ajuda"></p>'}
        <button type="button" class="btn btn-pri" data-perguntar="${esc(nome)} vai cumprir a previsão?">${ic('flow')}Perguntar ao Flow sobre ${nome.toLowerCase()}</button></section>`;
    }
    function vistaPrevisoes() {
      const sub = 'Estimativa para os próximos 3 meses, a partir da tendência dos últimos 6 meses completos.';
      const o = { periodo: true, extra: seg(CENARIOS, app.cenario, 'data-cenario', 'Cenário') };
      const espera = semDados('Previsões', sub, o);
      if (espera) return espera;
      const D = app.dados, P = D.previsao;
      if (!P) return cabecalho('Previsões', sub, o) + `<p class="estado" role="status">${esc(D.semPrevisao)}</p>`;
      const fut = P.futuros, v = f => f[app.cenario], soma = fut.reduce((s, f) => s + v(f), 0), ult = P.reais.at(-1);
      const top = fut.reduce((a, b) => v(b) > v(a) ? b : a), emCurso = cartaoMesEmCurso(D, fut[0], v(fut[0]));
      return cabecalho('Previsões', sub, o) + indicadores([
        { valor: fmt.eur(soma), rotulo: 'Previsão para os próximos 3 meses', nota: `Entre ${fmt.eur(fut.reduce((s, f) => s + f.conservadora, 0))} e ${fmt.eur(fut.reduce((s, f) => s + f.otimista, 0))}` },
        { valor: fmt.eur(v(fut[0])), rotulo: `${MESES[fut[0].m - 1]} previsto`, nota: `Entre ${fmt.eur(fut[0].conservadora)} e ${fmt.eur(fut[0].otimista)}` },
        { valor: fmt.eur(ult.atual), rotulo: 'Último mês completo', nota: `${MESES[ult.m - 1]} ${ult.a}` },
        { valor: MESES[top.m - 1], rotulo: 'Melhor mês previsto', nota: fmt.eur(v(top)), nome: true }]) +
        (emCurso ? '<div class="mes-linha">' : '') + painel({ titulo: 'Vendas e previsão por mês', extra: `<div class="legenda"><span><i></i>Real</span><span><i class="prev"></i>Previsão (${esc(CENARIOS.find(c => c[0] === app.cenario)[1].toLowerCase())})</span><span><i class="banda"></i>Intervalo provável</span>${P.reais.some(x => x.anterior != null) ? `<span><i class="ant"></i>${esc(D.anoAnt)}</span>` : ''}</div>`,
          corpo: graficoMeses({ meses: P.reais.map(x => ({ ...x, parcial: false })), interativo: false, W: emCurso ? 640 : 900, H: emCurso ? 270 : 250, rotulo: 'Vendas e previsão por mês',
            previsao: { valores: fut.map(v), banda: fut.map(f => [f.conservadora, f.otimista]), meses: fut.map(f => f.m) } }) +
            '<div class="sel-mes"><span class="sel-mes-o">A área azul clara é o intervalo provável. A previsão atualiza quando o mês em curso fechar.</span></div>' }) + emCurso + (emCurso ? '</div>' : '');
    }

    /* ---------- Guardados ---------- */
    const quando = iso => { const d = new Date(iso); return isNaN(d) ? '' : d.toLocaleDateString('pt-PT', { weekday: 'long', day: 'numeric', month: 'long' }) + ', ' + d.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }); };
    function vistaGuardados() {
      const todos = app.prefs.guardados || [], lista = guardados.filtrar(todos, app.guardFiltro);
      const n = t => guardados.filtrar(todos, t).length;
      return cabecalho('Guardados', 'Vistas e respostas do Flow que guardou para voltar mais tarde.', { iva: false, periodo: false, guardar: '', comFiltros: false, exportar: 'Exportar tudo' }) +
        chips([['todos', 'Todos', n('todos')], ['vista', 'Vistas', n('vista')], ['resposta', 'Respostas do Flow', n('resposta')]], app.guardFiltro, 'data-filtro-guardados', 'Tipo') +
        painel({ corpo: !todos.length ? '<p class="vazio">Ainda não guardou nada. Use "Guardar vista" no topo de cada separador ou "Guardar resposta" no Flow.</p>' : tabela({
          colunas: [{ t: 'Nome' }, { t: 'Tipo' }, { t: 'Guardado' }, { t: '', oculto: 'Ações' }],
          linhas: lista.map(g => ({ c: [esc(g.nome), g.tipo === 'vista' ? '<span class="tag tag-azul">Vista</span>' : '<span class="tag">Resposta do Flow</span>', `<span class="muted">${esc(quando(g.quando))}</span>`,
            `<span style="display:flex;gap:14px;justify-content:flex-end"><button type="button" class="ligacao" data-abrir-guardado="${esc(g.id)}">Abrir</button><button type="button" class="ligacao" data-exportar-guardado="${esc(g.id)}" aria-haspopup="menu">Exportar</button><button type="button" class="ligacao ligacao-discreta" data-remover-guardado="${esc(g.id)}">Remover</button></span>`] })) }) });
    }

    /* ---------- Resposta do Flow aberta no painel principal ---------- */
    function vistaResposta() {
      const r = app.resposta, a = r.analise;
      return cabecalho(a.titulo, `Pergunta: "${r.pergunta}"`, { periodo: false, comFiltros: false, guardar: 'Guardar resposta',
          extra: `<button type="button" class="btn" data-fechar-resposta>Voltar a ${esc(nomeSeparador(app.separador))}</button>` }) +
        (a.sub ? `<p class="cabeca-iva" style="margin-top:10px">${esc(a.sub)}.</p>` : '') +
        a.avisos.map(t => `<p class="aviso-dados" role="note">${ic('info')}<span>${esc(t)}</span></p>`).join('') +
        a.paragrafos.map((p, i) => `<p class="${i ? 'resp-paragrafo' : 'destaque'}">${esc(p)}</p>`).join('') + (a.nota ? `<p class="cabeca-iva" style="margin-top:10px">${esc(a.nota)}.</p>` : '') + (a.painel || '');
    }

    /* ---------- O que cada separador exporta (a vista atual, com o período e os filtros) ---------- */
    function exportacaoAtual() {
      if (app.resposta) return { titulo: app.resposta.analise.titulo, linhas: app.resposta.analise.linhas || [['Resposta'], [app.resposta.texto]] };
      const D = app.dados, titulo = `${nomeSeparador(app.separador)} ${descricaoFiltros()}`;
      if (app.separador === 'guardados') return { titulo: 'Guardados', linhas: [['Nome', 'Tipo', 'Separador', 'Período', 'Filtros', 'Pergunta', 'Guardado'],
        ...(app.prefs.guardados || []).map(g => [g.nome, g.tipo === 'vista' ? 'Vista' : 'Resposta do Flow', nomeSeparador(g.separador), g.periodo ? nomePeriodo(g.periodo) : '',
          g.filtros ? filtros.ativos(filtros.limpos(g.filtros), NOMES_FILTROS).map(a => a.rotulo).join(', ') : '', g.pergunta || '', quando(g.quando)])] };
      if (app.separador === 'alertas' && app.alertaAba === 'personalizados') return { titulo: 'Alertas personalizados', linhas: [['Alerta', 'Condição', 'Frequência', 'Último aviso', 'Ativo'],
        ...(app.prefs.alertasPers || []).map(a => [a.nome, alertasPers.frase(a, nomeZona), a.freq, a.ultimo ? fmt.dataLonga(a.ultimo, true) : 'Ainda não avisou', a.ligada ? 'Sim' : 'Não']),
        ...REGRAS_ALERTA.map(r => alertasRegras.regra(app.prefs.alertas, r[0])).map(r => [r.nome, r.frase(r.limite), 'Ao abrir o painel', '', r.ligada ? 'Sim' : 'Não'])] };
      if (!D || D.erro) return null;
      return { titulo, linhas: linhasDe(app.separador, D) };
    }
    function linhasDe(sep, D) {
      const K = D.kpis, ant = D.anoAnt || 'Ano anterior';
      const kpis = [['Indicador', 'Valor', 'Variação (%)'], ['Faturação total (€)', K.faturacao.valor, K.faturacao.variacao], ['Documentos', K.documentos.valor, K.documentos.variacao],
        ['Valor médio (€)', K.medio.valor, K.medio.variacao], ['Clientes ativos', K.clientes.valor, K.clientes.variacao]];
      switch (sep) {
        case 'resumo': return [...kpis, [], ['Mês', `Faturação (€)`, `${ant} (€)`, 'Variação (%)', 'Notas'],
          ...(D.serie ? D.serie.meses : []).map(x => [`${MESES[x.m - 1]} ${x.a}`, x.atual, x.anterior, x.parcial ? '' : x.delta, x.parcial ? 'Mês incompleto' : ''])];
        case 'alertas': return [['Alerta', 'Detalhe'], ...alertasAtivos(D).map(a => [a.titulo, a.texto])];
        case 'clientes': return [['Cliente', 'Código', 'Zona', 'Faturação (€)', 'Variação (%)', 'Última compra', 'Estado'],
          ...clientesFiltrados(D).map(c => [nomeProprio(c.nome), c.codigo, c.zona || '', c.num, c.delta, c.ultima || '', { ativo: 'Ativo', desvio: 'Com desvio', risco: 'Em risco', sem: 'Sem compras' }[estadoCliente(c)]])];
        case 'faturacao': return [...kpis, [], ['Tipo de documento', 'Documentos', 'Faturação (€)'], ...(D.tiposDoc || []).map(t => [t.nome, t.docs, t.valor]), [],
          ['Data', 'Documento', 'Cliente', 'Valor (€)'], ...(D.ultimosDocs || []).map(d => [d.data, d.documento, d.cliente, d.total])];
        case 'artigos': return [['Artigo', 'Código', app.artMetrica === 'unidades' ? 'Unidades' : 'Faturação (€)', 'Variação (%)'],
          ...((app.artMetrica === 'unidades' ? D.artigosQtd : D.artigosTodos) || []).map(a => [a.nome, a.codigo, a.num, a.delta]), [],
          ['Marca', 'Faturação (€)', 'Variação (%)'], ...(D.marcas || []).map(m => [m.nome, m.num, m.delta])];
        case 'zonas': return [['Zona', 'Clientes', 'Faturação (€)', 'Variação (%)'], ...(D.zonas || []).map(z => [z.nome, z.clientes, z.fat, z.variacao]), [],
          ['Localidade', 'Faturação (€)', 'Variação (%)'], ...(D.localidades || []).map(l => [l.nome, l.num, l.delta])];
        case 'previsoes': return D.previsao ? [['Mês', 'Real (€)', 'Conservadora (€)', 'Provável (€)', 'Otimista (€)'],
          ...D.previsao.reais.map(x => [`${MESES[x.m - 1]} ${x.a}`, x.atual, '', '', '']), ...D.previsao.futuros.map(f => [`${MESES[f.m - 1]} ${f.a}`, '', f.conservadora, f.provavel, f.otimista])] : [['Previsão'], [D.semPrevisao]];
        default: return kpis;
      }
    }

    const VISTAS = { resumo: vistaResumo, alertas: vistaAlertas, clientes: vistaClientes, faturacao: vistaFaturacao, artigos: vistaArtigos, zonas: vistaZonas, previsoes: vistaPrevisoes, guardados: vistaGuardados };

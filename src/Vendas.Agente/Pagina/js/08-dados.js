    /* =========================================================
       Dados do painel: tudo vem do servidor MCP (rota /resumo e /api/…), nunca do agente de IA.
       ========================================================= */
    const nomeUtilizador = (n => n && !n.startsWith('{{') ? n.trim() : '')(document.body.dataset.utilizador);
    const app = {
      separador: 'resumo', periodo: 'AnoAtual', filtros: filtros.vazios(),
      dados: null, erro: false, aCarregar: false, pedidos: 0,
      vendedor: document.body.dataset.vendedor === '1',
      prefs: { guardados: [], alertas: {} },
      resposta: null,           // resposta do Flow aberta no painel principal
      // estado de cada separador
      mes: null, cliEstado: 'todos', cliQ: '', alertaAba: 'recebidos', alertaFiltro: 'todos', guardFiltro: 'todos',
      artMetrica: 'valor', cenario: 'provavel', credTipo: 'Todos', credQ: '', credPag: 0
    };
    const TIPOS_DOC = { F: 'Faturas (F)', V: 'Faturas-recibo (V)', N: 'Notas de crédito s/ faturas (N)', C: 'Créditos s/ faturas-recibo (C)' };

    // Preferências por utilizador (Guardados, regras de alerta): no servidor, um ficheiro por login
    async function carregarPrefs() {
      try { app.prefs = { guardados: [], alertas: {}, ...(await pedirJson('/api/preferencias')) }; }
      catch (e) { console.error('Falha a ler as preferências:', e); }
    }
    async function guardarPrefs() {
      const r = await fetch('/preferencias', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(app.prefs) });
      if (!r.ok) throw new Error('HTTP ' + r.status);
    }

    /* ---------- Resumo: uma consulta por período e filtros, guardada nesta sessão ---------- */
    const resumos = new Map();
    const consultaAtual = () => filtros.consulta(app.periodo, app.filtros);
    async function carregarResumo() {
      const q = new URLSearchParams(consultaAtual()), chave = q.toString(), n = ++app.pedidos;
      const guardado = resumos.get(chave);
      Object.assign(app, { aCarregar: !guardado, erro: false });
      if (!guardado) redesenhar();
      try {
        if (guardado) app.dados = guardado;
        else {
          const j = await pedirJson('/resumo?' + q);
          if (n !== app.pedidos) return;
          app.dados = montarResumo(j);
          resumos.set(chave, app.dados);
        }
        if (app.artMetrica === 'unidades') carregarArtigosQtd();
      } catch (e) {
        if (n !== app.pedidos) return;
        console.error('Falha a carregar o resumo:', e);
        Object.assign(app, { dados: null, erro: true });
      }
      app.aCarregar = false;
      redesenhar();
    }
    // Artigos por unidades: só se pede quando se escolhe "Unidades"
    async function carregarArtigosQtd() {
      const D = app.dados;
      if (!D || D.artigosQtd || D.artQtdPedido) return;
      D.artQtdPedido = true;
      try {
        const q = new URLSearchParams({ ...consultaAtual(), ordenar: 'Quantidade' });
        D.artigosQtd = lerLista((await pedirJson('/resumo?' + q)).artigos || '').itens.filter(i => i.num > 0);
      } catch (e) {
        console.error('Falha a carregar os artigos por unidades:', e);
        app.artMetrica = 'valor';
        aviso('Não foi possível carregar as unidades. Tente outra vez.');
      }
      D.artQtdPedido = false;
      if (app.dados === D) redesenhar();
    }

    // Fonte única dos números da Faturação: N corrige F e C corrige V (já vêm negativos)
    function faturacaoDe(linhas) {
      const soma = id => linhas.filter(x => x.tipo === id).reduce((t, x) => ({ valor: t.valor + x.valor, documentos: t.documentos + x.documentos }), { valor: 0, documentos: 0 });
      const [F, V, N, C] = ['F', 'V', 'N', 'C'].map(soma);
      return { bruto: F.valor + V.valor, creditos: N.valor + C.valor, liquido: F.valor + V.valor + N.valor + C.valor, docCreditos: N.documentos + C.documentos, documentos: F.documentos + V.documentos };
    }

    // Textos e JSON das ferramentas -> números dos ecrãs. Sem dados para um bloco, o bloco fica vazio (nunca inventa).
    function montarResumo(j) {
      const textoMeses = j.meses || '', linha1 = textoMeses.split('\n')[0].trim();
      // "Cliente … não encontrado", "Vários clientes começam por …" (com a lista id,cliente) ou "Sem vendas: …"
      if (!/^Vendas por/.test(linha1)) {
        const [mensagem, lista = ''] = textoMeses.split(/\r?\nid,cliente\r?\n/);
        return { erro: mensagem.trim(), opcoes: lista.split(/\r?\n/).map(l => partir(l.trim(), ',')).filter(c => c.length === 2 && c[0] && !c[0].startsWith('#')) };
      }
      const hoje = new Date();
      const num = (txt, re) => { const m = (txt || '').match(re); return m ? paraNumero(m[1]) : null; };
      const facto = (r, re) => r.factos.find(([k]) => re.test(k)) || ['', ''];
      const rm = lerLista(textoMeses), serie = lerSerie(rm), meses = serie ? serie.meses : [];
      const anoAnt = (facto(rm, /^comparado com/i)[0].match(/(\d{4})$/) || [])[1] || null;
      const total = num(rm.resumo, /total\s+(-?[\d\s.,  ]+?)\s*€/), delta = num(facto(rm, /^comparado com/i)[1], /\(([+\-−]?[\d,]+)%\)/);
      const agora = x => x.a === hoje.getFullYear() && x.m === hoje.getMonth() + 1;
      meses.forEach(x => { if (x.atual != null && agora(x)) x.parcial = true; });
      const rc = j.clientes ? lerLista(j.clientes) : null, ativos = rc ? num(rc.resumo, /em\s+([\d\s  ]+?)\s+clientes/) : null;
      const positivos = texto => lerLista(texto || '').itens.filter(i => i.num > 0);
      const umAnoAntes = r => r ? facto(r, /^um ano antes/i)[1] : '';
      const variacaoDe = (r, antes) => num(umAnoAntes(r), new RegExp(antes + '[^(]*\\(([+\\-−]?[\\d,]+)%\\)'));
      let docs = null, carteira = null;
      try { docs = JSON.parse(j.documentos || 'null'); } catch { }
      try { carteira = JSON.parse(j.carteira || 'null'); } catch { }
      const fichaCarteira = new Map((carteira ? carteira.clientes : []).map(c => [String(c.id).trim(), c]));

      const kpis = {
        faturacao: { valor: total, variacao: delta },
        documentos: { valor: num(rm.resumo, /,\s*([\d\s  ]+?)\s+documentos/), variacao: variacaoDe(rm, 'documentos') },
        medio: { valor: num(facto(rm, /^valor m[ée]dio/i)[1], /^(-?[\d\s.,  ]+)/), variacao: variacaoDe(rm, 'por documento') },
        clientes: { valor: ativos, variacao: variacaoDe(rc, 'clientes') }
      };

      // Previsão: tendência dos últimos 6 meses completos, 3 meses à frente, com intervalo provável (±1,28 desvios ≈ 80%)
      // shortcut: reta simples; trocar por um modelo com sazonalidade quando houver vários anos de histórico.
      let previsao = null, semPrevisao = '';
      const completos = meses.filter(x => x.atual != null && !x.parcial);
      if (!['AnoAtual', 'Ultimos12Meses'].includes(app.periodo)) semPrevisao = 'A previsão só se calcula para o ano ou para os últimos 12 meses.';
      else if (completos.length < 6) semPrevisao = 'Ainda não há 6 meses completos para prever a faturação.';
      else {
        const ult = completos.slice(-6), ys = ult.map(x => x.atual), my = ys.reduce((s, v) => s + v, 0) / 6;
        const b = ys.reduce((s, y, i) => s + (i - 2.5) * (y - my), 0) / ys.reduce((s, _, i) => s + (i - 2.5) ** 2, 0), a = my - b * 2.5;
        const desvio = Math.sqrt(ys.reduce((s, y, i) => s + (y - (a + b * i)) ** 2, 0) / 4);
        previsao = { reais: ult, futuros: [1, 2, 3].map(h => {
          const d = new Date(ult[5].a, ult[5].m - 1 + h, 1), v = Math.max(0, a + b * (5 + h)), e = 1.28 * desvio * Math.sqrt(h);
          return { a: d.getFullYear(), m: d.getMonth() + 1, provavel: v, conservadora: Math.max(0, v - e), otimista: v + e };
        }) };
      }

      const artigosTodos = positivos(j.artigos), artigos = artigosTodos.slice(0, 10);
      const distritos = positivos(j.zonas), zonasReais = distritos.filter(i => !/^\(/.test(i.nome));
      const nClientes = new Map((j.distritosClientes || '').split(/\r?\n/).slice(2).map(l => partir(l.trim(), ',')).filter(c => c.length === 3).map(c => [c[0].toLowerCase(), +c[1]]));
      const parados = num(j.semVendas, /^Clientes sem vendas[^:]*:\s*([\d\s  ]+?)\s+clientes/);

      // Alertas: só o que os dados dizem (deteção de desvios que já existia). O limite de cada regra filtra-os depois.
      const idAlerta = (regra, nome) => [app.periodo, app.filtros.zona, app.filtros.cliente, regra, nome].join('|');
      const quebras = itens => itens.filter(i => i.delta <= -LIMITE_MIN).sort((a, b) => a.delta - b.delta);
      const menos = i => `menos ${fmt.pct(Math.abs(i.delta))} que no mesmo período de ${anoAnt}`;
      const alertas = rm.avisos.map(a => ({ id: idAlerta('dados', a), tipo: 'dados', icone: 'info', titulo: 'Dados incompletos', texto: maiuscula(a), pergunta: '' }));
      quebras(app.filtros.zona ? zonasReais.filter(z => z.nome === app.filtros.zona) : zonasReais).forEach(z => alertas.push({ id: idAlerta('distrito', z.nome), tipo: 'zonas', regra: 'distrito', delta: z.delta, icone: 'descida', zona: z.nome,
        titulo: `${nomeZona(z.nome)} caiu ${fmt.pct(Math.abs(z.delta))}`, texto: `Faturou ${fmt.eur(z.num)}, ${menos(z)}.`, pergunta: `Porque caiu a faturação do distrito ${nomeZona(z.nome)} face ao ano passado?` }));
      quebras(artigos).forEach(a => alertas.push({ id: idAlerta('artigo', a.nome), tipo: 'artigos', regra: 'artigo', delta: a.delta, icone: 'descida',
        titulo: `${a.nome} caiu ${fmt.pct(Math.abs(a.delta))}`, texto: `Vendeu ${fmt.eur(a.num)}, ${menos(a)}.`, pergunta: app.filtros.cliente ? '' : 'Comparar as vendas por artigo com o ano passado' }));
      quebras(positivos(j.clientes).slice(0, 10)).forEach(c => alertas.push({ id: idAlerta('cliente', c.nome), tipo: 'clientes', regra: 'cliente', delta: c.delta, icone: 'descida', cliente: c.codigo || c.nome, nome: c.nome,
        titulo: `${nomeProprio(c.nome)} com desvio`, texto: `Comprou ${fmt.eur(c.num)}, ${menos(c)}.`, pergunta: `O que mudou nas compras do cliente ${nomeProprio(c.nome)} este ano?` }));
      if (parados) alertas.push({ id: idAlerta('parados', ''), tipo: 'clientes', regra: 'parados', icone: 'semClientes', titulo: `${fmt.inteiro(parados)} clientes sem compras`,
        texto: 'Clientes com histórico que não compram há 90 dias.', pergunta: 'Clientes que não compram há 90 dias?' });

      return {
        serie, anoAnt, total, kpis, previsao, semPrevisao, alertas, parados,
        // "Dados até": a última venda registada na BD (consultar_documentos); nunca a data de hoje
        ate: (docs && docs.ultimaVenda) || (textoMeses.match(/[uú]ltima venda registada[^\d]*(\d{2}-\d{2}-\d{4})/) || [])[1] || '',
        cliente: (linha1.match(/cliente "([^"]+)"/) || [])[1] || null,
        datas: (linha1.match(/\((\d{2}-\d{2}-\d{4}) a (\d{2}-\d{2}-\d{4})/) || []).slice(1),
        artigos, artigosTodos, artigosTotal: +((j.artigos || '').match(/Mostro \d+ de (\d+)/) || [])[1] || null,
        marcas: j.marcas ? positivos(j.marcas) : null,
        zonas: j.zonas ? distritos.map(i => { const sem = /^\(/.test(i.nome), id = sem ? 'sem dados' : i.nome;
          return { id, nome: nomeZona(id), sem, fat: i.num, variacao: i.delta, novo: i.novo, clientes: nClientes.get(id.toLowerCase()) }; }) : null,
        localidades: j.localidades ? positivos(j.localidades).map(i => ({ nome: /^\(/.test(i.nome) ? 'Sem localidade na ficha' : nomeProprio(i.nome), num: i.num, delta: i.delta, novo: i.novo })) : null,
        clientes: j.clientes ? positivos(j.clientes).slice(0, 10) : null,
        carteira: j.clientes ? positivos(j.clientes).map(c => ({ ...c, ...fichaCarteira.get(c.codigo) })) : null,
        tiposDoc: docs ? docs.tipos.map(t => ({ tipo: t.tipo, nome: TIPOS_DOC[t.tipo] || 'Tipo ' + t.tipo, valor: t.valor, docs: t.documentos })) : null,
        faturacao: docs ? faturacaoDe(docs.tipos) : null,
        ultimosDocs: docs ? docs.ultimos : null
      };
    }

    // Alertas detetados que contam, com as regras e os limites de cada utilizador
    const alertasAtivos = D => !D || !D.alertas ? [] : D.alertas.filter(a => alertasRegras.conta(app.prefs.alertas, a));
    // Os já vistos só nesta sessão (têm nomes de clientes): o número no sino e no menu são os novos
    const ALERTAS_VISTOS = 'flow-alertas-vistos';
    const alertasVistos = new Set((() => { try { return JSON.parse(sessionStorage.getItem(ALERTAS_VISTOS)) || []; } catch { return []; } })());
    const nAlertasNovos = () => alertasAtivos(app.dados).filter(a => !alertasVistos.has(a.id)).length;
    function marcarAlertasVistos() {
      alertasAtivos(app.dados).forEach(a => alertasVistos.add(a.id));
      try { sessionStorage.setItem(ALERTAS_VISTOS, JSON.stringify([...alertasVistos])); } catch { }
    }

    /* ---------- Notas de crédito e créditos (lista paginada no servidor) ---------- */
    const CRED_POR_PAGINA = 15;
    const creditos = { dados: null, erro: false, chave: null, pedidos: 0 };
    async function carregarCreditos() {
      const n = ++creditos.pedidos, q = new URLSearchParams({ ...consultaAtual(), page: app.credPag + 1 });
      if (app.credTipo !== 'Todos') q.set('tipo', app.credTipo);
      if (app.credQ.trim()) q.set('q', app.credQ.trim());
      try {
        const j = await pedirJson('/api/creditos?' + q);
        if (n !== creditos.pedidos) return;
        Object.assign(creditos, { dados: j, erro: false });
      } catch (e) {
        if (n !== creditos.pedidos) return;
        console.error('Falha a carregar os créditos:', e);
        creditos.erro = true;
      }
      if (app.separador === 'faturacao' && !app.resposta) desenharCreditos();
    }

    // "N 10/2026/1034675" -> "N-10-2026-1034675" (o id das rotas /api/documentos/)
    const docId = doc => doc.replace(' ', '-').replaceAll('/', '-');

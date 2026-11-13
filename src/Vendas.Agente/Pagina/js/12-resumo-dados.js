
    /* ---------- Painel de dados + conversa ---------- */
    const painelEl = document.getElementById('painel');
    const painelRaiz = document.querySelector('.chat-painel');
    let abaAtiva = null;      // resposta aberta no painel (null: o Resumo)

    /* =========================================================
       PROPOSTA D 11/2026 — Resumo: painel inicial + cartão fixo no painel lateral (não é um separador)
       GET /resumo?periodo=AnoAtual[&cliente=1003][&zona=Balcão] devolve os textos da ferramenta consultar_vendas
       (os mesmos do chat): lêem-se com lerLista/lerSerie e montam-se os blocos.
       Carrega ao abrir a página; filtros, "resumo do cliente X" na conversa e as vistas guardadas voltam a carregar.
       ========================================================= */
    const RES_PERIODOS = [['Ultimos7Dias', 'Últimos 7 dias'], ['MesAtual', 'Este mês'], ['TrimestreAtual', 'Trimestre'], ['AnoAtual', 'Este ano'], ['Ultimos12Meses', 'Últimos 12 meses'], ['AnoAnterior', 'Ano passado']];
    const resumoEstado = { periodo: 'AnoAtual', zona: null, cliente: null, zonas: [], dados: null, erro: null, aCarregar: false, menu: null, alertaSel: null, alertaFiltro: 'Todos', cliSel: null, marcaSel: null, artVer: false, artPag: 0, artMetrica: 'valor', artQtdACarregar: false, artQtdErro: false, cliSeg: 'Todos', cliPag: 0, credTipo: 'Todos', credBusca: '', credPag: 0, fatVista: 'mes', fatDocs: 'cred' };
    // Vistas do Resumo (painel lateral): cada uma mostra só os seus cartões, em vez de tudo numa só página
    const SECOES = [['geral', 'Resumo das vendas'], ['alertas', 'Alertas'], ['clientes', 'Clientes'], ['faturacao', 'Faturação'],
                    ['artigos', 'Artigos'], ['zonas', 'Zonas'], ['previsoes', 'Previsões']];
    let secao = 'geral';
    const nomeSecao = () => SECOES.find(s => s[0] === secao)[1];
    let boasVindasAberta = true;   // ecrã de boas-vindas à frente até se escolher uma opção ou fazer uma pergunta
    let vistaResumo = true;   // o painel inicial (Resumo) está à frente; um separador aberto passa-o para trás
    let pedidosResumo = 0;    // só a resposta do último pedido conta (mudar de filtro a meio)

    // Alertas: as regras (sem dados) ficam neste computador; os lidos têm nomes de clientes, por isso só duram a sessão, como as vistas guardadas.
    const lerGuardado = (onde, chave, omissao) => { try { return JSON.parse(window[onde].getItem(chave)) ?? omissao; } catch { return omissao; } };
    const guardarValor = (onde, chave, valor) => { try { window[onde].setItem(chave, JSON.stringify(valor)); } catch { } };
    const alertasLidos = new Set(lerGuardado('sessionStorage', 'flow-alertas-lidos', []));
    const guardarLidos = () => guardarValor('sessionStorage', 'flow-alertas-lidos', [...alertasLidos]);
    const regrasAlerta = lerGuardado('localStorage', 'flow-alertas-regras', {});
    const REGRAS_ALERTA = [['distrito', 'Distrito cai mais de 20%', 'Comparado com o mesmo período do ano anterior'], ['artigo', 'Artigo cai mais de 25%', 'Entre os artigos mais vendidos'],
                           ['cliente', 'Cliente cai mais de 25%', 'Entre os maiores clientes'], ['parados', 'Clientes sem compras há 90 dias', 'Em toda a carteira']];
    const SEV_ORDEM = { 'Crítico': 0, 'Aviso': 1, 'Info': 2 }, SEV_CLASSE = { 'Crítico': 'res-sev--critico', 'Aviso': 'res-sev--aviso', 'Info': 'res-sev--info' };
    const alertaPorLer = a => !alertasLidos.has(a.id);
    const FILTROS_ALERTA = [['Todos', () => true], ['Por ler', alertaPorLer], ['Críticos', a => a.sev === 'Crítico'], ['Avisos', a => a.sev === 'Aviso'], ['Info', a => a.sev === 'Info']];
    const alertasAtivos = D => !D || !D.alertas ? [] : D.alertas.filter(a => !a.regra || regrasAlerta[a.regra] !== false).sort((a, b) => SEV_ORDEM[a.sev] - SEV_ORDEM[b.sev]);
    const alertasVisiveis = D => alertasAtivos(D).filter((FILTROS_ALERTA.find(f => f[0] === resumoEstado.alertaFiltro) || FILTROS_ALERTA[0])[1]);
    const nAlertasPorLer = D => alertasAtivos(D).filter(alertaPorLer).length;

    const rNB = ' ';
    const rGrupos = n => { let s = String(Math.round(Math.abs(n))), o = ''; while (s.length > 3) { o = rNB + s.slice(-3) + o; s = s.slice(0, -3); } return (n < 0 ? '−' : '') + s + o; };
    const rDec = (n, d) => { const [i, f] = Math.abs(n).toFixed(d).split('.'); return (n < 0 ? '−' : '') + rGrupos(+i) + (f ? ',' + f : ''); };
    const rEur = n => rGrupos(n) + rNB + '€';
    const rEur2 = n => rDec(n, 2) + rNB + '€';
    const rMil = n => rDec(n / 1000, 1) + rNB + 'mil' + rNB + '€';
    const rCurto = n => Math.abs(n) >= 1e6 ? rDec(n / 1e6, 2) + rNB + 'M€' : Math.abs(n) >= 1000 ? rMil(n) : rEur(n);   // um cliente pode faturar centenas, não milhões
    const rPct = n => rDec(n, 1) + '%';
    const rVar = v => v == null ? '–' : (v >= 0 ? '▲ ' : '▼ ') + rDec(Math.abs(v), 1) + '%';
    const rVarCls = v => v == null ? '' : v >= 0 ? 'res-sobe' : 'res-desce';
    const rEsc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
    const rValorKpi = k => k.valor == null ? '—' : k.tipo === 'eur' ? rEur(k.valor) : k.tipo === 'eur2' ? rEur2(k.valor) : rGrupos(k.valor);
    // Escala do eixo: passos "redondos" (50 mil, 5 mil, 500 €), no máximo 6 linhas
    function rEscala(valores) {
      const maxV = Math.max(...valores), minV = Math.max(0, Math.min(...valores));
      let passo = Math.pow(10, Math.floor(Math.log10(Math.max(maxV, 1)))) / 2;
      while ((maxV - minV) / passo > 6) passo *= 2;
      return { passo, max: Math.ceil(maxV / passo) * passo + passo / 5, min: Math.floor(minV / passo) * passo };
    }
    function rCurva(p) {
      let d = `M${p[0][0]},${p[0][1]}`;
      for (let i = 0; i < p.length - 1; i++) {
        const p0 = p[i - 1] || p[i], p1 = p[i], p2 = p[i + 1], p3 = p[i + 2] || p2;
        d += ` C${(p1[0] + (p2[0] - p0[0]) / 6).toFixed(1)},${(p1[1] + (p2[1] - p0[1]) / 6).toFixed(1)} ${(p2[0] - (p3[0] - p1[0]) / 6).toFixed(1)},${(p2[1] - (p3[1] - p1[1]) / 6).toFixed(1)} ${p2[0].toFixed(1)},${p2[1].toFixed(1)}`;
      }
      return d;
    }

    // A única porta para os dados do Resumo: conversa, sugestões, botão "Ver resumo", Top clientes e rankings chamam todos esta.
    // Os dados só carregam aqui, quando alguém os pede. cliente: ID ou início do nome (null = todas as vendas).
    function pedirResumo(periodo, cliente = null, zona = null) {
      Object.assign(resumoEstado, { periodo: periodo || resumoEstado.periodo, cliente: cliente || null, zona: zona || null, menu: null });
      vistaResumo = true;
      abaAtiva = null;
      desenharConversa();   // o cartão da resposta aberta deixa de dizer "Aberto no painel"
      carregarResumo();
    }

    // Pedido de resumo escrito na conversa -> { periodo, cliente }, ou null para seguir para o agente.
    // Estreita de propósito: "faturação por zona", "comparar com o ano passado" ou um artigo/zona concretos são perguntas ao agente.
    // "Hoje" também segue para o agente: o Resumo não tem esse período.
    function intencaoResumo(texto) {
      const t = semAcento(texto);
      if (!/\b(resumo|relatorio|visao geral|panorama|como (estao|esta|vao|vai) a?s? ?(vendas|faturacao))\b/.test(t)) return null;
      if (/\b(hoje|por|cada|face|compar\w*|top|melhor\w*|artigos?|familias?|marcas?|zonas?|localidades?|distritos?|vendedor\w*)\b/.test(t)) return null;
      const cliente = (texto.match(/\bcliente\s+["“«]?(.+?)["”»]?\s*\??$/i) || [])[1] || null;
      if (/\bclientes?\b/.test(t) && !cliente) return null;   // "resumo dos clientes": pergunta ao agente
      const periodo = /\bano passado\b/.test(t) ? 'AnoAnterior' : /\b12 meses\b/.test(t) ? 'Ultimos12Meses' : /\bsemana\b/.test(t) ? 'Ultimos7Dias'
        : /\btrimestre\b/.test(t) ? 'TrimestreAtual' : /\bmes\b/.test(t) ? 'MesAtual' : 'AnoAtual';
      return { periodo, cliente };
    }

    // "Abri o resumo das vendas de 2026 no painel."
    function fraseResumo({ periodo, cliente }) {
      const ano = new Date().getFullYear();
      const quando = { AnoAtual: `de ${ano}`, AnoAnterior: `de ${ano - 1}`, MesAtual: 'deste mês', TrimestreAtual: 'deste trimestre',
                       Ultimos12Meses: 'dos últimos 12 meses', Ultimos7Dias: 'dos últimos 7 dias' }[periodo];
      return `Abri o resumo das vendas ${quando}${cliente ? ` do cliente ${cliente}` : ''} no painel.`;
    }

    // Pedido escrito: bolha da pergunta + resposta curta com o cartão, como as outras respostas, mas sem passar pelo modelo
    function responderComResumo(texto, pedido) {
      historico.unshift({ pergunta: texto, texto: fraseResumo(pedido), resumo: pedido, erro: null, hora: new Date(), horaResposta: new Date() });
      pedirResumo(pedido.periodo, pedido.cliente);
      anunciar('Resumo aberto no painel.');
    }

    // Período de uma resposta ("01-01-2026 a 02-11-2026") em palavras que intencaoResumo percebe; null se não for um dos do Resumo
    function periodoEmPalavras(periodo) {
      const m = (periodo || '').match(/^(\d{2})-(\d{2})-(\d{4}) a (\d{2})-(\d{2})-(\d{4})$/);
      if (!m) return null;
      const hoje = new Date(), ano = hoje.getFullYear(), mes = hoje.getMonth() + 1;
      const [, d1, m1, a1, d2, m2, a2] = m.map(Number);
      if (d1 === 1 && m1 === 1 && a1 === ano - 1 && d2 === 31 && m2 === 12 && a2 === ano - 1) return 'do ano passado';
      if (d2 !== hoje.getDate() || m2 !== mes || a2 !== ano) return null;   // os períodos "deste …" acabam hoje
      if (d1 === 1 && a1 === ano && m1 === 1) return 'deste ano';
      if (d1 === 1 && a1 === ano && m1 === mes) return 'deste mês';
      if (d1 === 1 && a1 === ano && m1 === mes - (mes - 1) % 3) return 'deste trimestre';
      return null;
    }

    // Resumos já carregados nesta sessão, por período + zona + cliente: voltar a uma vista ou filtro não chama o servidor,
    // como os separadores das respostas. O cartão mostra "Guardado às …" com a hora em que os dados vieram.
    // shortcut: só em memória (recarregar a página volta a pedir); sem prazo de validade, porque a sessão é curta.
    const resumosGuardados = new Map();
    function parametrosResumo() {
      const q = new URLSearchParams({ periodo: resumoEstado.periodo });
      if (resumoEstado.cliente) q.set('cliente', resumoEstado.cliente);
      if (resumoEstado.zona) q.set('zona', resumoEstado.zona);
      return q;
    }

    // Botão "Unidades" da vista Artigos: a lista por unidades só se pede quando é precisa e fica com os dados
    // deste filtro (voltar a ele não pede outra vez). Se falhar, volta a "Valor" com uma mensagem.
    async function carregarArtigosQtd() {
      const dados = resumoEstado.dados;
      if (!dados || dados.artigosQtd || resumoEstado.artQtdACarregar) return;
      Object.assign(resumoEstado, { artQtdACarregar: true, artQtdErro: false });
      try {
        const q = parametrosResumo();
        q.set('ordenar', 'Quantidade');
        const r = await fetch('/resumo?' + q);
        if (!r.ok) throw new Error('HTTP ' + r.status);
        dados.artigosQtd = lerLista((await r.json()).artigos || '').itens.filter(i => i.num > 0);
      } catch (erro) {
        console.error('Falha a carregar os artigos por unidades:', erro);
        if (resumoEstado.dados === dados) Object.assign(resumoEstado, { artMetrica: 'valor', artQtdErro: true });
      }
      resumoEstado.artQtdACarregar = false;
      // O filtro mudou a meio do pedido: as unidades dos dados novos ainda não foram pedidas (este pedido bloqueava-as)
      if (resumoEstado.dados !== dados) { if (resumoEstado.artMetrica === 'unidades') carregarArtigosQtd(); return; }
      if (!vistaResumo) return;
      const foco = document.activeElement?.dataset?.artMetrica;   // quem usa teclado não perde o lugar
      desenharPainel();
      if (foco) painelEl.querySelector(`[data-art-metrica="${resumoEstado.artMetrica}"]`)?.focus();
    }

    async function carregarResumo() {
      const n = ++pedidosResumo;
      const chave = JSON.stringify([resumoEstado.periodo, resumoEstado.zona, resumoEstado.cliente]);
      const guardado = resumosGuardados.get(chave);
      Object.assign(resumoEstado, { aCarregar: !guardado, erro: null, menu: null });
      if (!guardado) { desenharLateral(); if (vistaResumo) desenharPainel(); }
      try {
        if (guardado) resumoEstado.dados = guardado;
        else {
          const r = await fetch('/resumo?' + parametrosResumo());
          if (!r.ok) throw new Error('HTTP ' + r.status);
          const j = await r.json();
          if (n !== pedidosResumo) return;
          resumoEstado.dados = montarResumo(j);
          resumosGuardados.set(chave, resumoEstado.dados);   // só as respostas que chegaram: uma falha volta a tentar
        }
        if (!resumoEstado.cliente && resumoEstado.dados.locais) resumoEstado.zonas = resumoEstado.dados.locais;
        // Outro período ou filtro com "Unidades" escolhido: as unidades destes dados também se pedem
        if (resumoEstado.artMetrica === 'unidades') carregarArtigosQtd();
      } catch (erro) {
        if (n !== pedidosResumo) return;
        console.error('Falha a carregar o resumo:', erro);
        resumoEstado.dados = null;
        resumoEstado.erro = 'Não foi possível carregar o resumo. Tente de novo; se continuar, o detalhe está na consola do agente.';
      }
      resumoEstado.aCarregar = false;
      desenharLateral();
      if (vistaResumo) desenharPainel();
      if (!historico.length) desenharConversa();   // as boas-vindas passam a mostrar o que o Resumo encontrou
    }

    const TIPOS_DOC = { F: 'Faturas (F)', V: 'Faturas-recibo (V)', N: 'Notas de crédito s/ faturas (N)', C: 'Créditos s/ faturas-recibo (C)' };

    // Fonte única dos números da Faturação: todos os visuais passam por aqui. N corrige F e C corrige V (já vêm negativos).
    // A taxa de crédito é sempre sobre o bruto do próprio fluxo, nunca sobre o líquido.
    function faturacaoDe(linhas) {
      const soma = id => linhas.filter(x => x.tipo === id).reduce((t, x) => ({ valor: t.valor + x.valor, documentos: t.documentos + x.documentos }), { valor: 0, documentos: 0 });
      const fluxo = (venda, credito) => ({ bruto: venda.valor, creditos: credito.valor, liquido: venda.valor + credito.valor,
        taxa: venda.valor > 0 ? Math.abs(credito.valor) / venda.valor * 100 : null, documentos: venda.documentos, docCreditos: credito.documentos });
      const [F, V, N, C] = ['F', 'V', 'N', 'C'].map(soma), juntar = (a, b) => ({ valor: a.valor + b.valor, documentos: a.documentos + b.documentos });
      return { fat: fluxo(F, N), rec: fluxo(V, C), total: fluxo(juntar(F, V), juntar(N, C)) };
    }

    // Textos da ferramenta -> números dos blocos. Sem dados para um bloco, o bloco fica vazio (nunca inventa).
    function montarResumo(j) {
      const textoMeses = j.meses || '';
      const linha1 = textoMeses.split('\n')[0].trim();
      // "Cliente … não encontrado", "Vários clientes começam por …" (com a lista id,cliente) ou "Sem vendas: …"
      if (!/^Vendas por/.test(linha1)) {
        const [mensagem, lista = ''] = textoMeses.split(/\r?\nid,cliente\r?\n/);
        const opcoes = lista.split(/\r?\n/).map(l => partir(l.trim(), ',')).filter(c => c.length === 2 && c[0] && !c[0].startsWith('#'));
        return { erro: mensagem.trim(), opcoes };
      }
      const hoje = new Date();
      const num = (txt, re) => { const m = (txt || '').match(re); return m ? paraNumero(m[1]) : null; };
      const facto = (r, re) => r.factos.find(([k]) => re.test(k)) || ['', ''];
      const rm = lerLista(textoMeses), serie = lerSerie(rm);
      const comparado = facto(rm, /^comparado com/i);
      const anoAnt = (comparado[0].match(/(\d{4})$/) || [])[1] || null;
      const total = num(rm.resumo, /total\s+(-?[\d\s.,]+?)\s*€/);
      const delta = num(comparado[1], /\(([+\-−]?[\d,]+)%\)/);
      const datas = linha1.match(/\((\d{2}-\d{2}-\d{4}) a (\d{2}-\d{2}-\d{4})/);
      const nomePeriodo = (RES_PERIODOS.find(p => p[0] === resumoEstado.periodo) || [])[1] || '';
      const meses = serie ? serie.meses : [];
      const agora = x => x.a === hoje.getFullYear() && x.m === hoje.getMonth() + 1;
      const incompleto = meses.find(x => x.atual != null && (x.parcial || agora(x)));
      const rc = j.clientes ? lerLista(j.clientes) : null;
      const ativos = rc ? num(rc.resumo, /em\s+([\d\s]+?)\s+clientes/) : null;
      const positivos = texto => lerLista(texto || '').itens.filter(i => i.num > 0);
      const subida = itens => itens.filter(i => i.delta > 0).sort((a, b) => b.delta - a.delta)[0];
      const descida = itens => itens.filter(i => i.delta < 0).sort((a, b) => a.delta - b.delta)[0];

      // "Um ano antes: 224 clientes (+4,0%), 33 450 documentos (+4,1%), valor médio por documento 57,50 € (+2,1%)": a variação de cada indicador
      const umAnoAntes = r => r ? facto(r, /^um ano antes/i)[1] : '';
      const variacaoDe = (r, antes) => num(umAnoAntes(r), new RegExp(antes + '[^(]*\\(([+\\-−]?[\\d,]+)%\\)'));
      const kpis = [
        { nome: 'Faturação total', valor: total, tipo: 'eur', variacao: delta,
          contexto: delta == null ? 'Sem comparação com o ano anterior.' : `${rDec(Math.abs(delta), 1)}% ${delta >= 0 ? 'acima' : 'abaixo'} do mesmo período de ${anoAnt}.` },
        { nome: 'N.º de documentos', valor: num(rm.resumo, /,\s*([\d\s]+?)\s+documentos/), tipo: 'int', variacao: variacaoDe(rm, 'documentos'), contexto: 'Faturas e talões emitidos no período.' },
        { nome: 'Valor médio por documento', valor: num(facto(rm, /^valor m[ée]dio/i)[1], /^(-?[\d\s.,]+)/), tipo: 'eur2', variacao: variacaoDe(rm, 'por documento'), contexto: 'Valor médio de cada venda.' }
      ];
      if (ativos != null) kpis.push({ nome: 'Clientes ativos', valor: ativos, tipo: 'int', variacao: variacaoDe(rc, 'clientes'), contexto: 'Clientes com compras no período.' });

      // Previsão: tendência linear dos últimos 6 meses completos, 3 meses à frente.
      // shortcut: reta simples + intervalo de ±1,28 desvios (≈80%); trocar por um modelo com sazonalidade se houver vários anos de histórico.
      let previsao = null, semPrevisao = '';
      const completos = meses.filter(x => x.atual != null && !x.parcial && !agora(x));
      if (!['AnoAtual', 'Ultimos12Meses'].includes(resumoEstado.periodo)) semPrevisao = 'A previsão só se calcula para "Este ano" ou "Últimos 12 meses".';
      else if (completos.length < 6) semPrevisao = 'Ainda não há 6 meses completos para prever a faturação.';
      else {
        const ult = completos.slice(-6), ys = ult.map(x => x.atual), my = ys.reduce((s, v) => s + v, 0) / 6;
        const b = ys.reduce((s, y, i) => s + (i - 2.5) * (y - my), 0) / ys.reduce((s, _, i) => s + (i - 2.5) ** 2, 0), a = my - b * 2.5;
        const desvio = Math.sqrt(ys.reduce((s, y, i) => s + (y - (a + b * i)) ** 2, 0) / 4);
        const futuros = [1, 2, 3].map(h => {
          const d = new Date(ult[5].a, ult[5].m - 1 + h, 1), v = Math.max(0, a + b * (5 + h)), e = 1.28 * desvio * Math.sqrt(h);
          return { m: d.getMonth() + 1, v, inf: Math.max(0, v - e), sup: v + e };
        });
        const todos = [...ult, ...futuros];
        previsao = { meses: todos.map(x => MESES[x.m - 1].slice(0, 3)), nomes: todos.map(x => MESES[x.m - 1]), real: ys,
                     previsto: [ys[5], ...futuros.map(f => f.v)], inferior: [ys[5], ...futuros.map(f => f.inf)], superior: [ys[5], ...futuros.map(f => f.sup)] };
      }

      // O painel pede os 100 maiores (lista "Ver artigos"); o resto da página usa só os 10 primeiros
      const artigosTodos = positivos(j.artigos), artigos = artigosTodos.slice(0, 10);
      // "(sem distrito)" fica no mosaico (é faturação real), mas não dá alertas: não se analisa um distrito que não existe
      const distritos = positivos(j.zonas), zonas = distritos.filter(i => !/^\(/.test(i.nome));
      // Clientes da ficha em cada distrito (CSV: distrito,clientes,percentagem; a 1.ª e a 2.ª linha são título e cabeçalho)
      const nClientes = new Map((j.distritosClientes || '').split(/\r?\n/).slice(2).map(l => partir(l.trim(), ','))
        .filter(c => c.length === 3).map(c => [c[0].toLowerCase(), +c[1]]));
      const locais = positivos(j.localidades).filter(i => !/^\(/.test(i.nome));
      // Alertas: só o que os dados dizem. Queda de 50% ou mais é crítica; abaixo disso, aviso. Os limites são os das REGRAS_ALERTA.
      const idAlerta = (regra, nome) => [resumoEstado.periodo, resumoEstado.zona, resumoEstado.cliente, regra, nome].join('|');
      const quebras = (itens, limite) => itens.filter(i => i.delta <= limite).sort((a, b) => a.delta - b.delta);
      const gravidade = d => d <= -50 ? 'Crítico' : 'Aviso';
      const menos = i => `menos ${rPct(Math.abs(i.delta))} do que no mesmo período de ${anoAnt}`;
      const alertas = rm.avisos.map(a => ({ id: idAlerta('dados', a), tipo: 'dados', sev: 'Info', cat: 'Dados', regra: null, ver: 'geral', tag: 'Dados incompletos', titulo: 'Dados incompletos', texto: maiuscula(a), pergunta: '' }));
      const pa = descida(artigos), zonasEmQuebra = quebras(resumoEstado.zona ? zonas.filter(z => z.nome === resumoEstado.zona) : zonas, -20), pz = zonasEmQuebra[0];
      zonasEmQuebra.forEach(z => alertas.push({ id: idAlerta('distrito', z.nome), tipo: 'local', sev: gravidade(z.delta), cat: 'Distritos', regra: 'distrito', ver: 'zonas', zona: z.nome, tag: 'Distrito em quebra',
        titulo: `${nomeProprio(z.nome)} caiu ${rPct(Math.abs(z.delta))}`, texto: `Faturou ${rEur(z.num)}, ${menos(z)}.`, pergunta: `Comparar as vendas do distrito ${nomeProprio(z.nome)} mês a mês com o ano passado` }));
      quebras(artigos, -25).forEach(a => alertas.push({ id: idAlerta('artigo', a.nome), tipo: 'artigo', sev: gravidade(a.delta), cat: 'Artigos', regra: 'artigo', ver: 'artigos', tag: 'Artigo em quebra',
        titulo: `${a.nome} caiu ${rPct(Math.abs(a.delta))}`, texto: `Vendeu ${rEur(a.num)}, ${menos(a)}.`, pergunta: resumoEstado.cliente ? '' : 'Comparar as vendas por artigo com o ano passado' }));
      quebras(positivos(j.clientes).slice(0, 10), -25).forEach(c => alertas.push({ id: idAlerta('cliente', c.nome), tipo: 'cliente', sev: gravidade(c.delta), cat: 'Clientes', regra: 'cliente', ver: 'clientes', cliente: c.codigo || c.nome, tag: 'Cliente em queda',
        titulo: `${nomeProprio(c.nome)} caiu ${rPct(Math.abs(c.delta))}`, texto: `Comprou ${rEur(c.num)}, ${menos(c)}.`, pergunta: `Que artigos comprou o cliente ${nomeProprio(c.nome)} este ano?` }));
      const parados = num(j.semVendas, /^Clientes sem vendas[^:]*:\s*([\d\s]+?)\s+clientes/);
      if (parados) alertas.push({ id: idAlerta('parados', ''), tipo: 'cliente', sev: 'Aviso', cat: 'Clientes', regra: 'parados', ver: 'clientes', tag: 'Clientes sem compras', titulo: `${rGrupos(parados)} clientes sem compras`, texto: `${rGrupos(parados)} clientes não compram há 90 dias.`, pergunta: 'Clientes que não compram há 90 dias?' });

      let acumulado = null;
      if (anoAnt && meses.length > 1) {
        let sa = 0, sb = 0;
        const ultimo = meses.findLastIndex(x => x.atual != null);
        acumulado = { anoAtual: serie.anoAtual, anoAnterior: anoAnt, rotulos: meses.map(x => MESES[x.m - 1][0]), nomes: meses.map(x => MESES[x.m - 1].toLowerCase()),
                      atual: meses.slice(0, ultimo + 1).map(x => sa += x.atual || 0), anterior: meses.map(x => sb += x.anterior || 0) };
      }

      // Topo e painel lateral: média dos meses completos (contra os mesmos meses do ano anterior), diferença em euros e melhor mês
      const mediaDe = arr => arr.reduce((s, v) => s + v, 0) / arr.length;
      const mediaAtual = completos.length ? mediaDe(completos.map(x => x.atual)) : null;
      const mediaMes = completos.length ? {
        valor: mediaAtual,
        variacao: completos.every(x => x.anterior > 0) ? (mediaAtual / mediaDe(completos.map(x => x.anterior)) - 1) * 100 : null,
        de: MESES[completos[0].m - 1], ate: MESES[completos[completos.length - 1].m - 1] } : null;
      const melhor = meses.filter(x => x.atual > 0).reduce((m, x) => !m || x.atual > m.atual ? x : m, null);

      // Vista Faturação (JSON da consultar_documentos). Outro texto = cliente por resolver ou erro: os cartões ficam "Ainda sem dados".
      let docs = null;
      try { docs = JSON.parse(j.documentos || 'null'); } catch { }
      // Vista Clientes (JSON da consultar_carteira): acrescenta zona, última compra, documentos, compras por mês e estado
      let carteira = null;
      try { carteira = JSON.parse(j.carteira || 'null'); } catch { }
      const fichaCarteira = new Map((carteira ? carteira.clientes : []).map(c => [String(c.id).trim(), c]));

      return {
        serie, mediaMes,
        melhorMes: melhor ? { nome: MESES[melhor.m - 1], valor: melhor.atual } : null,
        diferenca: total != null && delta != null ? total - total / (1 + delta / 100) : null,
        // "dados até": a última venda registada, quando o servidor a indica; senão o fim do período
        ate: (textoMeses.match(/[uú]ltima venda registada[^\d]*(\d{2}-\d{2}-\d{4})/) || [])[1] || (datas ? datas[2] : ''), guardado: new Date(),
        cliente: (linha1.match(/cliente "([^"]+)"/) || [])[1] || null,
        periodoTexto: nomePeriodo + (datas ? ` · ${datas[1]} a ${datas[2]}` : ''),
        mesIncompleto: incompleto ? MESES[incompleto.m - 1] : '',
        anoAnt, total, kpis, previsao, semPrevisao, alertas, acumulado,
        artigos, artigosTodos, artigosSobe: subida(artigos), artigosDesce: pa,
        // Cabeçalho da consulta: "total 2 044 643,20 € em 23 280 artigos" (todos os artigos, não só os da lista)
        artigosValor: num(j.artigos, /total\s+(-?[\d\s.  ]+,\d+)\s*€/),
        artigosTotal: +((j.artigos || '').match(/Mostro \d+ de (\d+)/) || [])[1] || null,
        marcas: j.marcas ? positivos(j.marcas).map(i => ({ nome: i.nome, valor: i.num, delta: i.delta, media: i.media, anterior: i.anterior ? paraNumero(i.anterior.valor) : null })) : null,
        marcasTotal: +((j.marcas || '').match(/Mostro \d+ de (\d+)/) || [])[1] || null,
        locais: j.zonas ? distritos.map(i => {
          const sem = /^\(/.test(i.nome), id = sem ? 'sem dados' : i.nome;
          return { id, nome: nomeDistrito(id), sem, fat: i.num, variacao: i.delta, novo: i.novo, clientes: nClientes.get(id.toLowerCase()) };
        }) : null,
        // Tabela das localidades; "(sem localidade)" fica (é faturação real sem ficha completa)
        localidades: j.localidades ? positivos(j.localidades).map(i => ({ nome: i.nome, num: i.num, delta: i.delta, novo: i.novo, semDados: /^\(/.test(i.nome) })) : null,
        localidadesTotal: +((j.localidades || '').match(/Mostro \d+ de (\d+)/) || [])[1] || null,
        localidadesSobe: subida(locais), localidadesDesce: descida(locais),
        locaisDesce: pz,
        clientes: j.clientes ? positivos(j.clientes).slice(0, 10) : null,
        carteira: j.clientes ? positivos(j.clientes).map(c => ({ ...c, ...fichaCarteira.get(c.codigo) })) : null,
        clientesTotal: +((j.clientes || '').match(/Mostro \d+ de (\d+)/) || [])[1] || null,
        tiposDoc: docs ? docs.tipos.map(t => ({ nome: TIPOS_DOC[t.tipo] || 'Tipo ' + t.tipo, valor: t.valor, docs: t.documentos })) : null,
        faturacao: docs ? faturacaoDe(docs.tipos) : null,
        // O servidor manda uma linha por mês e tipo: aqui fica uma por mês
        docsPorMes: docs ? [...new Set(docs.meses.map(x => x.mes))].map(mes => {
          const doMes = docs.meses.filter(x => x.mes === mes), [a, m] = mes.split('-').map(Number), s = meses.find(y => y.a === a && y.m === m);
          return { a, m, n: doMes.reduce((t, x) => t + x.documentos, 0), parcial: !!s && (s.parcial || agora(s)), ...faturacaoDe(doMes) };
        }) : null,
        creditos: docs ? docs.creditos.map(d => ({ doc: d.documento, tipo: d.tipo, cliente: d.cliente, data: d.data, total: d.total })) : null,
        ultimosDocs: docs ? docs.ultimos.map(d => ({ doc: d.documento, cliente: d.cliente, data: d.data, total: d.total })) : null
      };
    }

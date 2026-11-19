    /* =========================================================
       Estado do painel e regras dos filtros e dos Guardados (sem DOM: testadas em tests/pagina)
       ========================================================= */
    // Períodos que o servidor aceita (ApiPainel.Periodos). Os três primeiros são os botões do cabeçalho.
    const PERIODOS = [['MesAtual', 'Mês'], ['TrimestreAtual', 'Trimestre'], ['AnoAtual', 'Ano'],
                      ['Ultimos12Meses', 'Últimos 12 meses'], ['AnoAnterior', 'Ano anterior'], ['Ultimos7Dias', 'Últimos 7 dias']];

    const ISO = /^\d{4}-\d{2}-\d{2}$/;
    const filtros = {
      // Os filtros transversais: valem para todos os separadores e mantêm-se ao mudar de separador.
      // Datas em ISO (aaaa-mm-dd, como o <input type="date">); comparar: 'ano' (mesmo período do ano anterior) ou 'nenhum'.
      vazios: () => ({ de: '', ate: '', comparar: 'ano', cliente: '', zonas: [], marcas: [], tipos: [] }),
      // Etiquetas da linha de filtros ativos: [{ chave, valor, rotulo }]. n: os nomes a mostrar (zona, tipo, data).
      ativos(F, n = {}) {
        const nome = (k, v) => (n[k] || (x => x))(v), a = [];
        if (F.de && F.ate) a.push({ chave: 'datas', valor: '', rotulo: `${nome('data', F.de)} a ${nome('data', F.ate)}` });
        if (F.comparar === 'nenhum') a.push({ chave: 'comparar', valor: '', rotulo: 'Sem comparação' });
        if (F.cliente) a.push({ chave: 'cliente', valor: '', rotulo: 'Cliente: ' + F.cliente });
        (F.zonas || []).forEach(z => a.push({ chave: 'zonas', valor: z, rotulo: nome('zona', z) }));
        (F.marcas || []).forEach(m => a.push({ chave: 'marcas', valor: m, rotulo: nome('marca', m) }));
        (F.tipos || []).forEach(t => a.push({ chave: 'tipos', valor: t, rotulo: nome('tipo', t) }));
        return a;
      },
      remover(F, chave, valor) {
        if (chave === 'datas') return { ...F, de: '', ate: '' };
        if (chave === 'comparar') return { ...F, comparar: 'ano' };
        if (Array.isArray(F[chave])) return { ...F, [chave]: F[chave].filter(x => x !== valor) };
        return { ...F, [chave]: '' };
      },
      // Valores vindos do formulário ou das preferências: sem espaços a mais, sem repetidos, com os limites do servidor
      limpos(F = {}) {
        const t = v => String(v ?? '').replace(/\s+/g, ' ').trim().slice(0, 100);
        const lista = v => [...new Map((Array.isArray(v) ? v : v ? [v] : []).map(t).filter(Boolean).map(x => [x.toLowerCase(), x])).values()].slice(0, 30);
        const de = ISO.test(F.de || '') ? F.de : '', ate = ISO.test(F.ate || '') ? F.ate : '';
        return { de: de && ate ? de : '', ate: de && ate ? ate : '', comparar: F.comparar === 'nenhum' ? 'nenhum' : 'ano', cliente: t(F.cliente),
          zonas: lista(F.zonas ?? F.zona), marcas: lista(F.marcas), tipos: lista(F.tipos).filter(x => /^[A-Za-z0-9]{1,10}$/.test(x)) };
      },
      // '' = datas certas; senão o motivo. hoje em ISO.
      validarDatas(de, ate, hoje) {
        if (!de && !ate) return '';
        if (!ISO.test(de || '') || !ISO.test(ate || '')) return 'Indique as duas datas, "De" e "Até".';
        if (de > ate) return 'A data "De" tem de ser anterior à data "Até".';
        if (de < '2000-01-01' || ate > hoje) return 'As datas têm de estar entre 2000 e hoje.';
        return '';
      },
      // Parâmetros dos pedidos ao servidor (pares, porque zonas, marcas e tipos repetem a chave)
      consulta(periodo, F) {
        const dma = iso => iso.split('-').reverse().join('-');
        const q = [['periodo', PERIODOS.some(p => p[0] === periodo) ? periodo : 'AnoAtual']];
        if (F.de && F.ate) q.push(['de', dma(F.de)], ['ate', dma(F.ate)]);
        if (F.cliente) q.push(['cliente', F.cliente]);
        (F.zonas || []).forEach(z => q.push(['zona', z]));
        (F.marcas || []).forEach(m => q.push(['marca', m]));
        (F.tipos || []).forEach(t => q.push(['tipodoc', t]));
        if (F.comparar === 'nenhum') q.push(['comparar', 'nao']);
        return q;
      }
    };

    // Guardados: vistas (separador, período e filtros) e respostas do Flow (pergunta e texto, voltam a ser desenhadas)
    const guardados = {
      chave: g => g.tipo === 'resposta' ? 'r|' + g.pergunta.toLowerCase() : JSON.stringify(['v', g.separador, g.periodo, filtros.consulta(g.periodo, filtros.limpos(g.filtros))]),
      // Devolve a lista nova (o mais recente primeiro) e se já lá estava; no máximo 50
      adicionar(lista, item) {
        const k = guardados.chave(item), jaExistia = lista.some(g => guardados.chave(g) === k);
        return { lista: jaExistia ? lista : [item, ...lista].slice(0, 50), jaExistia };
      },
      remover: (lista, id) => lista.filter(g => g.id !== id),
      filtrar: (lista, tipo) => tipo === 'todos' ? lista : lista.filter(g => g.tipo === tipo)
    };

    // Regras dos alertas: as que já existiam, agora guardadas por utilizador no servidor (Preferencias)
    // [id, nome, frase(limite), limite por omissão (% de queda; null = os 90 dias vêm do servidor)]
    const REGRAS_ALERTA = [
      ['distrito', 'Quedas de zona', l => `Avisar quando a faturação de uma zona cair mais de ${l}% face ao mesmo período do ano anterior.`, 20],
      ['artigo', 'Artigos em queda', l => `Avisar quando um dos artigos mais vendidos vender mais de ${l}% menos que no ano anterior.`, 25],
      ['cliente', 'Desvios de clientes', l => `Avisar quando um dos maiores clientes comprar mais de ${l}% abaixo do ano anterior.`, 25],
      ['parados', 'Clientes sem compras', () => 'Avisar quando houver clientes sem compras há mais de 90 dias.', null]
    ];
    const LIMITE_MIN = 5, LIMITE_MAX = 90;
    const alertasRegras = {
      limiteValido: v => Number.isInteger(v) && v >= LIMITE_MIN && v <= LIMITE_MAX,
      // prefs: { [id]: { ligada, limite } } guardado pelo utilizador; o resto vem das omissões
      regra(prefs, id) {
        const r = REGRAS_ALERTA.find(x => x[0] === id), p = (prefs || {})[id] || {};
        if (!r) return null;
        return { id, nome: r[1], ligada: p.ligada !== false, limite: r[3] == null ? null : alertasRegras.limiteValido(p.limite) ? p.limite : r[3], frase: r[2] };
      },
      // Um alerta detetado conta se a regra está ligada e a queda passa o limite
      conta(prefs, alerta) {
        if (!alerta.regra) return true;
        const r = alertasRegras.regra(prefs, alerta.regra);
        return !!r && r.ligada && (alerta.delta == null || r.limite == null || alerta.delta <= -r.limite);
      }
    };

    // Alertas personalizados (Alertas > Alertas personalizados): o utilizador escolhe o que vigiar e o limite; a página avalia-os
    // com os dados do painel (os mesmos do período e dos filtros ativos). Guardados por utilizador (prefs.alertasPers).
    const FREQ_ALERTA = ['Assim que acontecer', 'Uma vez por dia', 'Uma vez por semana'];
    const EMAIL_DA_EMPRESA = /^[^\s@<>]+@inforap\.pt$/i;   // como o Agendar envio: os dados de vendas só saem para endereços da empresa
    const MODELOS_ALERTA = [
      { tipo: 'zona', nome: 'Queda numa zona', un: '% de queda', min: 1, max: 100, omissao: 30, frase: (l, z) => `Avisar quando a faturação ${z ? 'da zona ' + z : 'de uma zona'} cair mais de ${l}% face ao mesmo período do ano anterior.` },
      { tipo: 'sem', nome: 'Cliente sem compras', un: 'dias', min: 1, max: 3650, omissao: 60, frase: l => `Avisar quando um dos maiores clientes ficar mais de ${l} dias sem comprar.` },
      { tipo: 'desvio', nome: 'Cliente com desvio', un: '% abaixo do ano anterior', min: 1, max: 100, omissao: 25, frase: l => `Avisar quando um dos maiores clientes comprar mais de ${l}% abaixo do ano anterior.` },
      { tipo: 'artigo', nome: 'Artigo em queda', un: '% de queda', min: 1, max: 100, omissao: 15, frase: l => `Avisar quando um artigo vender mais de ${l}% menos que no ano anterior.` },
      { tipo: 'fatura', nome: 'Fatura em atraso', un: 'dias', min: 1, max: 3650, omissao: 60, frase: l => `Avisar quando uma fatura ficar mais de ${l} dias por pagar.` },
      { tipo: 'novo', nome: 'Cliente novo de valor', un: '€ no período', min: 1, max: 10000000, omissao: 5000, frase: l => `Avisar quando um cliente novo faturar mais de ${fmt.eur(l)} no período.` }
    ];
    const alertasPers = {
      modelo: tipo => MODELOS_ALERTA.find(m => m.tipo === tipo),
      // nomeZona: o nome legível da zona (o que se guarda é o valor da ficha)
      frase(a, nomeZona = z => z) { const m = alertasPers.modelo(a.tipo); return m ? m.frase(a.limite, a.zona ? nomeZona(a.zona) : '') : ''; },
      // Valida o que vem do formulário: { alerta } ou { erro, campo }. Editar (com id) não conta para o máximo de 20.
      limpar(F, existentes = []) {
        const t = v => String(v ?? '').replace(/\s+/g, ' ').trim(), m = alertasPers.modelo(F.tipo), nome = t(F.nome), limite = Number(F.limite);
        if (!m) return { erro: 'Escolha o que vigiar.', campo: 'tipo' };
        if (!nome || nome.length > 60) return { erro: 'Escreva um nome com até 60 letras.', campo: 'nome' };
        if (!Number.isInteger(limite) || limite < m.min || limite > m.max) return { erro: `O limite tem de ser um número inteiro entre ${m.min} e ${m.max}.`, campo: 'limite' };
        const email = F.mail ? t(F.email) : '';
        if (F.mail && !EMAIL_DA_EMPRESA.test(email)) return { erro: 'Só endereços @inforap.pt.', campo: 'email' };
        if (!F.id && existentes.length >= 20) return { erro: 'Pode ter no máximo 20 alertas.', campo: 'nome' };
        return { alerta: { id: F.id || 'a' + Date.now().toString(36), nome, tipo: m.tipo, limite, zona: m.tipo === 'zona' ? t(F.zona).slice(0, 100) : '',
          freq: FREQ_ALERTA.includes(F.freq) ? F.freq : FREQ_ALERTA[1], mail: !!F.mail, email, ligada: F.ligada !== false, ultimo: F.ultimo || '' } };
      },
      // O que o alerta encontra nos dados de agora: uma entrada por zona, cliente ou artigo (no máximo 10). atrasos: resposta de /api/atrasos.
      avaliar(a, D, atrasos) {
        if (!D || D.erro) return [];
        const L = a.limite, id = k => `pers|${a.id}|${k}`, np = typeof nomeProprio === 'function' ? nomeProprio : x => x;
        const dias = (de, ate) => { const x = fmt.lerData(de), y = fmt.lerData(ate); return x && y ? Math.round((Date.UTC(y.a, y.m - 1, y.d) - Date.UTC(x.a, x.m - 1, x.d)) / 864e5) : null; };
        const ref = D.anoAnt ? `que em ${D.anoAnt}` : 'que no ano anterior', cli = (c, titulo, texto) => ({ id: id(c.codigo), tipo: 'clientes', icone: 'semClientes', cliente: c.codigo, nome: c.nome, titulo, texto, pergunta: `O que mudou nas compras do cliente ${np(c.nome)} este ano?` });
        let r = [];
        if (a.tipo === 'zona') r = (D.zonas || []).filter(z => !z.sem && z.variacao != null && z.variacao <= -L && (!a.zona || z.id.toLowerCase() === a.zona.toLowerCase()))
          .map(z => ({ id: id(z.id), tipo: 'zonas', icone: 'descida', titulo: `${z.nome} caiu ${fmt.pct(Math.abs(z.variacao))}`, texto: `Faturou ${fmt.eur(z.fat)}, menos ${fmt.pct(Math.abs(z.variacao))} ${ref}.`, pergunta: `Comparar as vendas do distrito ${z.nome} mês a mês com o ano passado` }));
        else if (a.tipo === 'sem') r = (D.carteira || []).filter(c => c.ultima && dias(c.ultima, D.ate) > L)
          .map(c => cli(c, `${np(c.nome)} sem compras`, `Última compra a ${fmt.dataLonga(c.ultima, true)}, há ${fmt.inteiro(dias(c.ultima, D.ate))} dias.`));
        else if (a.tipo === 'desvio') r = (D.carteira || []).filter(c => c.delta != null && c.delta <= -L)
          .map(c => ({ ...cli(c, `${np(c.nome)} com desvio`, `Comprou ${fmt.eur(c.num)}, menos ${fmt.pct(Math.abs(c.delta))} ${ref}.`), icone: 'descida' }));
        else if (a.tipo === 'novo') r = (D.carteira || []).filter(c => c.novo && c.num > L)
          .map(c => ({ ...cli(c, `${np(c.nome)} é cliente novo`, `Já faturou ${fmt.eur(c.num)} no período, sem compras no ano anterior.`), icone: 'alertas' }));
        else if (a.tipo === 'artigo') r = (D.artigosTodos || []).filter(x => x.delta != null && x.delta <= -L)
          .map(x => ({ id: id(x.codigo || x.nome), tipo: 'artigos', icone: 'descida', titulo: `${x.nome} caiu ${fmt.pct(Math.abs(x.delta))}`, texto: `Vendeu ${fmt.eur(x.num)}, menos ${fmt.pct(Math.abs(x.delta))} ${ref}.`, pergunta: 'Comparar as vendas por artigo com o ano passado' }));
        else if (a.tipo === 'fatura') {
          const itens = ((atrasos && atrasos.itens) || []).filter(i => i.dias > L), total = itens.reduce((s, i) => s + i.valor, 0);
          if (itens.length) r = [{ id: id('faturas'), tipo: 'clientes', icone: 'alertas', titulo: `${fmt.inteiro(itens.length)} ${itens.length > 1 ? 'faturas' : 'fatura'} há mais de ${L} dias por pagar`,
            texto: `Total ${fmt.eur(total)} com IVA${atrasos.truncado ? ' (as mais antigas)' : ''}. A mais antiga tem ${fmt.inteiro(Math.max(...itens.map(i => i.dias)))} dias de atraso.`, pergunta: 'Que faturas estão vencidas e por pagar?' }];
        }
        return r.slice(0, 10).map(x => ({ ...x, alertaId: a.id }));
      },
      // "Último aviso": o dia (aaaa-mm-dd) em que o alerta encontrou algo. Só grava uma vez por dia; devolve se mudou algum.
      marcarUltimos(lista, D, atrasos, hoje) {
        let mudou = false;
        const nova = lista.map(a => {
          if (!a.ligada || a.ultimo === hoje || !alertasPers.avaliar(a, D, atrasos).length) return a;
          mudou = true;
          return { ...a, ultimo: hoje };
        });
        return { lista: nova, mudou };
      }
    };

    // Listas longas: os primeiros (topo) e, com "Ver todos", páginas de 15. Nunca as 100 linhas de uma vez.
    const LISTA = { topo: 10, porPagina: 15 };
    const listas = {
      // pag começa em 0; uma página a mais fica na última
      paginar(lista, pag, porPagina = LISTA.porPagina) {
        const total = lista.length, ultima = Math.max(0, Math.ceil(total / porPagina) - 1), p = Math.min(Math.max(0, pag | 0), ultima);
        const itens = lista.slice(p * porPagina, (p + 1) * porPagina);
        return { itens, pag: p, ultima, total, de: total ? p * porPagina + 1 : 0, ate: p * porPagina + itens.length };
      }
    };

    // O ecrã de entrada só se salta quando não há nada a perguntar (vendedor), quando se pediu um separador ou documento
    // (#clientes), ou ao recarregar o painel. Abrir o Flow de novo mostra sempre a entrada, mesmo na mesma janela.
    const arranque = {
      saltarEntrada: ({ vendedor, ancoraValida, navegacao, marca }) => !!vendedor || !!ancoraValida || (navegacao === 'reload' && !!marca)
    };

    if (typeof module === 'object') module.exports = { PERIODOS, filtros, guardados, alertasRegras, REGRAS_ALERTA, listas, arranque, LISTA, alertasPers, MODELOS_ALERTA, FREQ_ALERTA };

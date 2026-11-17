    /* =========================================================
       Estado do painel e regras dos filtros e dos Guardados (sem DOM: testadas em tests/pagina)
       ========================================================= */
    // Períodos que o servidor aceita (ApiPainel.Periodos). Os três primeiros são os botões do cabeçalho.
    const PERIODOS = [['MesAtual', 'Mês'], ['TrimestreAtual', 'Trimestre'], ['AnoAtual', 'Ano'],
                      ['Ultimos12Meses', 'Últimos 12 meses'], ['AnoAnterior', 'Ano anterior'], ['Ultimos7Dias', 'Últimos 7 dias']];

    const filtros = {
      // Os filtros transversais: valem para todos os separadores e mantêm-se ao mudar de separador.
      vazios: () => ({ cliente: '', zona: '' }),
      // Etiquetas da linha de filtros ativos: [{ chave, rotulo }]
      ativos(F, nomeZona = z => z) {
        const a = [];
        if (F.zona) a.push({ chave: 'zona', rotulo: 'Zona: ' + nomeZona(F.zona) });
        if (F.cliente) a.push({ chave: 'cliente', rotulo: 'Cliente: ' + F.cliente });
        return a;
      },
      remover: (F, chave) => ({ ...F, [chave]: '' }),
      // Valores vindos do formulário: sem espaços a mais, com tamanho máximo (o servidor recusa mais de 100)
      limpos(F) {
        const t = v => String(v ?? '').replace(/\s+/g, ' ').trim().slice(0, 100);
        return { cliente: t(F.cliente), zona: t(F.zona) };
      },
      // Parâmetros dos pedidos ao servidor: o período e os filtros, para os indicadores e as tabelas
      consulta(periodo, F) {
        const q = { periodo: PERIODOS.some(p => p[0] === periodo) ? periodo : 'AnoAtual' };
        if (F.cliente) q.cliente = F.cliente;
        if (F.zona) q.zona = F.zona;
        return q;
      },
      iguais: (a, b) => (a.cliente || '') === (b.cliente || '') && (a.zona || '') === (b.zona || '')
    };

    // Guardados: vistas (separador, período e filtros) e respostas do Flow (pergunta e texto, voltam a ser desenhadas)
    const guardados = {
      chave: g => g.tipo === 'resposta' ? 'r|' + g.pergunta.toLowerCase() : JSON.stringify(['v', g.separador, g.periodo, g.filtros?.cliente || '', g.filtros?.zona || '']),
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

    if (typeof module === 'object') module.exports = { PERIODOS, filtros, guardados, alertasRegras, REGRAS_ALERTA };

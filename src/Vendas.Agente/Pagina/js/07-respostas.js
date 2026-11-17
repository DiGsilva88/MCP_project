    /* =========================================================
       Respostas do Flow: o texto que as ferramentas escrevem é lido (05-leitura-respostas.js) e
       desenhado com os componentes partilhados. Nada é inventado: sem número no texto, não há número no ecrã.
       ========================================================= */

    // Lista em que todas as linhas são meses "AAAA-MM" -> série para o gráfico
    function lerSerie(r) {
      if (r.itens.length < 2 || r.itens.some(i => i.unidades) || !r.itens.every(i => /^\d{4}-\d{2}$/.test(i.nome))) return null;
      const meses = r.itens.map(i => {
        const [a, m] = i.nome.split('-').map(Number);
        return { a, m, atual: paraNumero(i.valor), anterior: i.anterior ? paraNumero(i.anterior.valor) : null, delta: i.delta, anoAnt: i.anterior ? i.anterior.ano : null, media: i.media };
      });
      // O mês da última venda registada (aviso "Atenção") está incompleto; um período a começar a meio do mês também
      const ultima = r.avisos.join(' ').match(/(\d{2})-(\d{2})-(\d{4})/);
      meses.forEach(x => { x.parcial = !!ultima && x.atual != null && x.a === +ultima[3] && x.m === +ultima[2]; });
      if (r.periodo && !r.periodo.startsWith('01-') && meses[0].atual != null) meses[0].parcial = true;
      return { meses, anoAtual: meses[0].a, anoAnt: (meses.find(x => x.anoAnt) || {}).anoAnt, ultima: ultima ? ultima[0] : '' };
    }
    // "Ticket médio por mês": a série passa a ser o valor médio por documento
    function serieTicketMedio(s) {
      if (!s || !s.meses.every(x => x.atual == null || x.media)) return null;
      return { ...s, medida: true, anoAnt: null, meses: s.meses.map(x => ({ ...x, total: x.atual, atual: x.media ? paraNumero(x.media) : null, anterior: null, delta: null })) };
    }

    // "Nenhuma correspondência. … Localidade: FERNAO FERRO (41); …" ou "Não foi possível encontrar dados para a actividade "X". Os valores …"
    function lerSemResultado(texto) {
      const naoEncontrou = texto.match(/n[ãa]o foi poss[íi]vel encontrar[^"“«]*?\bpara\s+(?:a|o)\s+([\p{L} ]+?)\s+["“«]([^"”»]+)["”»]/iu);
      if (naoEncontrou) {
        const resto = texto.slice(naoEncontrou.index + naoEncontrou[0].length).replace(/^[^:]*:/, '');
        const opcoes = resto.split(';').map(p => p.trim().match(/^(.+?)\s*\((\d+)\)\s*\.?$/)).filter(Boolean).map(x => [x[1], +x[2]]);
        if (opcoes.length) return { campo: naoEncontrou[1].trim(), errado: naoEncontrou[2].trim(), opcoes };
      }
      if (!/nenhuma correspond[êe]ncia/i.test(texto)) return null;
      const m = texto.match(/([A-Za-zÀ-ú][A-Za-zÀ-ú ]*?):\s*[^:()]+\(\d+\)/);
      if (!m) return null;
      const resto = texto.slice(m.index + m[1].length + 1);
      return { campo: m[1].trim(), opcoes: [...resto.matchAll(/([^;()]+?)\s*\((\d+)\)/g)].map(x => [x[1].trim(), +x[2]]) };
    }
    // "Sem vendas: Trimestre atual (01-10-2026 a 29-10-2026)" + "Atenção: …"
    function lerSemVendas(texto) {
      const linhas = texto.split('\n').map(l => l.trim()).filter(Boolean);
      const m = (linhas[0] || '').match(/^sem vendas\b(?:\s+registadas)?[\s:,-]*(?:n[oa]s?\s+|em\s+)?(.*?)\.?$/i);
      if (!m || linhas.length > 3) return null;
      const aviso = linhas.find(l => /^aten[çc][ãa]o\s*:/i.test(l));
      return { periodo: m[1], aviso: aviso ? aviso.replace(/^aten[çc][ãa]o\s*:\s*/i, '') : '' };
    }

    // Nomes de coluna da ficha em português corrente
    const CAMPOS = { nomecliente: 'Nome do cliente', contribuinte: 'NIF', nif: 'NIF', codcliente: 'Código do cliente', email: 'Email',
                     telefone: 'Telefone', telemovel: 'Telemóvel', localidade: 'Localidade', vendedor: 'Vendedor', zona: 'Zona', distrito: 'Distrito', tipocliente: 'Tipo de cliente',
                     actividade: 'Atividade', cobranca: 'Cobrança', expedicao: 'Expedição', situacaofinanceira: 'Situação financeira', pagamento: 'Pagamento',
                     escalaoplafond: 'Escalão de plafond', escalaovolumevendas: 'Escalão de volume de vendas' };
    const rotuloCampo = k => /\s/.test(k) ? k : CAMPOS[semAcento(k).replace(/[^a-z]/g, '')] || maiuscula(k.replace(/(\p{Ll})(\p{Lu})/gu, '$1 $2').toLowerCase());

    // "Clientes com Vendedor = Paulo Afonso e Localidade = Corroios" -> título "Clientes" + filtros
    function lerFiltros(titulo) {
      const f = [...(titulo || '').matchAll(/(\p{L}[\p{L} ]*?)\s*=\s*(.+?)(?=\s+e\s+\p{L}[\p{L} ]*?\s*=|$)/gu)].map(m => [m[1].replace(/^(?:.*\bcom\s+|e\s+)/i, ''), m[2].trim()]);
      return { titulo: f.length ? titulo.replace(/\s+com\s+.*=.*$/i, '') : titulo, filtros: f };
    }
    // "Qual foi a localidade que mais faturou este ano?" + "no mês passado" -> "... faturou no mês passado?"
    function trocarPeriodo(pergunta, periodo) {
      const sem = pergunta.replace(/\?\s*$/, '')
        .replace(/\s+(?:(?:n?este|deste)\s+ano|(?:no|do|o)\s+ano\s+(?:atual|passado)|(?:no|do|n?este|deste)\s+m[eê]s(?:\s+(?:passado|atual))?|(?:no|do|neste)\s+[uú]ltimo\s+trimestre|(?:no|do)\s+1\.º\s+semestre|(?:no|do)\s+dia\s+\d{2}-\d{2}-\d{4}|(?:em|de)\s+\d{4})(?=\s|$)/i, '').replace(/\s{2,}/g, ' ').trim();
      return `${sem} ${periodo}?`;
    }

    // Perguntas seguintes, a partir da própria resposta: só o que as ferramentas respondem
    const FEMININO = /^(zona|localidade|marca|fam[ií]lia|a?c?tividade|categoria|regi[ãa]o)/i;
    const dimensao = titulo => ((titulo || '').match(/\bpor\s+(\S+)/i) || [])[1]
      || (((titulo || '').match(/\b(marca|artigo|fam[ií]lia|zona|localidade|cliente|vendedor)s?\b/i) || [])[1] || '').toLowerCase();
    const limparDim = dim => (dim || '').replace(/[^\p{L}]/gu, '').toLowerCase();
    const daDim = (dim, nome) => limparDim(dim) ? `${FEMININO.test(limparDim(dim)) ? 'da' : 'do'} ${limparDim(dim)} ${nome}` : `de ${nome}`;
    const naDim = (dim, nome) => limparDim(dim) ? `${FEMININO.test(limparDim(dim)) ? 'na' : 'no'} ${limparDim(dim)} ${nome}` : `em ${nome}`;
    const filtravel = dim => /^(zona|localidade|distrito|vendedor|a?c?tividade|cliente)/.test(limparDim(dim));
    function perguntasDoItem(nome, dim, quando = 'este ano') {
      if (/^\(/.test(nome) || /^(m[eê]s|trimestre)/.test(limparDim(dim))) return [];
      if (!filtravel(dim)) {
        if (!/artigo|marca|fam[ií]lia/i.test(dim)) return [];
        const d = limparDim(dim) === 'familia' ? 'família' : limparDim(dim), [o, s] = FEMININO.test(d) ? ['a', 'as'] : ['o', 'os'];
        return [`Qual foi ${o} ${d} mais vendid${o} em cada distrito?`, `Comparar as vendas por ${d} com o ano passado`, `Quais ${d === 'família' ? 'as famílias' : `${s} ${d}s`} que venderam menos este ano?`];
      }
      const n = nomeProprio(nome);
      return [`Evolução mensal das vendas ${daDim(dim, n)} ${quando}`, `Comparar as vendas ${daDim(dim, n)} com o ano passado`, `Que artigos venderam mais ${naDim(dim, n)} ${quando}?`];
    }
    function perguntasSeguintes(r, itens, serie) {
      if (serie) {
        const completos = serie.meses.filter(x => x.atual != null && !x.parcial);
        if (completos.length < 2) return [];
        const melhor = completos.reduce((a, b) => b.atual > a.atual ? b : a), pior = completos.reduce((a, b) => b.atual < a.atual ? b : a);
        const mes = x => MESES[x.m - 1].toLowerCase() + ' de ' + x.a;
        return [`Que artigos venderam mais em ${mes(melhor)}?`, `Que clientes compraram mais em ${mes(melhor)}?`, `Vendas por zona em ${mes(pior)}`];
      }
      const validos = itens.filter(i => i.num > 0 && !/^\(/.test(i.nome));
      if (validos.length < 2) return [];
      const dim = dimensao(r.titulo || r.paragrafos.join(' ')), n = i => nomeProprio(i.nome);
      const primeiro = validos.reduce((a, b) => b.num > a.num ? b : a), ano = +((r.periodo.match(/(\d{4})$/) || [])[1] || new Date().getFullYear());
      const comVariacao = validos.filter(i => i.delta != null);
      if (comVariacao.length < 2 || !filtravel(dim)) return perguntasDoItem(primeiro.nome, dim, `em ${ano}`);
      const sobe = comVariacao.reduce((a, b) => b.delta > a.delta ? b : a), desce = comVariacao.reduce((a, b) => b.delta < a.delta ? b : a), lista = [];
      if (sobe.delta > 0) lista.push(`Que artigos venderam mais ${naDim(dim, n(sobe))} face ao ano passado?`);
      if (desce.delta < 0) lista.push(`Comparar as vendas ${daDim(dim, n(desce))} mês a mês com o ano passado`);
      lista.push(`Evolução mensal das vendas ${daDim(dim, n(primeiro))} em ${ano}`);
      return lista;
    }

    // Destaque em texto, calculado dos números: quem lidera, quem mais subiu e quem mais desceu
    const nomeECodigo = i => nomeProprio(i.nome) + (i.codigo ? ` (${i.codigo})` : '');
    function fraseDestaque(itens) {
      const comValor = itens.filter(i => i.num > 0);
      if (comValor.length < 3 || itens.some(i => i.sub)) return '';
      const primeiro = comValor.reduce((a, b) => b.num > a.num ? b : a);
      let frase = `${nomeECodigo(primeiro)} lidera com ${primeiro.valor}` + (/%/.test(primeiro.pct) ? ` (${primeiro.pct} do total)` : '') + '.';
      const comVariacao = comValor.filter(i => i.delta != null);
      if (comVariacao.length > 1) {
        const sobe = comVariacao.reduce((a, b) => b.delta > a.delta ? b : a), desce = comVariacao.reduce((a, b) => b.delta < a.delta ? b : a);
        if (sobe.delta > 0) frase += ` A maior subida é de ${nomeECodigo(sobe)} (${fmt.variacao(sobe.delta).texto}).`;
        if (desce.delta < 0) frase += ` A maior descida é de ${nomeECodigo(desce)} (${fmt.variacao(desce.delta).texto}).`;
      }
      return frase;
    }

    // Resumo e factos -> métricas ("Total", "Valor médio", "Melhor mês") e ficha (os factos em texto)
    function metricasDe(r) {
      const metricas = [], ficha = [], textos = [];
      if (r.resumo) {
        const [principal, ...resto] = r.resumo.split(/\.\s+(?=\S)/);
        const m = semPonto(principal).match(/^(?:total\s+)?(-?\d[\d\s.  ]*(?:,\d+)?\s*€)\s*(.*)$/i);
        if (m) metricas.push({ r: 'Total', v: fmt.eur(paraNumero(m[1])), nota: maiuscula(m[2].replace(/^em\s+/i, '')) });
        else if (/^\d/.test(principal)) metricas.push({ r: 'Total', v: semPonto(principal) });
        else textos.push(principal);   // não é um total: é uma frase da resposta ("últimos 90 dias (…)")
        // O que vem depois da 1.ª frase também é resposta ("Todos os clientes … compraram neste período."); só a nota dos valores é ruído
        textos.push(...resto.filter(f => !/^valores em euros/i.test(f)));
      }
      const curto = v => /^[-−+]?[\d\s.,  ]+\s*(€|%|\p{L}+)?$/u.test(v);
      for (const [k, v] of r.factos) {
        if (/^lista completa/i.test(k)) continue;
        const c = k.match(/^comparado com\s.*?(\d{4})$/i), p = v.match(/\(([+\-−]?[\d.,]+)\s*%\)/);
        if (c && p && metricas[0]?.r === 'Total') { metricas[0].delta = fmt.numero(p[1]); metricas[0].ano = c[1]; continue; }
        const mes = v.match(/^(\d{4})-(\d{2})\s+[–-]\s+(.+)$/);
        if (mes) { metricas.push({ r: k, v: MESES[+mes[2] - 1], nota: /unidades/.test(mes[3]) ? mes[3] : fmt.eur(paraNumero(mes[3])), nome: true }); continue; }
        if (!curto(v) || /^\d{6,}$/.test(v)) { ficha.push([k, v]); continue; }
        metricas.push({ r: k, v });
      }
      return { metricas, ficha, textos: textos.map(semPonto).filter(Boolean).map(t => maiuscula(t) + '.') };
    }

    // A resposta inteira -> { titulo, paragrafos, metricas, avisos, opcoes, painel (HTML da vista completa ou null), linhas, sugestoes }
    function analisarResposta(texto, pergunta = '') {
      texto = texto.split('\n').map(limparMarkdown).join('\n');
      const base = { titulo: '', paragrafos: [], metricas: [], avisos: [], opcoes: [], painel: null, linhas: null, sugestoes: [], nota: '' };

      const sr = lerSemResultado(texto);
      if (sr) {
        const palavras = pergunta.split(/\s+/).filter(p => p.length > 3);
        let errada = sr.errado && pergunta.includes(sr.errado) ? sr.errado : palavras[palavras.length - 1] || '';
        const parecidas = sr.opcoes.filter(o => palavras.some(p => distancia(semAcento(p), semAcento(o[0])) <= 2 && (errada = p)));
        const lista = [...parecidas, ...sr.opcoes.filter(o => !parecidas.includes(o) && !/^sem dados$/i.test(o[0]))].slice(0, 12);
        return { ...base, titulo: `Não encontrei esse valor de ${sr.campo.toLowerCase()}`, paragrafos: ['Escolha um dos valores existentes. A pergunta corrigida fica na caixa, para rever antes de enviar.'],
          opcoes: lista.map(([nome, n]) => ({ t: nomeProprio(nome), n, escrever: errada ? pergunta.replace(errada, nomeProprio(nome)) : pergunta + ' ' + nomeProprio(nome) })),
          linhas: [[maiuscula(sr.campo), 'Clientes'], ...sr.opcoes.map(([nome, n]) => [nomeProprio(nome), n])] };
      }
      const sv = lerSemVendas(texto);
      if (sv) {
        return { ...base, titulo: 'Sem vendas neste período', paragrafos: sv.periodo ? [maiuscula(sv.periodo) + '.'] : [], avisos: sv.aviso ? [sv.aviso] : [],
          opcoes: [['Este ano', 'este ano'], ['Ano passado', 'no ano passado']].map(([t, p]) => ({ t, perguntar: trocarPeriodo(pergunta, p) })) };
      }

      const t = lerTabela(texto);
      if (t) {
        const antes = t.antes.split('\n').map(l => l.replace(MARCADOR, '').trim()).filter(Boolean).join(' ').replace(/:\s*$/, '');
        if (t.linhas.length === 1) {
          const pares = t.cabecalho.map((k, c) => [rotuloCampo(k), t.linhas[0][c]]);
          return { ...base, titulo: maiuscula(antes) || 'Ficha', paragrafos: t.depois ? [t.depois] : [], linhas: [['Campo', 'Valor'], ...pares],
            painel: painel({ corpo: `<dl class="ficha">${pares.map(([k, v]) => `<dt>${esc(k)}</dt><dd>${esc(v)}</dd>`).join('')}</dl>` }) };
        }
        const colunas = t.cabecalho.map((h, c) => ({ t: rotuloCampo(h), dir: t.numericas[c] }));
        return { ...base, titulo: maiuscula(antes) || 'Resultado', paragrafos: [`${fmt.inteiro(t.linhas.length)} linhas.`, ...(t.depois ? [t.depois] : [])],
          linhas: [t.cabecalho.map(rotuloCampo), ...t.linhas],
          painel: painel({ corpo: tabela({ colunas, linhas: t.linhas.map(l => ({ c: l.map(v => esc(nomeProprioSe(v))) })) }) }) };
      }

      const r = lerLista(texto);
      if (!r.titulo && !r.itens.length && !r.factos.length && !r.avisos.length) return { ...base, paragrafos: [texto.trim()] };

      const serieBase = lerSerie(r), pedeTicket = /ticket|valor medio|media por (documento|fatura|venda)/.test(semAcento(pergunta));
      const serie = (pedeTicket && serieTicketMedio(serieBase)) || serieBase;
      const leitura = lerFiltros(r.titulo);
      const titulo = serie?.medida ? 'Ticket médio por mês' : maiuscula(leitura.titulo) || 'Resposta';
      const { metricas, ficha, textos } = metricasDe(r);
      const devolucoes = r.itens.filter(i => i.num < 0), itens = r.itens.filter(i => !(i.num < 0));
      const destaque = serie ? '' : fraseDestaque(itens);
      const sub = [r.periodo ? 'Período: ' + r.periodo : '', ...leitura.filtros.map(([k, v]) => `${k}: ${v}`)].filter(Boolean).join('. ');
      const paragrafos = [destaque, ...textos, ...r.paragrafos].filter(Boolean);

      // Vista completa no painel principal
      let corpo = '';
      if (metricas.length) corpo += indicadores(metricas.slice(0, 4).map(m => ({ valor: esc(m.v), rotulo: m.r, nome: !!m.nome,
        nota: m.delta != null ? faceA(m.delta, m.ano) : esc(m.nota || '') })));
      if (ficha.length) corpo += painel({ corpo: `<dl class="ficha">${ficha.map(([k, v]) => `<dt>${esc(rotuloCampo(k))}</dt><dd>${esc(v)}</dd>`).join('')}</dl>` });
      if (serie) {
        const ms = serie.meses;
        corpo += painel({ titulo: serie.medida ? 'Ticket médio por mês' : 'Vendas por mês',
          extra: `<div class="legenda"><span><i></i>${serie.anoAtual}</span>${serie.anoAnt ? `<span><i class="ant"></i>${esc(serie.anoAnt)}</span>` : ''}</div>`,
          corpo: graficoMeses({ meses: ms, ultimaData: serie.ultima, interativo: false, rotulo: titulo }) });
        corpo += painel({ titulo: 'Detalhe por mês', corpo: tabela({
          colunas: [{ t: 'Mês' }, { t: serie.medida ? 'Ticket médio' : String(serie.anoAtual), dir: true }, ...(serie.anoAnt ? [{ t: serie.anoAnt, dir: true }, { t: 'Variação', dir: true }] : [])],
          linhas: ms.filter(x => x.atual != null).map(x => ({ c: [`${MESES[x.m - 1]} ${x.a}${x.parcial ? ' (incompleto)' : ''}`, serie.medida ? fmt.eur2(x.atual) : fmt.eur(x.atual),
            ...(serie.anoAnt ? [fmt.eur(x.anterior), x.parcial ? '<span class="muted">incompleto</span>' : variacaoHtml(x.delta)] : [])] })) }) });
      } else if (itens.length > 1 || (itens.length === 1 && !metricas.length)) {
        const comNum = itens.every(i => i.num > 0 && !i.rotulo), max = Math.max(...itens.map(i => i.num || 0), 1), comVar = itens.some(i => i.delta != null || i.novo);
        corpo += painel({ titulo: esc(titulo), corpo: tabela({
          colunas: [{ t: '#', oculto: 'Posição' }, { t: 'Nome' }, ...(itens.some(i => i.sub) ? [{ t: maiuscula(((r.titulo.match(/\bmelhor(?:es)?\s+(\S+)/i) || [])[1]) || 'Detalhe') }] : []),
            { t: itens.some(i => i.rotulo) ? 'Data' : 'Valor', dir: true }, ...(comNum ? [{ t: '', oculto: 'Proporção', largura: '22%' }] : []), ...(comVar ? [{ t: 'Variação', dir: true }] : [])],
          linhas: itens.map(i => ({ c: [esc(i.pos), `${esc(nomeProprio(i.nome) || '—')}${i.codigo ? `<span class="celula-sub">${esc(i.codigo)}</span>` : ''}${i.grupo ? `<span class="celula-sub">${esc(i.grupo)}</span>` : ''}`,
            ...(itens.some(x => x.sub) ? [esc(i.sub)] : []), `${esc(i.rotulo ? i.rotulo + ' ' : '')}${esc(i.valor)}${i.pct ? `<span class="celula-sub">${esc(i.pct)}</span>` : ''}`,
            ...(comNum ? [barra(i.num / max * 100)] : []), ...(comVar ? [i.novo ? `<span class="muted">Sem vendas em ${esc(i.novo)}</span>` : variacaoHtml(i.delta)] : [])] })) }) });
      }
      if (devolucoes.length) corpo += painel({ titulo: 'Devoluções e notas de crédito', corpo: tabela({ colunas: [{ t: 'Nome' }, { t: 'Valor', dir: true }],
        linhas: devolucoes.map(i => ({ c: [esc(nomeProprio(i.nome)), esc(i.valor)] })) }) });

      const linhas = !r.itens.length ? [['Indicador', 'Valor', 'Detalhe'], ...metricas.map(m => [m.r, m.v, m.nota || '']), ...ficha.map(([k, v]) => [rotuloCampo(k), v, ''])]
        : serie ? [['Mês', serie.anoAtual, serie.anoAnt || '', 'Variação (%)'], ...serie.meses.map(x => [`${MESES[x.m - 1]} ${x.a}`, x.atual, x.anterior, x.delta])]
        : [['Posição', 'Nome', 'Código', 'Valor', '%', 'Variação (%)'], ...r.itens.map(i => [i.pos, i.nome, i.codigo, i.valor, i.pct, i.delta])];
      return { ...base, titulo, sub, paragrafos, nota: r.nota, metricas: metricas.slice(0, 4), avisos: r.avisos, linhas,
        painel: corpo || null, sugestoes: r.itens.length ? perguntasSeguintes(r, itens, serie) : [] };
    }
    const nomeProprioSe = v => /\p{L}/u.test(v) ? nomeProprio(v) : v;

    // O briefing de um cliente (consultar_briefing) como resposta do Flow, sem passar pelo modelo. b: o JSON do servidor;
    // c: a linha do cliente na carteira do painel (pode faltar); D: os dados do painel (só para o ano anterior).
    function analiseBriefing(b, c, D) {
      const nome = nomeProprio(String(b.cliente || '').replace(/\s*\([^()]*\)\s*$/, '')) || 'o cliente';
      const a = b.emAberto || {}, parados = b.deixouDeComprar || [], paragrafos = [], metricas = [];
      const plural = (n, um, mais) => n === 1 ? um : mais;
      if (c) metricas.push({ r: 'Faturação', v: fmt.eur(c.num), ...(c.delta != null && !c.novo ? { delta: c.delta, ano: D && D.anoAnt } : {}) });
      if (c && c.ultima) metricas.push({ r: 'Última compra', v: fmt.dataCurta(c.ultima) });
      metricas.push({ r: 'Faturas por pagar (com IVA)', v: a.documentos ? fmt.eur2(a.total) : 'Nenhuma' });
      if (a.documentosCredito) metricas.push({ r: 'Notas de crédito por abater', v: fmt.eur2(a.creditos) });
      if (b.maisComprado) paragrafos.push(`Artigo mais comprado: ${nomeProprio(b.maisComprado.artigo)}.`);
      paragrafos.push(parados.length
        ? `Deixou de comprar ${parados.length} ${plural(parados.length, 'artigo', 'artigos')}: ${parados.slice(0, 3).map(x =>
          `${nomeProprio(x.artigo)} (comprava ${fmt.decimal(x.mediaMes, x.mediaMes % 1 ? 1 : 0)} por mês, última compra a ${fmt.dataCurta(x.ultima)})`).join('; ')}${parados.length > 3 ? '…' : ''}.`
        : 'Continua a comprar os artigos habituais.');
      paragrafos.push(a.documentos
        ? `Tem ${fmt.inteiro(a.documentos)} ${plural(a.documentos, 'fatura', 'faturas')} por pagar, ${fmt.eur2(a.total)} com IVA${a.vencido > 0 ? `, dos quais ${fmt.eur2(a.vencido)} vencidos` : ''}.`
        : 'Não tem faturas por pagar.');
      if (a.documentosCredito) paragrafos.push(`Tem ${fmt.inteiro(a.documentosCredito)} ${plural(a.documentosCredito, 'nota de crédito', 'notas de crédito')} por abater, ${fmt.eur2(a.creditos)}.`);
      return { titulo: `Antes de visitar ${nome}`, paragrafos, metricas: metricas.slice(0, 4), avisos: [], opcoes: [], painel: null, nota: '',
        linhas: [['Indicador', 'Valor'], ...metricas.map(m => [m.r, m.v])],
        sugestoes: [`Que artigos comprou o cliente ${nome} este ano?`, `Evolução mensal das vendas do cliente ${nome} este ano`] };
    }

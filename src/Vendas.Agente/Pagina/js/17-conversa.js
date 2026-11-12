    // Desenha a resposta uma só vez; a conversa e o painel usam o mesmo resultado
    function desenharResposta(item) {
      if (item.art) return item.art;
      try {
        item.art = formatar(item.texto, item.pergunta);
      } catch (erro) {
        // Se a formatação falhar, mostra o texto tal como veio: o painel nunca fica parado.
        console.error('Falha a formatar a resposta:', erro);
        item.art = el('article', 'chat-folha');
        item.art.append(el('p', 'chat-texto', item.texto.trim()));
      }
      // Com números (cartões, lista, tabela, gráfico, ficha) vai para o painel; texto e "não encontrei" ficam na conversa
      item.noPainel = !!item.art.querySelector('.chat-kpis, .chat-folha-total, .chat-rank, .chat-circulo, .chat-lista, .chat-tabela, .chat-grafico, .chat-ficha');
      return item.art;
    }
    const tituloDaResposta = item => ((item.art && item.art.querySelector('.chat-folha-titulo')) || {}).textContent || item.pergunta;
    // Frase curta para a conversa: o destaque, ou o título e o valor principal
    function resumoDaResposta(art) {
      const texto = s => (art.querySelector(s) || {}).textContent || '';
      if (texto('.chat-destaque')) return texto('.chat-destaque');
      const titulo = texto('.chat-folha-titulo'), valor = texto('.chat-folha-total') || texto('.chat-kpi-valor');
      if (titulo && valor) return `${titulo}: ${valor}.`;
      return titulo ? titulo + '.' : 'Os dados estão no painel.';
    }

    // O que o assistente diz na conversa: o erro, a resposta sem dados, ou uma frase e o cartão do resultado
    function mensagemAgente(item) {
      if (item.erro) {
        const art = el('article', 'chat-folha chat-folha--erro');
        art.setAttribute('role', 'alert');
        art.append(el('p', '', item.erro === 'vazio' ? 'O agente não devolveu resposta. Tente de novo.'
          : item.erro === 'parado' ? 'Parou a resposta antes de terminar.'
          : 'Sem ligação ao agente. Confirme que está a correr e tente de novo.'));
        art.append(botaoEl('chat-acao', 'Tentar de novo', () => {
          historico.splice(historico.indexOf(item), 1);
          perguntar(item.pergunta);
        }));
        return art;
      }
      // Pedido de resumo: frase curta e o cartão, que volta a abrir este resumo
      if (item.resumo) {
        const pr = item.resumo, aberto = vistaResumo && resumoEstado.periodo === pr.periodo && resumoEstado.cliente === pr.cliente;
        const bolha = el('article', 'chat-folha');
        bolha.append(el('p', 'chat-texto', item.texto));
        const cartao = botaoEl('chat-cartao', '', () => pedirResumo(pr.periodo, pr.cliente));
        if (aberto) cartao.setAttribute('aria-current', 'true');
        const textos = el('span', 'chat-cartao-texto');
        textos.append(el('b', '', 'Resumo das vendas' + (pr.cliente ? ' · cliente ' + pr.cliente : '')), el('small', '', aberto ? 'Aberto no painel' : 'Abrir no painel'));
        cartao.append(icone('M3 13V8M8 13V3M13 13V6', 18), textos);
        bolha.append(cartao);
        return bolha;
      }
      const art = desenharResposta(item);
      if (!item.noPainel) return art;
      const bolha = el('article', 'chat-folha');
      bolha.append(el('p', 'chat-texto', resumoDaResposta(art)));
      const cartao = botaoEl('chat-cartao', '', () => abrirAba(item));
      if (item === abaAtiva) cartao.setAttribute('aria-current', 'true');
      const textos = el('span', 'chat-cartao-texto');
      textos.append(el('b', '', tituloDaResposta(item)), el('small', '', item === abaAtiva ? 'Aberto no painel' : 'Abrir no painel'));
      cartao.append(icone('M3 13V8M8 13V3M13 13V6', 18), textos);
      bolha.append(cartao);
      return bolha;
    }

    // Boas-vindas em três passos: início (como começar), conversa (escrever) ou dados (escolher um tema)
    let modoInicio = 'inicio';
    const mudarModo = (modo, foco) => { modoInicio = modo; desenharConversa(); (foco ? foco() : area.querySelector('.flow-modo, .flow-mudar'))?.focus(); };
    function boasVindas() {
      const ver = modo => () => mudarModo(modo, modo === 'conversa' ? () => campo : modo === 'dados' ? () => area.querySelector('.chat-sugestoes button') : null);
      const linhaModo = texto => { const p = el('p', 'flow-modo-atual', texto); p.append(botaoEl('flow-mudar', 'Mudar', ver('inicio'))); return p; };
      if (modoInicio === 'conversa') {
        return [linhaModo('Modo: conversa'),
          el('p', 'flow-msg', 'Diga-me o que precisa de saber. Por exemplo: "Como correu a faturação em agosto?" ou "Quais são os 5 melhores clientes este ano?"')];
      }
      // Análise de dados: o Flow diz que vista tem à frente e sugere perguntas sobre ela (o tema segue a vista do painel)
      if (modoInicio === 'dados') {
        temaAtual = { clientes: 'clientes', artigos: 'artigos' }[secao] || 'faturacao';
        const msg = el('div', 'flow-msg'), visto = el('p');
        visto.append('Estou a ver ', el('strong', '', nomeSecao()), '. Pergunte o que quiser sobre esta vista.');
        msg.append(el('p', 'flow-msg-ola', `Olá${nomeUtilizador ? ' ' + nomeUtilizador.split(/\s+/)[0] : ''}, sou o Flow.`), visto);
        return [msg, blocoSugestoes('Sugestões para ' + nomeSecao().toLowerCase())];
      }
      const msg = el('div', 'flow-msg');
      const lista = el('ul');
      ['Pergunte por palavras suas, como a um colega.', 'Ou escolha um tema: os gráficos abrem no painel ao lado.', 'Guarde o que lhe interessa no resumo, à esquerda.']
        .forEach(t => lista.append(el('li', '', t)));
      msg.append(el('p', 'flow-msg-ola', `Olá${nomeUtilizador ? ' ' + nomeUtilizador.split(/\s+/)[0] : ''}, sou o Flow.`),
        el('p', '', 'Respondo a perguntas sobre vendas, clientes e artigos com os dados da base de dados.'), lista,
        el('small', '', 'Só consulto dados — não altero nada na base de dados.'));
      const filhos = [msg];
      // Com o Resumo carregado, o Flow aponta logo a maior quebra por zona
      const D = resumoEstado.dados, pz = D && !D.erro && D.locaisDesce;
      if (pz) {
        const nota = el('div', 'flow-msg flow-msg--destaque');
        const texto = el('p');
        texto.append('Hoje reparei numa coisa: ', nomeProprio(pz.nome), ' faturou ', el('strong', '', `menos ${rDec(Math.abs(pz.delta), 1)}%`), ` do que em ${D.anoAnt}. Quer ver o detalhe?`);
        const alerta = D.alertas.find(a => a.tipo === 'local');
        nota.append(texto, botaoEl('flow-mudar', 'Ver detalhe', () => { if (!ocupado && alerta) perguntar(alerta.pergunta); }));
        filhos.push(nota);
      }
      const modo = (cls, icone, titulo, texto, acao) => {
        const b = botaoEl('flow-modo', '', acao);
        const ic = el('span', 'flow-modo-ic' + cls);
        ic.setAttribute('aria-hidden', 'true');
        ic.innerHTML = icone;
        const t = el('span');
        t.append(el('b', '', titulo), el('small', '', texto));
        b.append(ic, t);
        return b;
      };
      filhos.push(el('p', 'flow-pergunta', 'Como quer começar?'),
        modo('', '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 5h16v11H9l-5 4Z"/></svg>',
          'Vamos conversar', 'Pergunte por palavras suas, como faria a um colega.', ver('conversa')),
        modo(' flow-modo-ic--escuro', '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 20V12M12 20V5M18 20v-8"/></svg>',
          'Vamos analisar dados', 'Escolha um tema. Os resultados abrem no painel.', ver('dados')));
      return filhos;
    }

    // A conversa inteira, da primeira pergunta à última; feedback e sugestões só na última resposta
    function desenharConversa() {
      const filhos = [];
      if (!historico.length) {
        filhos.push(...boasVindas());
      }
      temasEl.hidden = !historico.length;   // antes da 1.ª pergunta, os temas estão no modo "analisar dados"
      const lista = [...historico].reverse();   // o histórico guarda a mais recente primeiro
      lista.forEach((item, k) => {
        filhos.push(mensagemEu(item.pergunta, item.hora, [
          botaoEl('chat-acao', 'Editar', () => prepararPergunta(item.pergunta)),
          botaoEl('chat-acao', 'Repetir', () => { if (!ocupado) perguntar(item.pergunta); })]));
        if (item.pendente) return;                // a espera é desenhada por mostrarEspera
        filhos.push(comAvatar(mensagemAgente(item), item.horaResposta));
        if (k === lista.length - 1 && !item.erro) {
          if (!item.resumo) filhos.push(blocoFeedback(item));   // o resumo não vem do agente: não há resposta a avaliar
          const seguintes = (item.art && item.art.sugestoes) || [];
          filhos.push(seguintes.length ? blocoSeguintes(seguintes) : blocoSugestoes('Mais sobre ' + tema().nome.toLowerCase()));
        }
      });
      area.replaceChildren(...filhos);
      area.scrollTop = area.scrollHeight;
    }

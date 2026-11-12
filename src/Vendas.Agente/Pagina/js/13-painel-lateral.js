    const nomePeriodo = p => (RES_PERIODOS.find(x => x[0] === p) || [])[1] || '';
    // Nome de quem tem a sessão: o servidor troca {{UTILIZADOR}}; sem troca, a página não mostra nome
    const nomeUtilizador = (n => n && !n.startsWith('{{') ? n.trim() : '')(document.body.dataset.utilizador);
    const ola = () => { const h = new Date().getHours(); return h < 12 ? 'Bom dia' : h < 20 ? 'Boa tarde' : 'Boa noite'; };
    // Breadcrumb: Inforap Flow › Vendas › Resumo das vendas (› Cliente/Zona do filtro) (› título da resposta aberta).
    // Os passos anteriores ao atual são botões; os cliques tratam-se em painelEl (data-migalha), porque o Resumo
    // usa a breadcrumb em HTML (outerHTML) e aí os listeners perdiam-se.
    function migalhas(resposta) {
      const nav = el('nav', 'flow-migalhas'), ol = el('ol');
      nav.setAttribute('aria-label', 'Onde está');
      const passo = (texto, acao) => {
        const li = el('li');
        if (acao) { const b = el('button', '', texto); b.type = 'button'; b.dataset.migalha = acao; li.append(b); }
        else { li.append(el('span', '', texto)); }
        ol.append(li);
        return li;
      };
      const D = resumoEstado.dados || {};
      const filtro = resumoEstado.cliente ? 'Cliente ' + (D.cliente || resumoEstado.cliente) : resumoEstado.zona ? 'Distrito: ' + nomeDistrito(resumoEstado.zona) : '';
      passo('Inforap Flow');
      passo('Vendas');
      const atual = passo(nomeSecao(), resposta || filtro ? 'geral' : null);
      if (filtro) passo(filtro, resposta ? 'resumo' : null);
      if (resposta) passo(resposta);
      (ol.lastChild || atual).setAttribute('aria-current', 'page');
      nav.append(ol);
      return nav;
    }
    painelEl.addEventListener('click', e => {
      const acao = e.target.closest('[data-migalha]')?.dataset.migalha;
      if (!acao) return;
      if (acao === 'geral') pedirResumo(null);   // sem cliente nem zona, mesmo período
      else verInicio();
      painelEl.querySelector('[data-menu]')?.focus();
    });
    if (nomeUtilizador) {
      const iniciais = document.getElementById('iniciais');
      const p = nomeUtilizador.split(/\s+/);
      iniciais.textContent = (p[0][0] + (p.length > 1 ? p[p.length - 1][0] : '')).toUpperCase();
      iniciais.title = nomeUtilizador;
      document.getElementById('utilizador-nome').textContent = nomeUtilizador;
      document.getElementById('utilizador').hidden = false;
    }
    const chipVar = (v, sufixo = '') => v == null ? '' : `<span class="chat-delta ${v >= 0 ? 'chat-delta--sobe' : 'chat-delta--desce'}">${rVar(v)}${sufixo}</span>`;
    // "02-11-2026" -> "2 nov"
    const dataCurta = d => { const m = (d || '').match(/^(\d{2})-(\d{2})/); return m ? `${+m[1]} ${MESES[m[2] - 1].slice(0, 3).toLowerCase()}` : ''; };

    // Painel lateral: o botão de guardar acompanha o que está à frente; as vistas e a navegação também
    function desenharLateral() {
      const D = resumoEstado.dados, tem = !!D && !D.erro;
      // Com uma resposta à frente guarda-se a pergunta; no Resumo, a carregar os dados ainda são do filtro anterior
      // (guardar juntava os filtros novos ao total antigo)
      document.getElementById('guardar-vista').disabled = respostaAFrente() ? false : !tem || resumoEstado.aCarregar;
      document.getElementById('guardar-vista-texto').textContent = textoGuardar();
      desenharVistas();
      desenharNav();
    }
    // Navegação entre vistas: a vista atual só fica marcada com o Resumo à frente (não com uma resposta aberta)
    function desenharNav() {
      document.getElementById('nav-lista').replaceChildren(...SECOES.map(([id, nome]) => {
        // Sem perguntas feitas, as sugestões da conversa acompanham a vista escolhida
        const b = botaoEl('', nome, () => { secao = id; verInicio(); });   // verInicio redesenha a conversa: as sugestões seguem a vista
        b.title = nome;   // com a barra recolhida só se vê o ícone
        b.insertAdjacentHTML('afterbegin', svgIc(id === 'geral' ? 'resumo' : id, 17));
        const porLer = id === 'alertas' ? nAlertasPorLer(resumoEstado.dados) : 0;
        if (porLer) b.insertAdjacentHTML('beforeend', `<span class="flow-nav-n" title="${porLer} por ler">${porLer}</span>`);
        if (vistaResumo && id === secao) b.setAttribute('aria-current', 'page');
        return b;
      }));
      document.getElementById('contexto-texto').textContent = respostaAFrente() ? tituloDaResposta(respostaAFrente()) : nomeSecao();
    }

    // Vistas guardadas: os filtros do Resumo (período + zona + cliente) ou a pergunta de uma resposta.
    // Só nesta sessão (sessionStorage): recarregar mantém-nas, fechar o separador/browser apaga-as. Máximo 6.
    const VISTAS = 'flow-vistas';
    localStorage.removeItem(VISTAS);   // as versões anteriores guardavam para sempre neste computador
    const lerVistas = () => { try { return JSON.parse(sessionStorage.getItem(VISTAS)) || []; } catch { return []; } };
    // A secção (Clientes, Faturação…) faz parte da vista: sem ela, as mesmas datas noutra secção eram "a mesma vista"
    const chaveVista = v => v.pergunta ? 'p:' + v.pergunta : JSON.stringify([v.secao || 'geral', v.periodo, v.zona || null, v.cliente || null]);
    const mesmaVista = (v, e) => chaveVista(v) === chaveVista(e);
    // A resposta aberta no painel (null = o Resumo está à frente)
    const respostaAFrente = () => !vistaResumo && abaAtiva && !abaAtiva.pendente ? abaAtiva : null;
    const textoGuardar = () => respostaAFrente() ? 'Guardar esta resposta' : resumoEstado.aCarregar ? 'A carregar os dados…' : 'Guardar vista atual';
    // Uma pergunta guardada: abre a resposta se ainda está nesta sessão; senão pergunta outra vez (dados de agora)
    function abrirPergunta(pergunta) {
      const aberta = historico.find(h => h.pergunta === pergunta && h.art && !h.erro && !h.pendente);
      if (aberta) abrirAba(aberta);
      else { verConversa(true); perguntar(pergunta); }
    }
    function desenharVistas() {
      const vistas = lerVistas();
      const D = resumoEstado.dados, resposta = respostaAFrente();
      document.getElementById('vistas').replaceChildren(...vistas.map(v => {
        const ativa = v.pergunta ? !!resposta && resposta.pergunta === v.pergunta : vistaResumo && mesmaVista(v, { ...resumoEstado, secao });
        if (ativa && !v.pergunta && D && !D.erro && D.total != null) v.total = D.total;   // o valor acompanha a última consulta
        const b = botaoEl('flow-vista', '', () => v.pergunta ? abrirPergunta(v.pergunta) : (secao = v.secao || 'geral', pedirResumo(v.periodo, v.cliente, v.zona)));
        if (v.pergunta) b.title = v.pergunta;
        b.append(el('span', '', v.nome), el('span', '', v.pergunta ? 'Resposta' : v.total == null ? '' : rCurto(v.total)));
        if (ativa) b.setAttribute('aria-current', 'true');
        const li = el('li'); li.append(b); return li;
      }));
      sessionStorage.setItem(VISTAS, JSON.stringify(vistas));
      // Exportar (CSV para o Excel) e Descartar: só com vistas na lista
      const acoes = document.getElementById('vistas-acoes');
      acoes.hidden = !vistas.length;
      const linhas = [['Vista', 'Tipo', 'Período', 'Distrito', 'Cliente', 'Pergunta', 'Faturação'],
        ...vistas.map(v => [v.nome, v.pergunta ? 'Resposta' : 'Resumo', v.pergunta ? '' : nomePeriodo(v.periodo), v.zona ? nomeDistrito(v.zona) : '',
                             v.cliente || '', v.pergunta || '', v.pergunta || v.total == null ? '' : rEur2(v.total)])];
      const descartar = botaoEl('chat-acao', 'Descartar', () => {
        if (!confirm('Descartar as vistas guardadas desta sessão?')) return;
        sessionStorage.removeItem(VISTAS);
        desenharVistas();
        anunciar('Vistas guardadas descartadas.');
        document.getElementById('guardar-vista').focus();   // o botão que tinha o foco desapareceu
      });
      acoes.replaceChildren(botaoExportar(linhas, 'vistas guardadas'), descartar);
    }
    document.getElementById('guardar-vista').addEventListener('click', () => {
      const D = resumoEstado.dados, resposta = respostaAFrente();
      let e, nome;
      if (resposta) {
        nome = tituloDaResposta(resposta);
        e = { pergunta: resposta.pergunta, nome };
      } else {
        if (!D || D.erro) return;
        const r = resumoEstado;
        nome = nomeSecao() + ' · ' + nomePeriodo(r.periodo) + ' · ' + (r.cliente ? 'Cliente ' + (D.cliente || r.cliente) : r.zona ? nomeDistrito(r.zona) : 'todos os distritos');
        e = { secao, periodo: r.periodo, zona: r.zona, cliente: r.cliente, nome, total: D.total };
      }
      const vistas = [e, ...lerVistas().filter(v => !mesmaVista(v, e))].slice(0, 6);
      const jaExistia = lerVistas().some(v => mesmaVista(v, e));
      sessionStorage.setItem(VISTAS, JSON.stringify(vistas));
      desenharVistas();
      anunciar('Vista guardada: ' + nome + '.');
      // Confirmação à vista: guardar uma vista que já existia não mudava nada na lista
      const texto = document.getElementById('guardar-vista-texto');
      texto.textContent = jaExistia ? '✓ Esta vista já estava guardada' : '✓ Vista guardada';
      setTimeout(() => { texto.textContent = textoGuardar(); }, 2500);
    });

    // Recolher o painel lateral: fica só a marca e o botão
    document.getElementById('btn-lateral').addEventListener('click', e => {
      const fechar = document.getElementById('lateral').classList.toggle('flow-lateral--recolhido');
      e.currentTarget.setAttribute('aria-expanded', String(!fechar));
      e.currentTarget.setAttribute('aria-label', fechar ? 'Abrir o painel lateral' : 'Recolher o painel lateral');
    });
    document.getElementById('procurar-ic').addEventListener('click', () => {
      document.getElementById('btn-lateral').click();   // só se vê com o painel recolhido: este clique abre-o
      document.getElementById('procurar-campo').focus();
    });

    // "▲ MAIOR SUBIDA" / "▼ MAIOR DESCIDA" por baixo de um bloco
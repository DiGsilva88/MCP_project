    // O resultado aberto, ou o Resumo (painel inicial). Voltar faz-se pela breadcrumb; outras respostas reabrem pelo cartão na conversa.
    // Ecrã de entrada (maquete "versão final", 11/2026): à esquerda a pergunta, à direita uma ilustração de como as respostas aparecem.
    // Do ecrã anterior só fica o logótipo. Perguntar sai logo para o painel: a espera e os erros aparecem na conversa.
    function folhaBoasVindas() {
      const sair = acao => () => { boasVindasAberta = false; secao = 'geral'; vistaResumo = true; abaAtiva = null; desenharPainel(); acao?.(); };
      // Até 1200px a conversa tapa parte dos dados: quem vem ver o painel entra com ela minimizada
      const libertarDados = () => { if (painelRaiz.clientWidth <= 1200) verConversa(false); };
      const D = resumoEstado.dados;
      const raiz = el('div', 'flow-ini');

      const topo = el('header', 'flow-ini-topo'), marca = el('div', 'flow-ini-marca'), avatar = el('img');
      // ?v=: a imagem fica 24 h na cache do browser; aumentar o número sempre que o ficheiro mudar. O logo já traz o nome
      Object.assign(avatar, { src: '/imagens/avatar-flow.png?v=3', alt: 'Flow', width: 120, height: 88 });
      marca.append(avatar);
      topo.append(marca);
      const m = D && D.ate ? D.ate.match(/^(\d{2})-(\d{2})/) : null;
      if (m) {
        const fresco = el('p', 'flow-ini-fresco', `Dados atualizados até ${+m[1]} de ${MESES[m[2] - 1].toLowerCase()}`);
        const ponto = el('i');
        ponto.setAttribute('aria-hidden', 'true');
        fresco.prepend(ponto);
        topo.append(fresco);
      }

      const esq = el('div', 'flow-ini-esq');
      const titulo = el('h1');
      titulo.append(el('span', 'flow-ini-ac', 'Uma plataforma.'), el('span', '', 'Todos os dados'), el('span', '', 'de vendas.'));
      const escrever = el('form', 'flow-ini-pergunta');
      escrever.noValidate = true;
      const rotulo = el('label', 'so-leitor', 'Pergunta sobre clientes, vendas ou faturação');
      rotulo.htmlFor = 'flow-ini-q';
      const entrada = el('input');
      Object.assign(entrada, { id: 'flow-ini-q', maxLength: 500, autocomplete: 'off', placeholder: 'Clientes que não compram há 90 dias?' });
      entrada.setAttribute('aria-describedby', 'flow-ini-msg');
      const enviar = el('button', '', 'Perguntar');
      enviar.type = 'submit';
      escrever.append(rotulo, entrada, enviar);
      const msg = el('p', 'flow-ini-msg');
      Object.assign(msg, { id: 'flow-ini-msg' });
      msg.setAttribute('role', 'status');
      escrever.addEventListener('submit', e => {
        e.preventDefault();
        const texto = entrada.value.replace(/\s+/g, ' ').trim();
        if (texto) return sair(() => perguntar(texto))();
        escrever.classList.add('flow-ini-pergunta--vazia');
        msg.textContent = 'Escreva uma pergunta sobre clientes, vendas ou faturação.';
        entrada.focus();
      });
      entrada.addEventListener('input', () => { escrever.classList.remove('flow-ini-pergunta--vazia'); msg.textContent = ''; });
      const direto = botaoEl('flow-ini-direto', 'Ir direto ao painel de vendas', sair(() => { modoInicio = 'dados'; desenharConversa(); libertarDados(); painelEl.querySelector('[data-menu]')?.focus(); }));
      esq.append(titulo, el('p', 'flow-ini-sub', 'Pergunte por palavras suas. A resposta abre no painel principal.'), escrever, msg, direto);

      // Só ilustração (números inventados, com a legenda "Exemplo"): o leitor de ecrã salta-a
      const arte = el('div', 'flow-ini-arte');
      arte.setAttribute('aria-hidden', 'true');
      const barras = [73, 86, 67, 93, 100, 57].map((h, i) => `<i style="height:${h}%"${i === 5 ? ' class="flow-ini-claro"' : ''}></i>`).join('');
      const fatias = [['#1f5bf5', 'Braga', '108 400 €'], ['#000', 'Porto', '64 100 €'], ['#9db5ff', 'Guimarães', '44 300 €'], ['#e4ebff', 'Outras', '29 800 €']]
        .map(([cor, nome, valor]) => `<li><b><i style="background:${cor}"></i>${nome}</b><span>${valor}</span></li>`).join('');
      arte.innerHTML = `<div class="flow-ini-fundo">
          <svg viewBox="0 0 500 520" preserveAspectRatio="none" fill="none"><path d="M-20 360 C 80 360 90 200 190 200 S 300 380 380 330 S 470 120 540 160" stroke="#fff" stroke-width="2"/><circle cx="250" cy="260" r="170" stroke="#fff" stroke-width="1.5"/></svg>
          <p class="flow-ini-balao">Como evoluíram as vendas mês a mês?</p></div>
        <div class="flow-ini-cartao flow-ini-c1"><h4>Vendas por mês <small>em milhares de €</small></h4>
          <div class="flow-ini-barras"><span class="flow-ini-media" style="bottom:66%"></span>${barras}</div>
          <div class="flow-ini-meses">${['Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov'].map(x => `<span>${x}</span>`).join('')}</div>
          <p class="flow-ini-leg"><i></i>Média 198 mil €</p></div>
        <div class="flow-ini-cartao flow-ini-c2"><h4>Faturação por loja <small>Últimos 30 dias</small></h4>
          <div class="flow-ini-donut"><svg width="96" height="96" viewBox="0 0 42 42"><circle cx="21" cy="21" r="15.9" fill="none" stroke="#e4ebff" stroke-width="6"/><circle cx="21" cy="21" r="15.9" fill="none" stroke="#1f5bf5" stroke-width="6" stroke-dasharray="44 56" stroke-dashoffset="25"/><circle cx="21" cy="21" r="15.9" fill="none" stroke="#000" stroke-width="6" stroke-dasharray="26 74" stroke-dashoffset="81"/><circle cx="21" cy="21" r="15.9" fill="none" stroke="#9db5ff" stroke-width="6" stroke-dasharray="18 82" stroke-dashoffset="55"/></svg><ul>${fatias}</ul></div></div>
        <p></p>`;

      const grelha = el('div', 'flow-ini-grelha');
      grelha.append(esq, arte);
      const rodape = el('footer', 'flow-ini-rodape');
      rodape.append(el('span', '', 'Só leitura. O assistente não altera a base de dados.'), el('span', '', `© ${new Date().getFullYear()} Inforap. Todos os direitos reservados.`));
      raiz.append(topo, grelha, rodape);
      return raiz;
    }

    // Onde se está (vista, período e filtros): recarregar a página volta aqui (ver 21-eventos.js).
    // Só nesta sessão, como as vistas guardadas: fechar o separador apaga-o.
    const LUGAR = 'flow-lugar';
    function desenharPainel() {
      try { sessionStorage.setItem(LUGAR, JSON.stringify({ inicio: boasVindasAberta, secao, periodo: resumoEstado.periodo, zona: resumoEstado.zona, cliente: resumoEstado.cliente })); } catch { }
      painelRaiz.classList.toggle('chat-painel--inicio', boasVindasAberta);
      if (boasVindasAberta) {
        // Recarregar o Resumo redesenha o painel: mantém o texto da pergunta que já estava a ser escrita
        const antes = painelEl.querySelector('.flow-ini input');
        const focado = !!antes && document.activeElement === antes;
        painelEl.replaceChildren(folhaBoasVindas());
        if (!antes) painelEl.querySelector('.flow-ini').classList.add('flow-ini--entra');   // só a primeira vez: redesenhar não volta a animar
        const nova = painelEl.querySelector('.flow-ini input');
        nova.value = antes ? antes.value : '';
        if (focado) nova.focus({ preventScroll: true });
        return;
      }
      if (historico.some(h => h.pendente)) {
        const esq = el('div', 'chat-painel-esqueleto');
        const cartoes = el('div', 'chat-esq-cartoes'), grafico = el('div', 'chat-esq-grafico');
        for (let i = 0; i < 3; i++) cartoes.append(el('span', 'chat-esq-cartao'));
        [55, 80, 40, 95, 65, 75, 50].forEach(h => { const b = el('span'); b.style.height = h + '%'; grafico.append(b); });
        const formas = [el('span', 'chat-esq-linha'), el('span', 'chat-esq-linha chat-esq-linha--curta'), cartoes, grafico];
        formas.forEach(f => f.setAttribute('aria-hidden', 'true'));   // só decoração; o leitor de ecrã ouve o texto escondido
        esq.append(el('span', 'so-leitor', 'A preparar os dados…'), ...formas);
        painelEl.replaceChildren(esq);
      } else if (vistaResumo || !abaAtiva) {
        vistaResumo = true;
        painelEl.replaceChildren(folhaResumo());
      } else {
        painelEl.replaceChildren(migalhas(tituloDaResposta(abaAtiva)), abaAtiva.art);
      }
      painelEl.scrollTop = 0;
      desenharLateral();   // o botão "Abrir o painel inicial" e a vista ativa mudam com a resposta aberta
    }
    // Volta ao painel inicial (o Resumo)
    function verInicio() {
      vistaResumo = true;
      abaAtiva = null;
      desenharPainel();
      desenharConversa();
    }
    function abrirAba(item) {
      abaAtiva = item;
      vistaResumo = false;
      desenharPainel();
      desenharConversa();
    }
    // Resposta nova: com dados abre no painel; sem dados fica só na conversa
    function mostrarResposta(item) {
      if (!item.erro) desenharResposta(item);
      if (!item.erro && item.noPainel) abrirAba(item);
      else desenharConversa();
    }

    // Mostrar ou esconder a janela da conversa; o botão redondo diz sempre o que vai fazer
    const lancador = document.getElementById('mostrar-conversa');
    function verConversa(sim) {
      if (soPainel) return;
      painelRaiz.classList.toggle('chat-painel--sem-conversa', !sim);
      lancador.setAttribute('aria-expanded', String(sim));
      lancador.setAttribute('aria-label', sim ? 'Flow, assistente de vendas: minimizar a conversa' : 'Flow, assistente de vendas: abrir a conversa');
      (sim ? campo : lancador).focus();
    }
    document.getElementById('esconder-conversa').addEventListener('click', () => verConversa(false));
    lancador.addEventListener('click', () => verConversa(painelRaiz.classList.contains('chat-painel--sem-conversa')));
    // ×: fecha e as boas-vindas voltam ao início (a conversa já feita fica)
    document.getElementById('fechar-conversa').addEventListener('click', () => { modoInicio = 'inicio'; desenharConversa(); verConversa(false); });

    // Barra do topo: exportar o separador aberto, email à segunda e procurar
    const topoExportar = document.getElementById('topo-exportar');
    const dlgExportar = document.getElementById('dlg-exportar');
    topoExportar.addEventListener('click', () => {
      // As duas opções sempre à vista; a que o separador aberto não tem fica desativada e escolhe-se a primeira que existe
      const opcoes = [...dlgExportar.querySelectorAll('input[name="formato"]')];
      opcoes.forEach(o => { o.disabled = !painelEl.querySelector(`[data-${o.value === 'email' ? 'exportar' : o.value}]`); o.checked = false; });
      const primeira = opcoes.find(o => !o.disabled);
      if (primeira) primeira.checked = true;
      // No painel escolhe-se o que entra no relatório; numa resposta aberta exporta-se essa resposta
      const D = dadosDoRelatorio();
      document.getElementById('dlg-exportar-titulo').textContent = D ? 'Exportar relatório' : 'Exportar dados';
      document.getElementById('dlg-exportar-nota').textContent = D ? 'Escolha o que entra no ficheiro. Os filtros aplicados ficam assim:' : 'Descarrega os dados do separador aberto.';
      filtrosExportar.hidden = !D;
      listaSeccoes.closest('fieldset').hidden = !D;
      if (D) {
        filtrosExportar.textContent = descreverFiltros(D).join(', ');
        const contagem = new Map();
        linhasResumo(D).slice(1).forEach(l => contagem.set(l[0], (contagem.get(l[0]) || 0) + 1));
        listaSeccoes.replaceChildren(...[...contagem].map(([nome, n]) => {
          const rotulo = el('label', 'dlg-opcao'), caixa = el('input');
          Object.assign(caixa, { type: 'checkbox', name: 'seccao', value: nome, checked: true });
          const texto = el('span');
          texto.append(el('b', '', nome), el('small', '', `${n} ${n === 1 ? 'linha' : 'linhas'}`));
          rotulo.append(caixa, texto);
          return rotulo;
        }));
      }
      atualizarExportar();
      dlgExportar.showModal();
    });
    const filtrosExportar = document.getElementById('dlg-exportar-filtros'), listaSeccoes = document.getElementById('exportar-lista');
    const dadosDoRelatorio = () => vistaResumo && resumoEstado.dados && !resumoEstado.dados.erro ? resumoEstado.dados : null;
    const descreverFiltros = D => [`Período: ${nomePeriodo(resumoEstado.periodo).toLowerCase()}`,
      resumoEstado.zona ? `Distrito: ${nomeDistrito(resumoEstado.zona)}` : 'Todos os distritos',
      resumoEstado.cliente ? `Cliente: ${D.cliente || resumoEstado.cliente}` : 'Todos os clientes'];
    // As secções só valem para o CSV, descarregado ou por email; sem nenhuma marcada não há ficheiro para gerar
    function atualizarExportar() {
      const f = new FormData(dlgExportar.querySelector('form')).get('formato'), comSeccoes = !!dadosDoRelatorio() && (f === 'exportar' || f === 'email');
      listaSeccoes.closest('fieldset').disabled = !comSeccoes;
      document.getElementById('dlg-exportar-ok').disabled = !f || (comSeccoes && !listaSeccoes.querySelector('input:checked'));
    }
    dlgExportar.addEventListener('change', atualizarExportar);
    dlgExportar.addEventListener('click', e => {
      const acao = e.target.closest('[data-seccoes]')?.dataset.seccoes;
      if (!acao) return;
      listaSeccoes.querySelectorAll('input').forEach(c => { c.checked = acao === 'todas'; });
      atualizarExportar();
    });
    dlgExportar.querySelector('form').addEventListener('submit', e => {
      if (e.submitter?.value !== 'exportar') return;   // "Fechar": o method="dialog" fecha sozinho
      const dados = new FormData(e.target), f = dados.get('formato'), D = dadosDoRelatorio();
      if ((f === 'exportar' || f === 'email') && D) {
        const marcadas = new Set(dados.getAll('seccao')), [cabecalho, ...resto] = linhasResumo(D);
        (f === 'email' ? enviarCsvPorEmail : descarregarCsv)([cabecalho, ...resto.filter(l => marcadas.has(l[0]))], ['relatório vendas', nomePeriodo(resumoEstado.periodo), resumoEstado.zona && nomeDistrito(resumoEstado.zona), resumoEstado.cliente && (D.cliente || resumoEstado.cliente)].filter(Boolean).join(' '));
        return;
      }
      if (f === 'email') { const b = painelEl.querySelector('[data-exportar]'); if (b) enviarCsvPorEmail(...b.dadosCsv); return; }
      if (f) painelEl.querySelector(`[data-${f}]`)?.click();
    });
    // o painel é redesenhado em vários sítios: observar evita esquecer algum
    // Exportar e Agendar envio: por cima do período e dos filtros; sem essa barra (ex.: resposta aberta), no topo do painel
    const acoesBotoes = topoExportar.parentElement;
    function colocarAcoes() {
      const alvo = document.getElementById('acoes-vaga') || painelEl.parentElement;
      if (acoesBotoes.parentElement !== alvo) alvo.prepend(acoesBotoes);
    }
    colocarAcoes();
    new MutationObserver(() => {
      colocarAcoes();
      topoExportar.disabled = !painelEl.querySelector('[data-exportar], [data-imagem]');
    }).observe(painelEl, { childList: true, subtree: true });
    document.getElementById('topo-email').addEventListener('click', () => document.getElementById('dlg-email').showModal());
    document.getElementById('dlg-email-fechar').addEventListener('click', () => document.getElementById('dlg-email').close());
    let avisoTempo;
    function avisar(texto) {
      const aviso = document.getElementById('aviso');
      aviso.textContent = texto;
      aviso.hidden = false;
      clearTimeout(avisoTempo);
      avisoTempo = setTimeout(() => { aviso.hidden = true; }, 5000);
    }
    // "Agendar envio": guarda no servidor; com sucesso fecha e avisa, senão diz na janela porquê (ex.: endereço fora da empresa).
    document.querySelector('#dlg-email form').addEventListener('submit', async e => {
      const botao = e.submitter;
      e.preventDefault();
      const dados = new FormData(e.target), estado = document.getElementById('dlg-email-estado');
      botao.disabled = true;
      try {
        const r = await fetch('/agendamentos', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ freq: dados.get('freq'), email: dados.get('email') })
        });
        const texto = await r.text();
        if (r.ok) {
          estado.hidden = true;
          document.getElementById('dlg-email').close();
          avisar(dados.get('freq') === 'nenhum' ? `Envio cancelado para ${dados.get('email')}.`
            : `Envio agendado: ${dados.get('freq') === 'mensal' ? 'na 1.ª segunda de cada mês' : 'todas as segundas'}, para ${dados.get('email')}.`);
          return;
        }
        estado.textContent = texto || 'Não foi possível agendar.';
        estado.hidden = false;
      } catch {
        estado.textContent = 'Não foi possível agendar: o servidor não respondeu.';
        estado.hidden = false;
      } finally {
        botao.disabled = false;
      }
    });
    document.getElementById('procurar').addEventListener('submit', e => {
      e.preventDefault();
      const caixa = document.getElementById('procurar-campo');
      const texto = caixa.value.replace(/\s+/g, ' ').trim();
      if (!texto) return;
      caixa.value = '';
      // Direto ao Resumo do cliente (como o formulário do painel): o agente não pesquisa artigos concretos.
      pedirResumo(null, texto);
    });

    // REDESIGN 11/2026: voltar ao topo do painel
    const voltarTopo = document.getElementById('voltar-topo');
    painelEl.addEventListener('scroll', () => { voltarTopo.hidden = painelEl.scrollTop < 300; });
    voltarTopo.addEventListener('click', () => { painelEl.scrollTo({ top: 0, behavior: 'smooth' }); voltarTopo.hidden = true; painelEl.querySelector('.flow-migalhas button, [data-menu]')?.focus(); });

    function mostrarInicio() {
      desenharConversa();
      desenharPainel();
    }

    // Mostra uma resposta já guardada (também usado para testar na consola)
    function mostrarItem(item) {
      if (!historico.includes(item)) historico.unshift(item);
      mostrarResposta(item);
    }

    function mostrarEspera(pergunta, parar) {
      const art = el('article', 'chat-folha chat-espera');
      const esq = el('div', 'chat-esqueleto');
      esq.setAttribute('aria-hidden', 'true');
      esq.append(el('span'), el('span'), el('span'));
      // Quando a resposta começa a chegar: só três pontos, o texto em bruto não se mostra (é formatado no fim)
      const pontos = el('p', 'chat-pontos');
      pontos.setAttribute('aria-hidden', 'true');
      pontos.append(el('span'), el('span'), el('span'));
      pontos.hidden = true;
      const msg = el('p', '', 'A consultar…');
      // o modelo em CPU pode levar mais de um minuto; o contador mostra que não parou
      const inicio = Date.now();
      let rotulo = 'A consultar…';
      const relogio = setInterval(() => {
        msg.textContent = `${rotulo} ${Math.round((Date.now() - inicio) / 1000)} s`;
      }, 1000);
      // Outra pessoa a ser atendida: diz porque espera, até chegar a vez
      const naFila = fila => { rotulo = fila ? 'Na fila: há outra pergunta em curso, a sua começa a seguir…' : 'A consultar…'; msg.textContent = rotulo; };
      art.append(msg, esq, pontos, botaoEl('chat-acao', 'Parar', parar));
      desenharConversa();                          // a pergunta já aparece na conversa
      area.append(comAvatar(art));
      area.scrollTop = area.scrollHeight;
      // Chamada no 1.º bocado da resposta: para o relógio e troca a mensagem pelos pontos
      const aChegar = () => { clearInterval(relogio); msg.hidden = true; esq.hidden = true; pontos.hidden = false; };
      return { aChegar, relogio, naFila };
    }
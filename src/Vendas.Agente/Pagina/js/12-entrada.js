    /* =========================================================
       Ecrã de entrada (aberto pelo botão do VisualIX). Estados da pergunta: sem texto, a consultar,
       respondido, erro e sem ligação; os dois últimos repetem a última pergunta.
       ========================================================= */
    const entrada = document.getElementById('entrada'), entForm = document.getElementById('ent-pergunta'), entCampo = document.getElementById('ent-campo'),
          entBotao = entForm.querySelector('button'), entMsg = document.getElementById('ent-msg');
    let entUltima = '';
    const LUGAR = 'flow-lugar';   // recarregar a página volta ao painel, no separador onde estava (só nesta sessão)

    function estadoEntrada(s) {
      entForm.classList.remove('a-consultar', 'erro');
      entCampo.disabled = entBotao.disabled = false;
      entBotao.lastElementChild.textContent = 'Perguntar';
      entMsg.className = 'ent-msg';
      entMsg.replaceChildren();
      const repetir = () => { const b = el('button', '', 'Tentar outra vez'); b.type = 'button'; b.addEventListener('click', () => enviarEntrada(entUltima)); return b; };
      if (s === 'vazio') { entForm.classList.add('erro'); entMsg.className = 'ent-msg info'; entMsg.textContent = 'Escreva uma pergunta sobre clientes, vendas ou faturação.'; entCampo.focus(); }
      if (s === 'consultar') { entForm.classList.add('a-consultar'); entCampo.disabled = entBotao.disabled = true; entBotao.lastElementChild.textContent = 'A consultar'; entMsg.className = 'ent-msg info'; entMsg.textContent = 'A consultar as vendas. A resposta abre no painel principal.'; }
      if (s === 'ok') { entMsg.className = 'ent-msg ok'; entMsg.textContent = 'A resposta abriu no painel principal.'; entCampo.value = ''; }
      if (s === 'erro') { entForm.classList.add('erro'); entMsg.className = 'ent-msg erro'; entMsg.append(el('span', '', 'Não foi possível obter a resposta. Tente outra vez ou reformule a pergunta.'), repetir()); }
      if (s === 'semLigacao') { entMsg.className = 'ent-msg erro'; entMsg.append(el('span', '', 'Sem ligação ao servidor de vendas. Verifique a ligação e tente outra vez.'), repetir()); }
    }
    async function enviarEntrada(texto) {
      texto = String(texto || '').replace(/\s+/g, ' ').trim();
      if (!texto) { estadoEntrada('vazio'); return; }
      entUltima = texto; entCampo.value = texto;
      estadoEntrada('consultar');
      const item = await perguntar(texto);
      if (!item || item.erro) { estadoEntrada(item?.erro === 'ligacao' ? 'semLigacao' : 'erro'); return; }
      estadoEntrada('ok');
      setTimeout(() => irParaPainel(), 900);
    }
    function irParaPainel() {
      entrada.hidden = true;
      document.querySelector('.app').inert = false;
      try { sessionStorage.setItem(LUGAR, '1'); } catch { }
      redesenhar(true);
    }

    entForm.addEventListener('submit', e => { e.preventDefault(); enviarEntrada(entCampo.value); });
    entCampo.addEventListener('input', () => { if (entForm.classList.contains('erro') && entMsg.classList.contains('info')) estadoEntrada(''); });
    document.getElementById('ent-direto').addEventListener('click', irParaPainel);
    document.getElementById('ent-ano').textContent = anoAtual();

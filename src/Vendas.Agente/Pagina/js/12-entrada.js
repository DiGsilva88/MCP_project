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

    // Cartões à direita: os últimos meses e a faturação por zona, com os dados reais do servidor (sem dados, não aparecem)
    function desenharArteEntrada() {
      const D = app.dados, arte = document.getElementById('ent-arte'), fresco = document.getElementById('ent-fresco');
      fresco.hidden = !D?.ate;
      if (D?.ate) fresco.lastChild.textContent = `Dados atualizados até ${fmt.dataLonga(D.ate)}`;
      const meses = D?.serie ? D.serie.meses.filter(x => x.atual > 0).slice(-6) : [];
      const zonas = (D?.zonas || []).filter(z => z.fat > 0).sort((a, b) => b.fat - a.fat);
      if (!D || D.erro || meses.length < 2 || !zonas.length) { arte.hidden = true; return; }
      arte.hidden = false;
      const max = Math.max(...meses.map(x => x.atual)), completos = meses.filter(x => !x.parcial);
      const media = completos.length ? completos.reduce((s, x) => s + x.atual, 0) / completos.length : null;
      document.getElementById('ent-barras').innerHTML = (media ? `<span class="ent-media" style="bottom:${(media / max * 100).toFixed(1)}%"></span>` : '') +
        meses.map(x => `<i class="${x.parcial ? 'parcial' : ''}" style="height:${Math.max(2, x.atual / max * 100).toFixed(1)}%"></i>`).join('');
      document.getElementById('ent-meses').innerHTML = meses.map(x => `<span>${esc(maiuscula(fmt.mesCurto(x.m)))}</span>`).join('');
      document.getElementById('ent-media').textContent = media ? `Média ${fmt.curto(media)}` : '';
      const partes = [...zonas.slice(0, 3), ...(zonas.length > 3 ? [{ nome: 'Outras', fat: zonas.slice(3).reduce((s, z) => s + z.fat, 0) }] : [])];
      const total = partes.reduce((s, z) => s + z.fat, 0);
      let ini = 25;   // começa no topo do círculo
      document.getElementById('ent-donut').innerHTML = `<svg width="96" height="96" viewBox="0 0 42 42" aria-hidden="true"><circle cx="21" cy="21" r="15.9" fill="none" class="ent-cor-4" stroke-width="6"/>${
        partes.map((z, i) => { const p = z.fat / total * 100, s = `<circle cx="21" cy="21" r="15.9" fill="none" class="ent-cor-${i + 1}" stroke-width="6" stroke-dasharray="${p.toFixed(2)} ${(100 - p).toFixed(2)}" stroke-dashoffset="${ini.toFixed(2)}"/>`; ini -= p; return s; }).join('')}</svg>
        <ul>${partes.map((z, i) => `<li><b><i class="ent-cor-${i + 1}"></i>${esc(z.nome)}</b><span>${fmt.eur(z.fat)}</span></li>`).join('')}</ul>`;
      document.getElementById('ent-periodo').textContent = nomePeriodo(app.periodo);
    }

    entForm.addEventListener('submit', e => { e.preventDefault(); enviarEntrada(entCampo.value); });
    entCampo.addEventListener('input', () => { if (entForm.classList.contains('erro') && entMsg.classList.contains('info')) estadoEntrada(''); });
    document.getElementById('ent-direto').addEventListener('click', irParaPainel);
    document.getElementById('ent-ano').textContent = anoAtual();

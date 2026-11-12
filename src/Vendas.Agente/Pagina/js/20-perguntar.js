  
  
  /* =========================================================
       Perguntar ao agente
       ========================================================= */
    // Resposta de um cliente (a ferramenta escreve 'cliente "Nome (1003)"' no título): o painel lateral passa a
    // mostrar o Resumo desse cliente, sem tirar a resposta do painel principal.
    // shortcut: só segue o cliente (não o período nem outros filtros da resposta); alargar se o lateral tiver de espelhar tudo.
    function acompanharCliente(item) {
      if (item.erro) return;
      const id = (item.texto.match(/cliente "[^"\n]*\(([^()"\n]+)\)"/) || [])[1];
      if (!id || id === resumoEstado.cliente) return;
      Object.assign(resumoEstado, { cliente: id, zona: null });
      carregarResumo();
    }

    async function perguntar(texto) {
      if (ocupado) return;
      // Pedido de resumo: abre o Resumo em vez de chamar o agente (instantâneo, números diretos do SQL)
      const pedidoResumo = intencaoResumo(texto);
      if (pedidoResumo) { responderComResumo(texto, pedidoResumo); return; }
      const item = { pergunta: texto, texto: '', erro: null, hora: new Date(), pendente: true };
      historico.unshift(item);
      bloquear(true);
      desenharTemas();
      const controlo = new AbortController();   // "Parar" cancela o pedido
      // /parar também pára o modelo no servidor (abortar só o fetch deixava-o a gerar)
      const { aChegar, relogio } = mostrarEspera(texto, () => { controlo.abort(); fetch('/parar', { method: 'POST' }).catch(() => {}); });
      desenharPainel();                         // esqueleto no painel até a resposta chegar
      area.setAttribute('aria-busy', 'true');   // o leitor de ecrã lê a resposta no fim, não aos bocados
      try {
        const r = await fetch('/perguntar', { method: 'POST', body: texto, signal: controlo.signal });
        mostrarEstado(r.ok ? 'ocupado' : 'desligado');
        if (!r.ok) throw new Error('HTTP ' + r.status);
        const leitor = r.body.getReader();
        const descodificador = new TextDecoder();
        for (;;) {
          const { done, value } = await leitor.read();
          if (done) break;
          item.texto += descodificador.decode(value, { stream: true });
          aChegar();
        }
        item.texto += descodificador.decode();
        if (!item.texto.trim()) item.erro = 'vazio';
        mostrarEstado('pronto');
      } catch (e) {
        item.erro = e.name === 'AbortError' ? 'parado' : 'ligacao';
        mostrarEstado(item.erro === 'parado' ? 'pronto' : 'desligado');
      } finally {
        clearInterval(relogio);
        bloquear(false);
        if (estadoTexto.textContent === 'A consultar') mostrarEstado('pronto');
        item.horaResposta = new Date();
        item.pendente = false;
        desenharPainel();                       // tira o esqueleto (sem dados ou com erro volta ao que estava)
        mostrarResposta(item);
        acompanharCliente(item);
        anunciar(item.erro ? 'A resposta falhou.' : 'Resposta pronta.');
        area.setAttribute('aria-busy', 'false');
        desenharTemas();
        campo.focus();
      }
    }
    /* =========================================================
       Ecrãs da zona de resposta
       ========================================================= */
    function tema() { return TEMAS.find(t => t.id === temaAtual); }

    function blocoSugestoes(titulo) {
      // só sugere o que ainda não tem resposta (as que deram erro ficam, para tentar de novo)
      const feitas = new Set(historico.filter(h => !h.erro).map(h => h.pergunta.toLowerCase()));
      const restantes = tema().perguntas.filter(p => !feitas.has(p.toLowerCase()));
      const perguntas = restantes.length ? restantes : tema().perguntas;   // todas feitas: volta a sugerir todas
      const sec = el('section', 'chat-sugestoes');
      const lista = el('div');
      perguntas.forEach(p => {
        const b = botaoEl('chat-chip', p, () => { if (!ocupado) perguntar(p); });
        b.disabled = ocupado;
        lista.append(b);
      });
      sec.append(el('h3', '', titulo), lista);
      return sec;
    }


    // Hora curta para a conversa: "10:25"
    const horaCurta = d => d ? d.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }) : '';

    // Mensagem de quem pergunta: balão à direita, avatar, hora e (opcional) Editar / Repetir
    function mensagemEu(texto, hora, acoes) {
      const linha = el('div', 'chat-msg chat-msg--eu');
      const corpo = el('div', 'chat-msg-corpo');
      const balao = el('p', 'chat-balao-eu');
      balao.append(el('span', 'so-leitor', 'Pergunta: '), texto);
      const meta = el('div', 'chat-msg-meta');
      if (acoes) meta.append(...acoes);
      if (hora) meta.append(el('span', '', horaCurta(hora)));
      corpo.append(balao, meta);
      const avatar = el('span', 'chat-avatar-eu');
      avatar.append(icone('M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM2.5 14.2c.7-2.7 2.9-4.2 5.5-4.2s4.8 1.5 5.5 4.2', 16));
      linha.append(corpo, avatar);
      return linha;
    }

    // Resposta do assistente: avatar à esquerda, nome por cima, hora por baixo
    function comAvatar(art, hora) {
      const linha = el('div', 'chat-resposta chat-msg chat-msg--agente');
      const corpo = el('div', 'chat-msg-corpo');
      const meta = el('div', 'chat-msg-meta');
      meta.append(el('span', '', 'Assistente de vendas' + (hora ? ' · ' + horaCurta(hora) : '')));
      corpo.append(art, meta);
      linha.append(corpo);
      return linha;
    }

    // Feedback na resposta: "Sim" ou "Não" + motivo. Envia para /feedback; se falhar, diz que falhou.
    // Só a avaliação e o motivo: a pergunta e a resposta podem ter nomes de clientes.
    function blocoFeedback(item) {
      const sec = el('div', 'chat-feedback');
      const motivos = el('div', 'chat-acoes');
      motivos.hidden = true;
      const enviar = async (avaliacao, motivo = '') => {
        sec.replaceChildren(el('span', '', 'A enviar a avaliação…'));
        try {
          const r = await fetch('/feedback', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ avaliacao, motivo })
          });
          if (!r.ok) throw new Error('HTTP ' + r.status);
          sec.replaceChildren(el('span', '', 'Obrigado. A avaliação foi enviada.'));
          anunciar('Avaliação enviada.');
        } catch {
          sec.replaceChildren(el('span', '', 'Não foi possível enviar a avaliação.'));
          anunciar('Não foi possível enviar a avaliação.');
        }
      };
      ['Números errados', 'Não respondeu à pergunta', 'Difícil de ler'].forEach(m => motivos.append(botaoEl('chat-chip', m, () => enviar('nao', m))));
      const nao = botaoEl('chat-acao', 'Não', () => { motivos.hidden = false; nao.setAttribute('aria-expanded', 'true'); motivos.querySelector('button').focus(); });
      nao.setAttribute('aria-expanded', 'false');
      sec.append(el('span', '', 'Esta resposta está certa?'), botaoEl('chat-acao', 'Sim', () => enviar('sim')), nao, motivos);
      return sec;
    }

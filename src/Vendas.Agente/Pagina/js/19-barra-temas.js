 /* ---------- Barra lateral ---------- */
    function desenharTemas() {
      temasEl.replaceChildren(...TEMAS.map(t => {
        const b = botaoEl('chat-tema', t.nome, () => {
          if (ocupado) return;
          temaAtual = t.id;
          desenharTemas(); mostrarInicio();
        });
        b.setAttribute('aria-pressed', String(t.id === temaAtual));
        b.disabled = ocupado;
        return b;
      }));
    }
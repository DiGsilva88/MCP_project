

/* ---------- Eventos ---------- */
    form.addEventListener('submit', e => {
      e.preventDefault();
      const texto = campo.value.replace(/\s+/g, ' ').trim();
      if (!texto || ocupado) return;
      campo.value = '';
      perguntar(texto);
    });

    desenharTemas();
    mostrarInicio();
    carregarResumo();   // o Resumo carrega logo ao abrir (por trás das boas-vindas)

    painelEl.querySelector('.flow-ini input')?.focus({ preventScroll: true });   // em janelas baixas, o foco deslocava o ecrã e cortava o logótipo
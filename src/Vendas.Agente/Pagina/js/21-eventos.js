

/* ---------- Eventos ---------- */
    form.addEventListener('submit', e => {
      e.preventDefault();
      const texto = campo.value.replace(/\s+/g, ' ').trim();
      if (!texto || ocupado) return;
      campo.value = '';
      perguntar(texto);
    });

    desenharTemas();
    // Recarregar volta ao lugar onde se estava (como "Ir direto ao painel de vendas", já na vista e com os filtros de antes)
    try {
      const l = JSON.parse(sessionStorage.getItem(LUGAR));
      if (l && !l.inicio && SECOES.some(s => s[0] === l.secao)) {
        Object.assign(resumoEstado, { periodo: l.periodo || 'AnoAtual', zona: l.zona || null, cliente: l.cliente || null });
        boasVindasAberta = false; secao = l.secao; modoInicio = 'dados';
      }
    } catch { }
    mostrarInicio();
    carregarResumo();   // o Resumo carrega logo ao abrir (por trás das boas-vindas)

    painelEl.querySelector('.flow-ini input')?.focus({ preventScroll: true });   // em janelas baixas, o foco deslocava o ecrã e cortava o logótipo
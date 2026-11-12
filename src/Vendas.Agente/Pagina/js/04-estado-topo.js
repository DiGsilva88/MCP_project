    /* ---------- Estado no topo ---------- */
    
    function mostrarEstado(tipo) {   
      estado.className = 'chat-estado' + (tipo === 'pronto' ? '' : ' chat-estado--' + tipo);
      estadoTexto.textContent = { pronto: 'Pronto', ocupado: 'A consultar', desligado: 'Sem ligação' }[tipo];
    }
    function bloquear(sim) {
      ocupado = sim;
      campo.disabled = botao.disabled = sim;
      document.querySelectorAll('.chat-temas button, .chat-chip').forEach(b => b.disabled = sim);
      if (sim) mostrarEstado('ocupado');
    }

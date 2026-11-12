    /* ---------- Elementos e estado ---------- */
    const form = document.getElementById('form');
    const campo = document.getElementById('pergunta');
    const botao = form.querySelector('.chat-enviar');
    const estado = document.getElementById('estado');
    const estadoTexto = document.getElementById('estado-texto');
    const area = document.getElementById('area');
    const temasEl = document.getElementById('temas');

    // Mensagem curta para o leitor de ecrã ("Copiado", "Resposta pronta"), sem ler a resposta inteira
    const anuncio = document.getElementById('anuncio');
    function anunciar(texto) {
      anuncio.textContent = '';
      setTimeout(() => { anuncio.textContent = texto; }, 50);   // limpar e voltar a escrever: repete o anúncio
    }
    // Escreve a pergunta no campo para a pessoa rever; não envia
    function prepararPergunta(texto) {
      campo.value = texto;
      campo.focus();
      anunciar('Pergunta escrita no campo. Carregue em Enter para enviar.');
    }

    let temaAtual = TEMAS[0].id;
    const historico = [];   
    let ocupado = false;

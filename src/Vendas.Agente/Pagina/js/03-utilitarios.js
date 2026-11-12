    /* ---------- Utilitários ---------- */
    function el(tag, classe, texto) {
      const e = document.createElement(tag);
      if (classe) e.className = classe;
      if (texto) e.textContent = texto;   
      return e;
    }
    function botaoEl(classe, texto, aoClicar) {
      const b = el('button', classe, texto);
      b.type = 'button';
      b.addEventListener('click', aoClicar);
      return b;
    }
    const maiuscula = s => s ? s[0].toUpperCase() + s.slice(1) : s;
    const semPonto = s => s.replace(/\.\s*$/, '');
    const paraNumero = s => parseFloat(s.replace(/[^\d,-]/g, '').replace(',', '.'));
    // Ícones de linha (estilo do protótipo, desenhados aqui: a página não carrega recursos externos)
    const IC_LINHA = {
      resumo: 'M4 20V10M10 20V4M16 20v-7M21 20H3', clientes: 'M16 19a5 5 0 0 0-10 0M11 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7ZM17 4.5a3.5 3.5 0 0 1 0 6.5M21 19a5 5 0 0 0-3-4.6',
      faturacao: 'M5 3h14v18l-3-2-2 2-2-2-2 2-2-2-3 2ZM9 8h6M9 12h6', artigos: 'M12 3 3 7.5v9L12 21l9-4.5v-9ZM3 7.5 12 12l9-4.5M12 12v9',
      zonas: 'M12 21s-7-6.2-7-11.5a7 7 0 0 1 14 0C19 14.8 12 21 12 21ZM12 12a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z', previsoes: 'M3 17l6-6 4 4 8-8M15 7h6v6',
      alertas: 'M6 9a6 6 0 0 1 12 0c0 6 2.5 7 2.5 7h-17S6 15 6 9ZM10 20a2 2 0 0 0 4 0', ficha: 'M4 5h16v14H4ZM8 10a1.5 1.5 0 1 0 3 0 1.5 1.5 0 0 0-3 0M6.5 16a3 3 0 0 1 6 0M15 9h3M15 13h3',
      casa: 'M4 10.5 12 4l8 6.5V20H4ZM10 20v-6h4v6', euro: 'M17.5 6.5a6.5 6.5 0 1 0 0 11M4 10.5h9M4 13.5h9', documentos: 'M8 3h8l4 4v11H8ZM16 3v4h4M4 7v14h11',
      etiqueta: 'M3 12V3h9l9 9-9 9ZM7.5 7.5h.01', brilho: 'M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8ZM19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8Z',
      aviso: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18ZM12 7.5v5.5M12 16.5h.01', semCliente: 'M15 19a6 6 0 0 0-12 0M9 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7ZM16 11h6',
      calendarioX: 'M4 5h16v16H4ZM4 9h16M8 3v4M16 3v4M10 13l4 4M14 13l-4 4', relogio: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18ZM12 7v5l3 2',
      filtro: 'M3 5h18M6 12h12M10 19h4', descarregar: 'M12 4v11M7 10l5 5 5-5M5 20h14', calendarioOk: 'M4 5h16v16H4ZM4 9h16M8 3v4M16 3v4M9 15l2 2 4-4',
      conversa: 'M4 5h16v11H9l-5 4Z', seta: 'm9 6 6 6-6 6', abrir: 'M7 17 17 7M8 7h9v9', descida: 'M3 7l6 6 4-4 8 8M15 17h6v-6'
    };
    const svgIc = (n, t = 16) => `<svg width="${t}" height="${t}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${IC_LINHA[n]}"/></svg>`;
    const icone = (d, t = 16) => {
      const ns = 'http://www.w3.org/2000/svg';
      const svg = document.createElementNS(ns, 'svg');
      svg.setAttribute('width', t); svg.setAttribute('height', t); svg.setAttribute('viewBox', '0 0 16 16');
      svg.setAttribute('fill', 'none'); svg.setAttribute('stroke', 'currentColor'); svg.setAttribute('stroke-width', '1.6');
      svg.setAttribute('aria-hidden', 'true');
      const p = document.createElementNS(ns, 'path'); p.setAttribute('d', d); svg.append(p);
      return svg;
    };

    // Tira o markdown que o modelo às vezes acrescenta: "# Título" e "**negrito**".
    // O marcador de lista ("- ") fica: lerLista() precisa dele para saber o que é item.
    const limparMarkdown = l => l
      .replace(/^(\s*)#{1,6}\s+/, '$1')                         // "# Título"
      .replace(/(\*{1,3}|_{2,3})(\S(?:.*?\S)?)\1/g, '$2')       // **negrito**, *itálico*, ***os dois***, __negrito__
      .replace(/\*{2,}|_{2,}/g, '');                              // ** ou __ que ficaram sem par
    const MARCADOR = /^[-*•]\s+/;
    const semAcento = s => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
    // "ALDEIA DE PAIO PIRES" -> "Aldeia de Paio Pires" (só mexe em texto todo em maiúsculas)
    const nomeProprio = s => s !== s.toUpperCase() ? s
      : s.toLowerCase().replace(/(^|[\s-])(\p{L})/gu, (_, a, b) => a + b.toUpperCase())
         .replace(/ (Da|De|Do|Das|Dos|E) /g, w => w.toLowerCase());
    // O filtro dos clientes sem distrito é o valor especial "sem dados" do servidor
    const nomeDistrito = d => d === 'sem dados' ? 'Sem distrito na ficha' : nomeProprio(d);
    // Quantas letras é preciso mudar para passar de a para b ("montija" -> "montijo" = 1)
    function distancia(a, b) {
      const d = Array.from({ length: b.length + 1 }, (_, j) => j);
      for (let i = 1; i <= a.length; i++) {
        let diagonal = d[0];
        d[0] = i;
        for (let j = 1; j <= b.length; j++) {
          const cima = d[j];
          d[j] = Math.min(d[j] + 1, d[j - 1] + 1, diagonal + (a[i - 1] === b[j - 1] ? 0 : 1));
          diagonal = cima;
        }
      }
      return d[b.length];
    }

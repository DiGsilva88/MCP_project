    /* =========================================================
       Utilitários: elementos, texto e ícones
       ========================================================= */
    function el(tag, classe, texto) {
      const e = document.createElement(tag);
      if (classe) e.className = classe;
      if (texto) e.textContent = texto;
      return e;
    }
    // Texto para dentro de HTML: os nomes de clientes podem ter < ou &
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const maiuscula = s => s ? s[0].toUpperCase() + s.slice(1) : s;
    const semPonto = s => s.replace(/\.\s*$/, '');
    const paraNumero = s => fmt.numero(s);
    // Tira o markdown que o modelo às vezes acrescenta: "# Título" e "**negrito**". O marcador de lista fica (lerLista precisa dele).
    const limparMarkdown = l => l
      .replace(/^(\s*)#{1,6}\s+/, '$1')
      .replace(/(\*{1,3}|_{2,3})(\S(?:.*?\S)?)\1/g, '$2')
      .replace(/\*{2,}|_{2,}/g, '');
    const MARCADOR = /^[-*•]\s+/;
    const semAcento = s => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
    // "ALDEIA DE PAIO PIRES" -> "Aldeia de Paio Pires" (só mexe em texto todo em maiúsculas)
    const nomeProprio = s => !s || s !== s.toUpperCase() ? s
      : s.toLowerCase().replace(/(^|[\s-])(\p{L})/gu, (_, a, b) => a + b.toUpperCase()).replace(/ (Da|De|Do|Das|Dos|E) /g, w => w.toLowerCase());
    // O painel chama "zona" ao distrito da ficha; o valor especial "sem dados" do servidor são os clientes sem distrito
    const nomeZona = d => d === 'sem dados' ? 'Sem zona na ficha' : nomeProprio(d);
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

    // Uma só família de ícones: linha, 16px, traço 1,5, currentColor
    const ICONES = {
      procurar: '<circle cx="7" cy="7" r="4.5"/><path d="M10.5 10.5L14 14"/>',
      sino: '<path d="M4 11V7a4 4 0 018 0v4l1.5 1.5h-11z"/><path d="M6.5 14a1.5 1.5 0 003 0"/>',
      resumo: '<rect x="2.5" y="2.5" width="4.5" height="4.5"/><rect x="9" y="2.5" width="4.5" height="4.5"/><rect x="2.5" y="9" width="4.5" height="4.5"/><rect x="9" y="9" width="4.5" height="4.5"/>',
      clientes: '<circle cx="6" cy="5.5" r="2.5"/><path d="M1.5 13.5c0-2.5 2-4 4.5-4s4.5 1.5 4.5 4"/><path d="M10.5 3.2a2.5 2.5 0 010 4.6M12 9.8c1.6.5 2.5 1.8 2.5 3.7"/>',
      faturacao: '<path d="M4 1.5h5.5l3 3v10H4z"/><path d="M6 8h5M6 11h4"/>',
      artigos: '<path d="M2 5l6-3 6 3v6.5l-6 3-6-3z"/><path d="M2 5l6 3 6-3M8 8v6.5"/>',
      zonas: '<path d="M8 14.5s-4.5-4-4.5-7.5a4.5 4.5 0 019 0c0 3.5-4.5 7.5-4.5 7.5z"/><circle cx="8" cy="7" r="1.6"/>',
      previsoes: '<path d="M2 12l4-4 3 3 5-6"/><path d="M10 5h4v4"/>',
      alertas: '<circle cx="8" cy="8" r="6.2"/><path d="M8 4.8v3.8M8 11v.2"/>',
      guardados: '<path d="M4 2h8v12l-4-3-4 3z"/>',
      descida: '<path d="M2 4l4.5 4.5 3-3L14 10"/><path d="M14 6.5V10h-3.5"/>',
      semClientes: '<circle cx="6" cy="5.5" r="2.5"/><path d="M1.5 13.5c0-2.5 2-4 4.5-4s4.5 1.5 4.5 4"/><path d="M11 4l3 3M14 4l-3 3"/>',
      filtro: '<path d="M2 4h12M4.5 8h7M7 12h2"/>',
      exportar: '<path d="M8 2v8M4.5 7L8 10.5 11.5 7M2.5 13.5h11"/>',
      calendario: '<rect x="2" y="3" width="12" height="11" rx="1.5"/><path d="M2 6.5h12M5 1.5v3M11 1.5v3"/>',
      flow: '<path d="M8 1.5l1.6 4.9 4.9 1.6-4.9 1.6L8 14.5l-1.6-4.9L1.5 8l4.9-1.6z"/>',
      enviar: '<path d="M8 13V3M4 7l4-4 4 4"/>',
      fechar: '<path d="M3.5 3.5l9 9M12.5 3.5l-9 9"/>',
      mais: '<path d="M8 3v10M3 8h10"/>',
      copiar: '<rect x="5" y="5" width="9" height="9" rx="1.5"/><path d="M11 5V3.5A1.5 1.5 0 009.5 2h-6A1.5 1.5 0 002 3.5v6A1.5 1.5 0 003.5 11H5"/>',
      topo: '<path d="M8 13V3M4 7l4-4 4 4"/>',
      definicoes: '<circle cx="8" cy="8" r="2.2"/><path d="M8 1.5v2M8 12.5v2M1.5 8h2M12.5 8h2M3.4 3.4l1.4 1.4M11.2 11.2l1.4 1.4M3.4 12.6l1.4-1.4M11.2 4.8l1.4-1.4"/>',
      parar: '<rect x="4" y="4" width="8" height="8" rx="1"/>',
      info: '<circle cx="8" cy="8" r="6.2"/><path d="M8 7.2v4M8 5v.2"/>',
      abrir: '<path d="M6 3H3v10h10v-3M9 2.5h4.5V7M13.5 2.5L7.5 8.5"/>'
    };
    const ic = (nome, t = 16) => `<svg width="${t}" height="${t}" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICONES[nome] || ''}</svg>`;
    // Logótipo do Flow (a curva e o ponto), a azul
    const logo = (l = 34, a = 19) => `<svg width="${l}" height="${a}" viewBox="0 0 120 64" fill="none" aria-hidden="true"><circle cx="9" cy="8" r="6" fill="currentColor"/><path d="M9 22c0 20 6 32 16 32 11 0 13-22 16-36 3-14 10-16 18-16 14 0 24 22 44 28" stroke="currentColor" stroke-width="9" stroke-linecap="round"/></svg>`;

    // Mensagem curta para o leitor de ecrã ("Copiado", "Resposta pronta"), sem ler a resposta inteira
    function anunciar(texto) {
      const a = document.getElementById('anuncio');
      a.textContent = '';
      setTimeout(() => { a.textContent = texto; }, 50);
    }
    // Pedido ao servidor com o JSON da resposta; erro com o estado HTTP (a mensagem ao utilizador é sempre simples)
    async function pedirJson(url, opcoes) {
      const r = await fetch(url, opcoes);
      if (!r.ok) { const e = new Error('HTTP ' + r.status); e.estado = r.status; throw e; }
      return r.status === 204 ? null : r.json();
    }
    const ERRO_SIMPLES = 'Não foi possível carregar os dados. Tente outra vez; se continuar, avise a equipa técnica.';

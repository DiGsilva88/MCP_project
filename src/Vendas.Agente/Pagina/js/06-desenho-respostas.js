    /* =========================================================
       Desenho das respostas
       ========================================================= */
    // Colunas que só têm células vazias não vão para o Excel
    function semColunasVazias(linhas) {
      const manter = linhas[0].map((_, c) => linhas.slice(1).some(l => String(l[c] ?? '').trim() !== ''));
      return linhas.map(l => l.filter((_, c) => manter[c]));
    }

    // "15 373,55 €" -> "15373,55" e o cabeçalho ganha "(€)": o Excel em pt-PT lê como número e soma
    const EUROS = /^-?[\d\s  .]+(,\d+)?\s*€$/;
    function paraFolha(linhas) {
      const [cab, ...dados] = linhas;
      const emEuros = cab.map((_, c) => dados.some(l => l[c]) && dados.every(l => !l[c] || EUROS.test(String(l[c]).trim())));
      return [
        cab.map((h, c) => emEuros[c] && !String(h).includes('€') ? h + ' (€)' : h),
        ...dados.map(l => l.map((v, c) => emEuros[c] && v ? String(v).replace(/[^\d,-]/g, '') : v))
      ];
    }

    async function copiarTexto(texto) {
      try { await navigator.clipboard.writeText(texto); return true; } catch { /* segue para o plano B */ }
      // Sem localhost nem HTTPS não há navigator.clipboard: copia por uma caixa de texto escondida
      const antes = document.activeElement;
      const caixa = el('textarea');
      caixa.value = texto;
      caixa.setAttribute('readonly', '');
      caixa.style.position = 'fixed'; caixa.style.opacity = '0';
      document.body.append(caixa);
      caixa.select();
      let ok = false;
      try { ok = document.execCommand('copy'); } catch { ok = false; }
      caixa.remove();
      if (antes) antes.focus();
      return ok;
    }

    // conteudo: texto simples, ou linhas [[cabeçalho...], [valores...], ...]
    function botaoCopiar(conteudo) {
      // Tabulações: cola em colunas no Excel
      const texto = typeof conteudo === 'string' ? conteudo : paraFolha(conteudo).map(l => l.join('\t')).join('\n');
      const b = botaoEl('chat-acao', 'Copiar', async () => {
        b.textContent = await copiarTexto(texto) ? 'Copiado' : 'Não foi possível copiar';
        anunciar(b.textContent);
        setTimeout(() => { b.textContent = 'Copiar'; }, 2000);
      });
      return b;
    }

    function textoCsv(linhas) {
      // ";" e BOM: o Excel em pt-PT abre em colunas e com acentos certos
      // Texto começado por = + @ (ou - sem número) o Excel lê como fórmula: o ' obriga a ficar texto
      const seguro = v => /^[=+@\t\r]|^-(?!\d)/.test(v) ? "'" + v : v;
      const campo = v => (v = seguro(v), /[;"\n]/.test(v) ? '"' + v.replace(/"/g, '""') + '"' : v);
      const csv = '﻿' + paraFolha(linhas).map(l => l.map(v => campo(String(v ?? ''))).join(';')).join('\r\n');
      return csv;
    }

    // "Clientes por Zona" -> clientes-por-zona-2026-10-28.csv
    const nomeFicheiro = (titulo, extensao) =>
      `${semAcento(titulo || '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 50) || 'vendas'}-${new Date().toISOString().slice(0, 10)}.${extensao}`;

    function descarregarFicheiro(conteudo, tipo, nome) {
      const a = el('a');
      a.href = URL.createObjectURL(new Blob([conteudo], { type: tipo }));
      a.download = nome;
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);   // revogar logo pode cancelar o download
    }

    function descarregarCsv(linhas, titulo) {
      descarregarFicheiro(textoCsv(linhas), 'text/csv;charset=utf-8', nomeFicheiro(titulo, 'csv'));
      anunciar('Ficheiro CSV descarregado.');
    }

    const emBase64 = texto => { let s = ''; for (const b of new TextEncoder().encode(texto)) s += String.fromCharCode(b); return btoa(s); };

    // O mailto não aceita anexos e não abre nada sem programa de email definido (ex.: servidores RDS):
    // este .eml com X-Unsent abre no Outlook como rascunho. anexo (opcional): { nome, tipo, conteudo }.
    function descarregarRascunho(assunto, texto, nomeEml, anexo) {
      const limite = 'flow-' + Date.now();
      const quebrar = b64 => b64.replace(/.{76}(?=.)/g, '$&\r\n');
      const parte = (tipo, conteudo, extra = []) => [`--${limite}`, `Content-Type: ${tipo}`, ...extra, 'Content-Transfer-Encoding: base64', '', quebrar(emBase64(conteudo))];
      // uma palavra codificada tem no máximo 75 caracteres
      const eml = ['X-Unsent: 1', 'To: ', `Subject: =?UTF-8?B?${emBase64(assunto.slice(0, 40))}?=`, 'MIME-Version: 1.0', `Content-Type: multipart/mixed; boundary="${limite}"`, '',
        ...parte('text/plain; charset="utf-8"', texto),
        ...(anexo ? parte(`${anexo.tipo}; charset="utf-8"; name="${anexo.nome}"`, anexo.conteudo, [`Content-Disposition: attachment; filename="${anexo.nome}"`]) : []),
        `--${limite}--`, ''].join('\r\n');
      descarregarFicheiro(eml, 'message/rfc822', nomeEml);
      anunciar('Rascunho de email descarregado. Abra o ficheiro para o enviar.');
    }

    function enviarCsvPorEmail(linhas, titulo) {
      descarregarRascunho(`Exportação: ${titulo || 'vendas'}`, 'Segue em anexo o ficheiro exportado do Inforap Flow.', nomeFicheiro(titulo, 'eml'),
        { nome: nomeFicheiro(titulo, 'csv'), tipo: 'text/csv', conteudo: textoCsv(linhas) });
    }

    function botaoExportar(linhas, titulo) {
      const b = botaoEl('chat-acao', 'Exportar CSV', () => descarregarCsv(linhas, titulo));
      b.dataset.exportar = '';   // o "Exportar dados" do topo usa o primeiro destes no separador aberto
      b.dadosCsv = [linhas, titulo];   // e a opção de email usa os mesmos dados
      return b;
    }

    /* =========================================================
       Exportar: Excel (.xlsx), PDF (impressão) e CSV, da vista atual com o período e os filtros.
       Tudo no browser: o .xlsx é escrito à mão (zip sem compressão), sem bibliotecas nem internet.
       ========================================================= */
    // "15 373,55 €" e "6,3%" -> número; o resto fica texto
    const NUMERO_TEXTO = /^[−-]?[\d\s  .]+(,\d+)?\s*(€|%)?$/;
    const celulaNumero = v => typeof v === 'number' ? (isFinite(v) ? v : null) : NUMERO_TEXTO.test(String(v ?? '').trim()) && /\d/.test(v) ? fmt.numero(v) : null;

    const nomeFicheiro = (titulo, extensao) =>
      `${semAcento(titulo || '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 50) || 'vendas'}-${new Date().toISOString().slice(0, 10)}.${extensao}`;

    function descarregarFicheiro(conteudo, tipo, nome) {
      const a = el('a');
      a.href = URL.createObjectURL(new Blob([conteudo], { type: tipo }));
      a.download = nome;
      document.body.append(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);   // revogar logo pode cancelar o download
    }

    // CSV para o Excel em pt-PT: ";" e BOM; números com vírgula; texto começado por = + @ fica texto
    function textoCsv(linhas) {
      const seguro = v => /^[=+@\t\r]|^-(?!\d)/.test(v) ? "'" + v : v;
      const campo = v => {
        const n = celulaNumero(v);
        const t = n != null ? String(Math.round(n * 100) / 100).replace('.', ',') : seguro(String(v ?? ''));
        return /[;"\n]/.test(t) ? '"' + t.replace(/"/g, '""') + '"' : t;
      };
      return '﻿' + linhas.map(l => l.map(campo).join(';')).join('\r\n');
    }

    /* ---------- .xlsx mínimo: uma folha, números como números ---------- */
    const CRC = (() => { const t = new Uint32Array(256); for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } return t; })();
    const crc32 = b => { let c = 0xFFFFFFFF; for (const x of b) c = CRC[(c ^ x) & 0xFF] ^ (c >>> 8); return (c ^ 0xFFFFFFFF) >>> 0; };
    // Zip sem compressão ("store"): suficiente para um .xlsx pequeno
    function zip(ficheiros) {
      const enc = new TextEncoder(), partes = [], central = [];
      let pos = 0;
      for (const { nome, texto } of ficheiros) {
        const n = enc.encode(nome), d = enc.encode(texto), crc = crc32(d);
        const loc = new DataView(new ArrayBuffer(30));
        [[0, 0x04034b50, 4], [4, 20, 2], [6, 0x0800, 2], [8, 0, 2], [10, 0, 2], [12, 0x21, 2], [14, crc, 4], [18, d.length, 4], [22, d.length, 4], [26, n.length, 2], [28, 0, 2]]
          .forEach(([o, v, t]) => t === 4 ? loc.setUint32(o, v, true) : loc.setUint16(o, v, true));
        const cen = new DataView(new ArrayBuffer(46));
        [[0, 0x02014b50, 4], [4, 20, 2], [6, 20, 2], [8, 0x0800, 2], [10, 0, 2], [12, 0, 2], [14, 0x21, 2], [16, crc, 4], [20, d.length, 4], [24, d.length, 4], [28, n.length, 2], [30, 0, 2], [32, 0, 2], [34, 0, 2], [36, 0, 2], [38, 0, 4], [42, pos, 4]]
          .forEach(([o, v, t]) => t === 4 ? cen.setUint32(o, v, true) : cen.setUint16(o, v, true));
        partes.push(new Uint8Array(loc.buffer), n, d);
        central.push(new Uint8Array(cen.buffer), n);
        pos += 30 + n.length + d.length;
      }
      const tamCentral = central.reduce((s, p) => s + p.length, 0), fim = new DataView(new ArrayBuffer(22));
      [[0, 0x06054b50, 4], [8, ficheiros.length, 2], [10, ficheiros.length, 2], [12, tamCentral, 4], [16, pos, 4]]
        .forEach(([o, v, t]) => t === 4 ? fim.setUint32(o, v, true) : fim.setUint16(o, v, true));
      return new Blob([...partes, ...central, new Uint8Array(fim.buffer)]);
    }
    function folhaXlsx(linhas, titulo) {
      const x = s => String(s ?? '').replace(/[<>&"]/g, c => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', '"': '&quot;' }[c])).replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/g, '');
      const col = i => { let s = ''; for (i++; i; i = Math.floor((i - 1) / 26)) s = String.fromCharCode(65 + (i - 1) % 26) + s; return s; };
      const linhasXml = linhas.map((l, r) => `<row r="${r + 1}">${l.map((v, c) => {
        const n = r ? celulaNumero(v) : null, ref = col(c) + (r + 1);
        return n != null ? `<c r="${ref}"><v>${n}</v></c>` : `<c r="${ref}" t="inlineStr"><is><t xml:space="preserve">${x(v)}</t></is></c>`;
      }).join('')}</row>`).join('');
      const nome = x((titulo || 'Vendas').replace(/[\\/?*[\]:]/g, ' ').slice(0, 31));
      return zip([
        { nome: '[Content_Types].xml', texto: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>' },
        { nome: '_rels/.rels', texto: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>' },
        { nome: 'xl/workbook.xml', texto: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="${nome}" sheetId="1" r:id="rId1"/></sheets></workbook>` },
        { nome: 'xl/_rels/workbook.xml.rels', texto: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>' },
        { nome: 'xl/worksheets/sheet1.xml', texto: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${linhasXml}</sheetData></worksheet>` }
      ]);
    }

    // exportacao: { titulo, linhas: [[cabeçalho], ...] }. PDF: imprime a vista (o CSS de impressão tira menus e botões).
    function exportar(formato, { titulo, linhas }, imprimirDrawer = false) {
      if (formato === 'pdf') {
        document.body.classList.toggle('a-imprimir-drawer', imprimirDrawer);
        window.print();
        document.body.classList.remove('a-imprimir-drawer');
        return `${titulo} pronto a guardar em PDF na janela de impressão.`;
      }
      if (formato === 'csv') descarregarFicheiro(textoCsv(linhas), 'text/csv;charset=utf-8', nomeFicheiro(titulo, 'csv'));
      else descarregarFicheiro(folhaXlsx(linhas, titulo), 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', nomeFicheiro(titulo, 'xlsx'));
      return `${titulo} exportado em ${formato === 'csv' ? 'CSV' : 'Excel'}, com o período e os filtros atuais.`;
    }

    async function copiarTexto(texto) {
      try { await navigator.clipboard.writeText(texto); return true; } catch { /* sem HTTPS nem localhost: plano B */ }
      const antes = document.activeElement, caixa = el('textarea');
      caixa.value = texto;
      caixa.setAttribute('readonly', '');
      caixa.style.position = 'fixed'; caixa.style.opacity = '0';
      document.body.append(caixa);
      caixa.select();
      let ok = false;
      try { ok = document.execCommand('copy'); } catch { ok = false; }
      caixa.remove();
      antes?.focus();
      return ok;
    }
    if (typeof module === 'object') module.exports = { textoCsv, celulaNumero, crc32 };

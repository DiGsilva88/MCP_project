    /* =========================================================
       Leitura das respostas
       ========================================================= */

    // A) Bloco CSV: "Zona, clientes, percentagem" + linhas. Vírgula+espaço separa colunas.
    // shortcut: lê a tabela do texto; quando o agente devolver colunas e linhas em JSON, trocar por isso.
    // sep = ', ' (vírgula e espaço, o formato antigo) ou ',' (CSV sem espaços: "Balcão,103,27.2")
    function partir(linha, sep = ', ') {
      const campos = [];
      let atualC = '', aspas = false;
      for (let i = 0; i < linha.length; i++) {
        const c = linha[i];
        if (c === '"') aspas = !aspas;
        else if (!aspas && linha.startsWith(sep, i)) { campos.push(atualC.trim()); atualC = ''; i += sep.length - 1; }
        else atualC += c;
      }
      campos.push(atualC.trim());
      return campos;
    }
    const eNumero = s => /^-?\d[\d.]*(,\d+)?%?$/.test(s);
    function lerTabela(texto) {
      const linhas = texto.trim().split('\n').map(l => l.trim());
      for (let i = 0; i < linhas.length; i++) {
        const l = linhas[i];
        // Cabeçalho: tem vírgulas e não é item de lista, item numerado nem frase com ":"
        if (!l.includes(',') || MARCADOR.test(l) || /^\d+\.\s/.test(l) || /:\s/.test(l)) continue;
        // Com "Zona, clientes" separa em ", "; com "Zona,clientes" separa em ","
        const sep = l.includes(', ') ? ', ' : ',';
        const n = partir(l, sep).length;
        if (n < 2) continue;
        if (!partir(l, sep).every(c => /^\p{L}[\p{L}\d _.%º/-]{0,30}$/u.test(c))) continue;   // cabeçalho = nomes de colunas curtos
        const bloco = [];
        let j = i;
        for (; j < linhas.length; j++) {
          if (!linhas[j]) continue;                 // linhas em branco no meio do CSV
          const campos = partir(linhas[j], sep);
          if (campos.length !== n) break;
          bloco.push(campos);
        }
        if (bloco.length < 2) continue;            // cabeçalho + pelo menos 1 linha (1 linha = ficha)
        const [cabecalho, ...dados] = bloco;
        const numericas = cabecalho.map((_, c) => dados.every(d => eNumero(d[c])));
        return { antes: linhas.slice(0, i).join('\n').trim(), depois: linhas.slice(j).join('\n').trim(), cabecalho, linhas: dados, numericas };
      }
      return null;
    }

    // B) Texto com título, factos, avisos e lista numerada (o formato atual do agente)
    function lerItem(pos, s) {
      // Travessão separa nome e valor: o último seguido de um número. "· 2025: –" (não vendeu no ano anterior)
      // também tem travessão; com lastIndexOf partia a linha aí e o valor ficava NaN (lista sem barras).
      const seps = [...s.matchAll(/ [–-] (?=[-−]?\d)/g)];
      let i = seps.length ? seps[seps.length - 1].index : s.lastIndexOf(' – ');   // sem número: "– última compra a…", "– nunca comprou"
      if (i < 0) i = s.lastIndexOf(' - ');
      const esq = i < 0 ? s : s.slice(0, i);
      let dir = i < 0 ? '' : s.slice(i + 3).trim(), media = '';
      // "… · média 73,40 €" no fim (meses e trimestres): valor médio por documento
      const med = dir.match(/^(.*?)\s*·\s*média\s+(.+)$/);
      if (med) { dir = med[1]; media = med[2]; }
      let nome = esq.trim(), codigo = '';
      const c = esq.match(/^(.*\S)\s*\(([^()]+)\)\s*$/);   // último parêntesis = código
      if (c) { nome = c[1]; codigo = c[2]; }
      let sub = '';
      const par = nome.match(/^(.+?):\s+(.+)$/);   // "FERNAO FERRO: CARV" (melhor marca em cada localidade)
      if (par) { nome = par[1]; sub = par[2]; }
      let valor = dir, pct = '', rotulo = '', anterior = null, delta = null, novo = null;
      // "255 348,78 € · 2025: 265 396,36 € (-3,8%)": valor, ano anterior e variação.
      // Sem "(%)" quando o ano anterior foi 0 ou negativo; "· 2025: –" quando o artigo não vendeu no ano anterior.
      const cmp = dir.match(/^(.*?)\s*·\s*(\d{4}|um ano antes):\s*(.*?)(?:\s*\(([+\-−]?[\d.,]+)\s*%\))?$/);
      if (cmp) {
        valor = cmp[1];
        if (!/^[–—-]?$/.test(cmp[3])) anterior = { ano: cmp[2], valor: cmp[3] };
        else novo = cmp[2];   // sem vendas no ano anterior: não há %, mas há que dizê-lo (senão a linha fica sem indicador)
        if (cmp[4]) delta = parseFloat(cmp[4].replace('−', '-').replace(',', '.'));
      } else {
        const p = dir.match(/^(.*\S)\s*\(([\d.,]+\s*%)(?:\s+([^)]+))?\)$/);  // "(1,9%)" ou "(10,6% da localidade)"
        if (p) { valor = p[1]; pct = p[2] + (p[3] ? ' ' + p[3] : ''); }
        const d = valor.match(/^(.*?)\s*(\d{2}-\d{2}-\d{4})$/); // "última compra a 23-12-2025"
        if (d && d[1]) { rotulo = d[1].replace(/\s+(a|em)$/i, ''); valor = d[2]; }
      }
      // "1 234 unidades (255 348,78 €)" (ordenar=Quantidade): as unidades são o valor, os euros vão por baixo.
      // Sem isto o paraNumero juntava os dois números (1 234 255 348,78).
      const un = valor.match(/^(.*\d)\s+unidades\s*\((.+)\)$/);
      if (un) { valor = un[1] + ' unidades'; pct = un[2]; }
      const num = un ? paraNumero(un[1]) : /^-?[\d\s.\u00a0\u202f]+(,\d+)?\s*€?$/.test(valor) ? paraNumero(valor) : NaN;
      return { pos, nome, codigo, sub, valor, pct, rotulo, num, anterior, delta, novo, media, unidades: !!un };
    }
    function lerLista(texto) {
      const r = { titulo: '', periodo: '', resumo: '', factos: [], avisos: [], itens: [], paragrafos: [], nota: '' };
      const ls = texto.split('\n').map(l => limparMarkdown(l.trim())).filter(Boolean);
      const eFacto = s => /^[^:]{3,40}:\s/.test(s);   // "Rótulo: valor"
      let grupo = '';                                  // "Clientes em Odivelas:" por cima de uma lista
      ls.forEach((linha, i) => {
        const marcador = MARCADOR.test(linha);         // a linha começa por "- ", "* " ou "• "
        const l = linha.replace(MARCADOR, '');
        const seguinte = ls[i + 1] || '';
        let m;
        if ((m = l.match(/^(\d+)\.\s+(.+)$/))) r.itens.push({ ...lerItem(m[1], m[2]), grupo });
        else if (marcador && !eFacto(l)) r.itens.push({ pos: '', nome: l, codigo: '', valor: '', pct: '', rotulo: '', num: NaN, grupo });
        else if (!marcador && l.endsWith(':') && MARCADOR.test(seguinte) && !eFacto(seguinte.replace(MARCADOR, ''))) grupo = l.slice(0, -1);
        else if ((m = l.match(/^Aten[çc][ãa]o:\s*(.+)$/i))) r.avisos.push(m[1]);
        else if ((m = l.match(/^Mostr[oa]\s+(.+)$/i))) r.nota = 'Mostra ' + semPonto(m[1]);
        else if (i === 0 && !marcador && !/^lista completa/i.test(l) && (m = l.match(/^(.+?):\s+(.+)$/)) && /\s/.test(m[1]) && !/[.?!]\s/.test(m[1])) { r.titulo = m[1]; r.resumo = m[2]; }
        else if ((m = l.match(/^([^:]{3,40}):\s+(.+)$/))) r.factos.push([m[1], semPonto(m[2])]);
        else r.paragrafos.push(l);
      });
      const p = r.titulo.match(/\s*\((\d{2}-\d{2}-\d{4} a \d{2}-\d{2}-\d{4})\)/);
      if (p) { r.periodo = p[1]; r.titulo = r.titulo.replace(p[0], ''); }
      return r;
    }

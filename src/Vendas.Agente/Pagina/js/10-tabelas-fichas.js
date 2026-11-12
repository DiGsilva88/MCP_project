    // Número de uma célula: "27.2" e "27,2" valem o mesmo; "1 234,50 €" também funciona
    const numeroCelula = s => /^-?\d+\.\d+%?$/.test(s) ? parseFloat(s) : paraNumero(s);
    // Escreve um total com o formato das células da coluna ("27.2", "97.6%", "1 234,50 €")
    function formatarComo(amostra, n) {
      const casas = ((amostra.match(/[.,](\d+)\D*$/) || [])[1] || '').length;
      if (/€|\d\s\d{3}/.test(amostra)) return n.toLocaleString('pt-PT', { minimumFractionDigits: casas, maximumFractionDigits: casas }) + (/€/.test(amostra) ? ' €' : '');
      return n.toFixed(casas).replace('.', amostra.includes(',') ? ',' : '.') + (/%$/.test(amostra) ? '%' : '');
    }

    function folhaTabela(t, pergunta = '') {
      const art = el('article', 'chat-folha chat-folha--painel');
      // Sem markdown e sem "Lista completa: …", que repete o total
      const limpo = s => s.split('\n').map(l => limparMarkdown(l.trim()).replace(MARCADOR, ''))
        .filter(l => l && !/^lista completa/i.test(l)).join('\n');
      const antes = limpo(t.antes), depois = limpo(t.depois);
      const [titulo, resumo] = antes.split(/:\s+(.*)/s);
      const cab = el('div', 'chat-folha-cab');
      art.append(cab);
      const leitura = lerFiltros(titulo);
      if (titulo) cab.append(el('h2', 'chat-folha-titulo', maiuscula(leitura.titulo)));   // sem toLowerCase: mantém "Paulo Afonso"
      etiquetasLeitura(cab, leitura.filtros, '', pergunta);

      // Coluna de texto com o mesmo valor em todas as linhas (ex.: Vendedor): sai da tabela e vai para o cabeçalho
      const fixa = t.cabecalho.map((_, c) => c > 0 && !t.numericas[c] && t.linhas.length > 1 && t.linhas.every(l => l[c] === t.linhas[0][c]));
      const jaNoTitulo = nome => leitura.filtros.some(([k]) => semAcento(k) === semAcento(nome));   // "Vendedor" já está nas etiquetas
      if (fixa.some((f, c) => f && !jaNoTitulo(t.cabecalho[c]))) {
        const filtros = el('div', 'chat-filtros');
        t.cabecalho.forEach((nome, c) => {
          if (!fixa[c] || jaNoTitulo(nome)) return;
          const f = el('span', 'chat-filtro');
          f.append(el('span', '', maiuscula(nome)), t.linhas[0][c]);
          filtros.append(f);
        });
        cab.append(filtros);
      }
      if (resumo) desenharKpis({ resumo, factos: [] }, cab, art);   // "379 clientes em 9 grupos" num cartão

      const mostrar = (_, c) => !fixa[c];
      const cabecalho = t.cabecalho.filter(mostrar);
      const numericas = t.numericas.filter(mostrar);
      // "27.2" (ponto do SQL) passa a "27,2", como no resto da página
      const linhas = t.linhas.map(l => l.filter(mostrar).map((v, c) => numericas[c] && /^-?\d+\.\d{1,2}$/.test(v) ? v.replace('.', ',') : v));
      const estado = { col: -1, dir: -1, filtro: '' };   // col -1: a ordem em que a resposta veio

      // Filtro só em tabelas compridas
      const contagem = el('span', '');
      contagem.setAttribute('role', 'status');
      if (linhas.length > 8) {
        const filtro = el('label', 'chat-filtrar');
        const caixa = el('input');
        caixa.type = 'search';
        caixa.autocomplete = 'off';
        caixa.placeholder = maiuscula(cabecalho[0] || 'texto');
        caixa.addEventListener('input', () => { estado.filtro = caixa.value; desenharLinhas(); });
        filtro.append(el('span', '', 'Filtrar'), caixa, contagem);
        art.append(filtro);
      }

      const barra = numericas.indexOf(true);
      const maximo = barra < 0 ? 0 : Math.max(...linhas.map(l => numeroCelula(l[barra])));
      const tabela = el('table', 'chat-tabela');
      const topo = tabela.createTHead().insertRow();
      const cabecalhos = cabecalho.map((nome, c) => {
        const th = el('th', numericas[c] ? 'chat-num' : '');
        th.scope = 'col';
        const b = botaoEl('chat-ordenar', maiuscula(nome), () => {
          // 1.º clique: números do maior para o menor, texto de A a Z; 2.º clique inverte
          estado.dir = estado.col === c ? -estado.dir : (numericas[c] ? -1 : 1);
          estado.col = c;
          desenharLinhas();
        });
        b.append(el('span', 'chat-seta'));
        th.append(b);
        topo.append(th);
        if (c === 0 && barra >= 0) { const v = el('th'); v.append(el('span', 'so-leitor', 'Proporção')); topo.append(v); }
        return th;
      });
      const corpo = tabela.createTBody();
      const total = linhas.length > 1 && numericas.some(Boolean) ? tabela.createTFoot().insertRow() : null;

      function desenharLinhas() {
        cabecalhos.forEach((th, c) => {
          if (c === estado.col) th.setAttribute('aria-sort', estado.dir < 0 ? 'descending' : 'ascending');
          else th.removeAttribute('aria-sort');
          th.querySelector('.chat-seta').textContent = c === estado.col ? (estado.dir < 0 ? ' ↓' : ' ↑') : '';
        });
        const f = semAcento(estado.filtro.trim());
        let visiveis = linhas.filter(l => !f || l.some((v, c) => !numericas[c] && semAcento(v).includes(f)));
        if (estado.col >= 0) {
          const c = estado.col;
          visiveis = [...visiveis].sort((a, b) => (numericas[c] ? numeroCelula(a[c]) - numeroCelula(b[c]) : a[c].localeCompare(b[c], 'pt')) * estado.dir);
        }
        corpo.replaceChildren();
        for (const linha of visiveis) {
          const tr = corpo.insertRow();
          linha.forEach((valor, c) => {
            tr.append(el('td', numericas[c] ? 'chat-num' : '', valor));
            if (c === 0 && barra >= 0) {
              const td = el('td'), fundo = el('div', 'chat-barra'), cheio = el('span');
              cheio.style.width = Math.max(3, numeroCelula(linha[barra]) / maximo * 100) + '%';
              fundo.setAttribute('aria-hidden', 'true');
              fundo.append(cheio); td.append(fundo); tr.append(td);
            }
          });
        }
        if (!visiveis.length) {
          const td = el('td', 'chat-tabela-vazia', 'Nenhuma linha com esse texto.');
          td.colSpan = cabecalho.length + (barra >= 0 ? 1 : 0);
          corpo.insertRow().append(td);
        }
        if (total) {
          total.replaceChildren();
          cabecalho.forEach((_, c) => {
            const soma = numericas[c] ? visiveis.reduce((s, l) => s + numeroCelula(l[c]), 0) : null;
            total.append(el('td', numericas[c] ? 'chat-num' : '', c === 0 ? 'Total' : soma == null ? '' : formatarComo(linhas[0][c], soma)));
            if (c === 0 && barra >= 0) total.append(el('td'));
          });
        }
        contagem.textContent = estado.filtro ? `${visiveis.length} de ${linhas.length} linhas` : '';
      }
      desenharLinhas();

      const bloco = el('div', 'chat-bloco');
      bloco.append(tabela);
      if (depois) bloco.append(el('p', 'chat-texto', depois));
      art.append(bloco);
      // Exporta a tabela completa, com as colunas fixas incluídas
      rodapeDados(art, [t.cabecalho, ...t.linhas], titulo);
      return art;
    }

    // "Nenhuma correspondência. … Localidade: FERNAO FERRO (41); Corroios (21); … (mostrados 30 de 101)"
    function lerSemResultado(texto) {
      // "Não foi possível encontrar dados para a actividade "Balcão". Os valores disponíveis para actividade são: Empresas (195); …"
      const naoEncontrou = texto.match(/n[ãa]o foi poss[íi]vel encontrar[^"“«]*?\bpara\s+(?:a|o)\s+([\p{L} ]+?)\s+["“«]([^"”»]+)["”»]/iu);
      if (naoEncontrou) {
        const resto = texto.slice(naoEncontrou.index + naoEncontrou[0].length).replace(/^[^:]*:/, '');
        // Separa por ";" e tira o número do fim: "JOGADOR (EMPREGADO) (29)" -> ["JOGADOR (EMPREGADO)", 29]
        const opcoes = resto.split(';').map(p => p.trim().match(/^(.+?)\s*\((\d+)\)\s*\.?$/)).filter(Boolean).map(x => [x[1], +x[2]]);
        if (opcoes.length) return { campo: naoEncontrou[1].trim(), errado: naoEncontrou[2].trim(), opcoes, mostrados: opcoes.length, total: 0 };
      }
      if (!/nenhuma correspond[êe]ncia/i.test(texto)) return null;
      const m = texto.match(/([A-Za-zÀ-ú][A-Za-zÀ-ú ]*?):\s*[^:()]+\(\d+\)/);   // "Localidade: X (41)"
      if (!m) return null;
      const resto = texto.slice(m.index + m[1].length + 1);
      const opcoes = [...resto.matchAll(/([^;()]+?)\s*\((\d+)\)/g)].map(x => [x[1].trim(), +x[2]]);
      const contagem = texto.match(/mostrados\s+(\d+)\s+de\s+(\d+)/i);
      return { campo: m[1].trim(), opcoes, mostrados: contagem ? +contagem[1] : opcoes.length, total: contagem ? +contagem[2] : 0 };
    }

    // "Sem vendas: Trimestre atual (01-10-2026 a 29-10-2026)" + "Atenção: …": o período não tem vendas
    function lerSemVendas(texto) {
      const linhas = texto.split('\n').map(l => l.trim()).filter(Boolean);
      const m = (linhas[0] || '').match(/^sem vendas\b(?:\s+registadas)?[\s:,-]*(?:n[oa]s?\s+|em\s+)?(.*?)\.?$/i);
      if (!m || linhas.length > 3) return null;
      const aviso = linhas.find(l => /^aten[çc][ãa]o\s*:/i.test(l));
      return { periodo: m[1], aviso: aviso ? aviso.replace(/^aten[çc][ãa]o\s*:\s*/i, '') : '' };
    }

    function folhaSemVendas(s, pergunta) {
      const art = el('article', 'chat-folha');
      const cab = el('div', 'chat-folha-cab');
      cab.append(el('h2', 'chat-vazio-titulo', 'Sem vendas neste período'));
      if (s.periodo) cab.append(el('p', 'chat-folha-meta', maiuscula(s.periodo)));
      art.append(cab);
      if (s.aviso) {
        const div = el('div', 'chat-aviso');
        div.setAttribute('role', 'note');
        const span = el('span');
        span.append(el('strong', '', 'Atenção: '), s.aviso);
        div.append(icone('M8 1.8 15 14H1L8 1.8ZM8 6v3.5M8 11.5v.5'), span);
        art.append(div);
      }
      // Outro período para tentar: o trimestre anterior (se era um trimestre), este ano e o ano passado
      const opcoes = [];
      const inicio = s.periodo.match(/(\d{2})-(\d{2})-(\d{4})/);
      if (/trimestre/i.test(s.periodo) && inicio) {
        const ini = new Date(+inicio[3], +inicio[2] - 4, 1), fim = new Date(+inicio[3], +inicio[2] - 1, 0);
        const d = x => x.toLocaleDateString('pt-PT').replace(/\//g, '-');
        opcoes.push([`Trimestre anterior (${d(ini)} a ${d(fim)})`, `entre ${d(ini)} e ${d(fim)}`]);
      }
      opcoes.push(['Este ano', 'este ano'], ['Ano passado', 'no ano passado']);
      const lista = el('div', 'chat-opcoes');
      opcoes.forEach(([nome, periodo]) => lista.append(botaoEl('chat-chip', nome, () => { if (!ocupado) perguntar(trocarPeriodo(pergunta, periodo)); })));
      art.append(el('h3', 'chat-opcoes-titulo', 'Ver outro período'), lista);
      return art;
    }

    function folhaSemResultado(s, pergunta) {
      const art = el('article', 'chat-folha');
      const cab = el('div', 'chat-folha-cab');
      cab.append(el('h2', 'chat-vazio-titulo', `Não encontrei esse valor de ${s.campo.toLowerCase()}`),
                 el('p', 'chat-folha-meta', 'Escolha um dos valores existentes. A pergunta corrigida fica no campo, para rever antes de enviar.'));
      art.append(cab);

      // Palavra da pergunta mais parecida com um valor existente ("Montija" -> "Montijo")
      const palavras = pergunta.split(/\s+/).filter(p => p.length > 3);
      let errada = s.errado && pergunta.includes(s.errado) ? s.errado : palavras[palavras.length - 1] || '';
      const parecidas = [];
      for (const p of palavras) {
        for (const o of s.opcoes) {
          if (!parecidas.includes(o) && distancia(semAcento(p), semAcento(o[0])) <= 2) { parecidas.push(o); errada = p; }
        }
      }
      const escolher = nome => { campo.value = errada ? pergunta.replace(errada, nome) : pergunta + ' ' + nome; campo.focus(); };
      const botoes = lista => {
        const d = el('div', 'chat-opcoes');
        lista.forEach(([nome, n]) => {
          const b = botaoEl('chat-chip', nomeProprio(nome), () => escolher(nomeProprio(nome)));
          b.append(el('small', '', String(n)));
          d.append(b);
        });
        return d;
      };
      if (parecidas.length) art.append(el('h3', 'chat-opcoes-titulo', 'Parecidos'), botoes(parecidas));
      const resto = s.opcoes.filter(o => !parecidas.includes(o) && !/^sem dados$/i.test(o[0]));
      art.append(el('h3', 'chat-opcoes-titulo', 'Com mais clientes'), botoes(resto.slice(0, 8)));
      if (resto.length > 8) {
        const mais = el('details', 'chat-mais');
        mais.append(el('summary', 'chat-acao', `Ver mais ${resto.length - 8}`), botoes(resto.slice(8)));
        art.append(mais);
      }
      rodapeDados(art, [[maiuscula(s.campo), 'Clientes'], ...s.opcoes.map(([nome, n]) => [nomeProprio(nome), String(n)])],
                  'valores ' + s.campo, s.total ? `mostrados ${s.mostrados} de ${s.total}` : '');
      return art;
    }

    // "NomeCliente" -> "Nome do cliente"; "Contribuinte" -> "NIF"; outros nomes de coluna: separa as palavras
    const CAMPOS = { nomecliente: 'Nome do cliente', contribuinte: 'NIF', nif: 'NIF', codcliente: 'Código do cliente', email: 'Email',
                     telefone: 'Telefone', telemovel: 'Telemóvel', localidade: 'Localidade', vendedor: 'Vendedor', zona: 'Zona', tipocliente: 'Tipo de cliente',
                     actividade: 'Atividade', cobranca: 'Cobrança', expedicao: 'Expedição', situacaofinanceira: 'Situação financeira',
                     escalaoplafond: 'Escalão de plafond', escalaovolumevendas: 'Escalão de volume de vendas' };
    const rotuloCampo = k => /\s/.test(k) ? k
      : CAMPOS[semAcento(k).replace(/[^a-z]/g, '')] || maiuscula(k.replace(/(\p{Ll})(\p{Lu})/gu, '$1 $2').toLowerCase());
    // "Melhor marca em cada localidade" -> "Marca": nome da segunda coluna quando cada linha é "Localidade: Marca"
    const rotuloSub = titulo => maiuscula(((titulo || '').match(/\bmelhor(?:es)?\s+(\S+)/i) || [])[1] || 'Detalhe');

    // Um só registo (ex.: dados de um cliente): campo | valor, em vez de uma tabela com uma linha
    function folhaFicha(t) {
      const art = el('article', 'chat-folha chat-folha--painel');
      const antes = t.antes.split('\n').map(l => l.replace(MARCADOR, '').trim()).filter(Boolean).join(' ').replace(/:\s*$/, '');
      if (antes) { const cab = el('div', 'chat-folha-cab'); cab.append(el('h2', 'chat-folha-titulo', maiuscula(antes))); art.append(cab); }
      const dl = el('dl', 'chat-ficha');
      t.cabecalho.forEach((k, c) => dl.append(el('dt', '', rotuloCampo(k)), el('dd', '', t.linhas[0][c])));
      art.append(dl);
      if (t.depois) art.append(el('p', 'chat-texto', t.depois));
      rodapeDados(art, [['Campo', 'Valor'], ...t.cabecalho.map((k, c) => [rotuloCampo(k), t.linhas[0][c]])], antes || 'ficha');
      return art;
    }

    function formatar(texto, pergunta = '') {
      texto = texto.split('\n').map(limparMarkdown).join('\n');   // uma vez para todas as respostas, incluindo tabelas
      const s = lerSemResultado(texto);
      if (s) return folhaSemResultado(s, pergunta);
      const v = lerSemVendas(texto);
      if (v) return folhaSemVendas(v, pergunta);
      const t = lerTabela(texto);
      if (!t) return folhaLista(lerLista(texto), texto, pergunta);
      return t.linhas.length === 1 ? folhaFicha(t) : folhaTabela(t, pergunta);
    }

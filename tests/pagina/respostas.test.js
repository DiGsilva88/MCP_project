// As respostas do Flow: o que se lê do texto das ferramentas e o que o chat mostra (07-respostas.js e 11-flow.js)
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { carregar, BASE } = require('./carregar.js');

const FICHEIROS = [...BASE, '11-flow'];
const SEM_VENDAS = '0 clientes sem vendas: últimos 90 dias (20-08-2026 a 17-11-2026). Todos os clientes da ficha compraram neste período.';
const RANKING = 'Vendas por artigo, ano atual (01-01-2026 a 17-11-2026): total 7 500,00 € em 3 artigos, 40 documentos. Valores em euros, sem IVA.\n' +
  'Valor médio por documento: 187,50 €.\n1. Artigo A – 3 000,00 € (40,0%) · média 300,00 €\n2. Artigo B – 2 500,00 € (33,3%) · média 277,78 €\n' +
  '3. Artigo C – 2 000,00 € (26,7%) · média 250,00 €\nMostro 10 de 23 280 artigos.';
const analisar = (texto, pergunta = 'p') => { const p = carregar(FICHEIROS); p.ctx.__t = texto; return p.ver(`analisarResposta(__t, ${JSON.stringify(pergunta)})`); };

test('o texto da resposta não se perde (sem vendas)', () => {
  const a = analisar(SEM_VENDAS);
  assert.ok(a.paragrafos.some(p => p.includes('Todos os clientes da ficha compraram neste período')), JSON.stringify(a.paragrafos));
});

test('a nota de truncagem "Mostro N de M" chega à resposta', () => {
  assert.equal(analisar(RANKING).nota, 'Mostra 10 de 23 280 artigos');
});

test('o chat mostra todos os parágrafos e a nota, não só os 2 primeiros', () => {
  const p = carregar(FICHEIROS);
  const analise = { titulo: 'T', paragrafos: ['um', 'dois', 'três', 'quatro'], metricas: [], avisos: [], opcoes: [], painel: null, linhas: null, sugestoes: [], nota: 'Mostra 10 de 23 280 artigos' };
  p.ctx.__i = { id: 1, pergunta: 'q', texto: 't', analise };
  const html = p.ver('respostaHtml(__i)');
  assert.match(html, /três/);
  assert.match(html, /quatro/);
  assert.match(html, /Mostra 10 de 23 280 artigos/);
});

test('Enter durante uma resposta não a cancela; só o botão Parar', () => {
  const p = carregar(FICHEIROS);
  p.ver('var abortou = 0; emCurso = { item: { pendente: true }, controlo: { abort() { abortou++; } } };');
  p.porId.get('flow-form')._h.submit({ preventDefault() {} });
  assert.equal(p.ver('abortou'), 0, 'Enter cancelou a resposta');
  assert.ok(p.toasts.some(t => /resposta|Parar/.test(t.textContent)), 'sem aviso ao utilizador');
  let evitou = false;
  p.porId.get('flow-enviar')._h.click({ preventDefault() { evitou = true; } });
  assert.equal(p.ver('abortou'), 1, 'o botão Parar não cancelou');
  assert.ok(evitou, 'o clique em Parar não deve submeter o formulário');
});

test('uma pergunta feita durante outra não é ignorada em silêncio', async () => {
  const p = carregar(FICHEIROS);
  p.ver('emCurso = { item: { pendente: true }, controlo: { abort() {} } };');
  const r = await p.ver("perguntar('Quanto faturámos?')");
  assert.equal(r, null);
  assert.ok(p.toasts.some(t => /em curso/.test(t.textContent)), 'sem aviso ao utilizador');
});

test('a caixa do Flow fica bloqueada enquanto responde', () => {
  const p = carregar([...FICHEIROS, '09-vistas']);   // desenharFlow usa nomeSeparador, que é da vista
  p.ver('emCurso = { item: { pendente: true }, controlo: { abort() {} } }; desenharFlow();');
  assert.equal(p.porId.get('flow-campo').disabled, true);
});

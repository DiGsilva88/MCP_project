// "Como correu <mês>?" (o botão do cartão "Mês selecionado"): responde o servidor com os indicadores do mês, sem o modelo.
// Antes ia ao modelo local, que em CPU levava minutos; os números são os mesmos do cartão (/api/mes). 07-respostas.js e 11-flow.js
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { carregar, BASE } = require('./carregar.js');

const FICHEIROS = [...BASE, '09-vistas', '11-flow'];
const MES = (m, atual, anterior, extra = {}) => ({ a: 2026, m, atual, anterior, delta: anterior ? (atual - anterior) / anterior * 100 : null, parcial: false, ...extra });
const DADOS = () => ({ ate: '04-09-2026', anoAnt: '2025', alertas: [], clientes: [],
  serie: { anoAtual: 2026, anoAnt: '2025', meses: [MES(7, 278519, 270070), MES(8, 210443.55, 198488.02), MES(9, 38087, 41079, { parcial: true })] },
  kpis: { faturacao: { valor: 2044643, variacao: 6.3 }, documentos: { valor: 34816, variacao: 3.8 }, medio: { valor: 58.73, variacao: 2.4 }, clientes: { valor: 233, variacao: -4.1 } } });
const AGOSTO = () => ({ ano: 2026, mes: 8, incompleto: false,
  atual: { faturacao: 210443.55, documentos: 3583, clientes: 233 }, anterior: { faturacao: 198488.02, documentos: 3350, clientes: 240 } });

function painel() {
  const p = carregar(FICHEIROS);
  p.ctx.__D = DADOS();
  p.ver('app.dados = __D; app.aCarregar = false; app.erro = false');
  p.ctx.__chamadas = [];
  return p;
}
const intencao = (p, texto, hoje = 'new Date(2026, 10, 23)') => JSON.parse(p.ver(`JSON.stringify(intencaoMes(${JSON.stringify(texto)}, ${hoje}))`));

test('reconhece "Como correu <mês>?" com ou sem ano', () => {
  const p = painel();
  assert.deepEqual(intencao(p, 'Como correu setembro de 2026?'), { a: 2026, m: 9 });
  assert.deepEqual(intencao(p, 'Como correu setembro de 2025?'), { a: 2025, m: 9 });
  assert.deepEqual(intencao(p, 'como correu março'), { a: 2026, m: 3 });   // com acento ou sem ele
  assert.deepEqual(intencao(p, 'Como correu marco?'), { a: 2026, m: 3 });
});

test('sem ano usa a ocorrência mais recente do mês: dezembro, em novembro, é o do ano passado', () => {
  const p = painel();
  assert.deepEqual(intencao(p, 'Como correu novembro?'), { a: 2026, m: 11 });
  assert.deepEqual(intencao(p, 'Como correu dezembro?'), { a: 2025, m: 12 });
});

test('"o último mês" (a sugestão do Resumo) é o último mês completo dos dados, como o cartão', () => {
  const p = painel();   // a série tem julho, agosto e setembro (parcial)
  assert.deepEqual(intencao(p, 'Como correu o último mês?'), { a: 2026, m: 8 });
  assert.deepEqual(intencao(p, 'Como correu o mês passado?'), { a: 2026, m: 8 });
  assert.deepEqual(intencao(p, 'como correu o mes anterior'), { a: 2026, m: 8 });
});

test('"o último mês" sem dados carregados: o mês anterior do calendário', () => {
  const p = painel();
  p.ver('app.dados = null');
  assert.deepEqual(intencao(p, 'Como correu o último mês?'), { a: 2026, m: 10 });
  assert.deepEqual(intencao(p, 'Como correu o último mês?', 'new Date(2027, 0, 5)'), { a: 2026, m: 12 });   // em janeiro, dezembro do ano anterior
});

test('o último trimestre ou ano não são do mês: seguem para o modelo', () => {
  const p = painel();
  assert.equal(intencao(p, 'Como correu o último trimestre?'), null);
  assert.equal(intencao(p, 'Como correu o ano passado?'), null);
});

test('a sugestão "Como correu o último mês?" vai ao servidor e não ao modelo', async () => {
  const p = painel();
  p.ctx.fetch = async url => { p.ctx.__chamadas.push(url); return { ok: true, status: 200, json: async () => AGOSTO() }; };
  const item = await p.ver("perguntar('Como correu o último mês?')");
  const chamadas = JSON.parse(p.ver('JSON.stringify(__chamadas)'));
  assert.equal(chamadas.length, 1);
  assert.match(chamadas[0], /^\/api\/mes\?.*ano=2026.*mes=8/);
  assert.equal(item.analise.titulo, 'Agosto 2026');
});

test('outras perguntas não são do mês: seguem para o modelo', () => {
  const p = painel();
  assert.equal(intencao(p, 'Quanto faturámos em setembro?'), null);
  assert.equal(intencao(p, 'Como correu setembro nas vendas por zona?'), null);
  assert.equal(intencao(p, 'Como correu setembro de 1999?'), null);   // ano absurdo: não é do painel
});

test('o botão do cartão pergunta pelo mês e pelo ano', () => {
  const html = painel().ver('vistaResumo()');
  assert.match(html, /data-perguntar="Como correu agosto de 2026\?"/);
});

test('a resposta tem os 4 indicadores do mês e a comparação com o ano anterior', () => {
  const p = painel();
  p.ctx.__M = AGOSTO();
  const a = JSON.parse(p.ver('JSON.stringify(analiseMes(__M))'));
  const texto = a.paragrafos.join(' ');
  assert.equal(a.titulo, 'Agosto 2026');
  assert.match(texto, /Agosto de 2026 fechou com 210\s444\s€, mais 6,0\s?% que em agosto de 2025/);
  assert.match(texto, /3\s583 documentos/);
  assert.match(texto, /valor médio de 58,73\s€/);
  assert.match(texto, /233 clientes ativos/);
  assert.deepEqual(a.metricas.map(m => m.r), ['Faturação', 'Documentos', 'Valor médio', 'Clientes ativos']);
  assert.ok(Math.abs(a.metricas[0].delta - 6.02) < 0.01);
  assert.equal(a.metricas[0].ano, '2025');
  assert.deepEqual(a.avisos, []);
});

test('mês em curso: avisa que é parcial e não compara', () => {
  const p = painel();
  p.ctx.__M = { ...AGOSTO(), mes: 9, incompleto: true, atual: { faturacao: 38087, documentos: 520, clientes: 90 } };
  const a = JSON.parse(p.ver('JSON.stringify(analiseMes(__M))'));
  assert.equal(a.titulo, 'Setembro 2026');
  assert.match(a.avisos.join(' '), /Mês em curso/);
  assert.doesNotMatch(a.paragrafos.join(' '), /mais .* que em|menos .* que em/);
  assert.equal(a.metricas[0].delta, undefined);
});

test('mês sem vendas diz-o, e nunca inventa', () => {
  const p = painel();
  p.ctx.__M = { ano: 2026, mes: 12, incompleto: false, atual: { faturacao: 0, documentos: 0, clientes: 0 }, anterior: { faturacao: 0, documentos: 0, clientes: 0 } };
  const a = JSON.parse(p.ver('JSON.stringify(analiseMes(__M))'));
  assert.match(a.paragrafos.join(' '), /Não há vendas registadas em dezembro de 2026/);
  assert.deepEqual(a.metricas, []);
});

test('a pergunta do mês vai ao servidor (/api/mes) e não ao modelo', async () => {
  const p = painel();
  p.ctx.fetch = async url => { p.ctx.__chamadas.push(url); return { ok: true, status: 200, json: async () => AGOSTO() }; };
  const item = await p.ver("perguntar('Como correu agosto de 2026?')");
  const chamadas = JSON.parse(p.ver('JSON.stringify(__chamadas)'));
  assert.equal(chamadas.length, 1);
  assert.match(chamadas[0], /^\/api\/mes\?.*ano=2026.*mes=8/);   // nenhuma chamada a /perguntar
  assert.equal(p.ver('conversa.length'), 1);
  assert.equal(item.local, true);
  assert.equal(item.analise.titulo, 'Agosto 2026');
});

test('sem os indicadores do mês, segue para o modelo, como antes', async () => {
  const p = painel();
  p.ctx.fetch = async url => { p.ctx.__chamadas.push(url); return { ok: false, status: 500, text: async () => 'erro', json: async () => ({}) }; };
  await p.ver("perguntar('Como correu agosto de 2026?')");
  const chamadas = JSON.parse(p.ver('JSON.stringify(__chamadas)'));
  assert.equal(chamadas.length, 2);
  assert.match(chamadas[0], /^\/api\/mes\?/);
  assert.equal(chamadas[1], '/perguntar');
});

test('"Tentar outra vez" repete a pergunta no modelo (o painel já falhou)', async () => {
  const p = painel();
  p.ctx.fetch = async url => { p.ctx.__chamadas.push(url); return { ok: false, status: 500, text: async () => 'erro', json: async () => ({}) }; };
  p.ctx.__item = { id: 1, pergunta: 'Como correu agosto de 2026?', texto: '', erro: 'vazio' };
  p.ver('conversa.push(__item)');
  await p.ver("perguntar('Como correu agosto de 2026?', __item)");
  assert.deepEqual(JSON.parse(p.ver('JSON.stringify(__chamadas)')), ['/perguntar']);
});

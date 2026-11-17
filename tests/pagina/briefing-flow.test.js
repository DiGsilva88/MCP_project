// "O que devo saber antes de visitar o cliente X?": o Flow responde com o briefing do servidor, sem o modelo
// (o modelo confundia-a com um pedido de dados do cliente e respondia sobre o email). 07-respostas.js e 11-flow.js
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { carregar, BASE } = require('./carregar.js');

const FICHEIROS = [...BASE, '09-vistas', '11-flow'];
const B = () => ({
  cliente: 'Rui Miguel Neves Bandeira Maia (1003)', referencia: '04-09-2026',
  maisComprado: { artigo: 'Pastilhas de travão', id: 'A1', valor: 2400 },
  deixouDeComprar: [{ artigo: 'Discos de travão', id: 'A2', mediaMes: 4, ultima: '07-05-2026' }],
  emAberto: { documentos: 2, total: 450, vencido: 300, documentosCredito: 1, creditos: 40, comIva: true, itens: [] },
});
const CARTEIRA = { codigo: '1003', nome: 'Rui Miguel Neves Bandeira Maia', zona: 'LISBOA', num: 8000, delta: 5.2, ultima: '03-09-2026', documentos: 10 };

function painel() {
  const p = carregar(FICHEIROS);
  p.ctx.__B = B(); p.ctx.__C = CARTEIRA;
  return p;
}

test('reconhece a pergunta de briefing e tira o nome do cliente', () => {
  const p = painel();
  const nome = t => p.ver(`intencaoBriefing(${JSON.stringify(t)})`);
  assert.equal(nome('O que devo saber antes de visitar o cliente Rui Miguel Neves Bandeira Maia?'), 'Rui Miguel Neves Bandeira Maia');
  assert.equal(nome('O que devo saber antes de visitar Casa Costa?'), 'Casa Costa');
  assert.equal(nome('Briefing do cliente Casa Costa'), 'Casa Costa');
  assert.equal(nome('Quanto faturámos no último trimestre?'), null);
  assert.equal(nome('Email do cliente Casa Costa'), null);
});

test('o briefing como resposta: o que deixou de comprar, o por pagar com IVA e os créditos', () => {
  const p = painel();
  const a = JSON.parse(p.ver('JSON.stringify(analiseBriefing(__B, __C, { anoAnt: "2025" }))'));
  const texto = a.paragrafos.join(' ');
  assert.equal(a.titulo, 'Antes de visitar Rui Miguel Neves Bandeira Maia');
  assert.match(texto, /Artigo mais comprado: Pastilhas/);
  assert.match(texto, /Deixou de comprar 1 artigo: Discos de travão \(comprava 4 por mês, última compra a 7 mai\)/);
  assert.match(texto, /Tem 2 faturas por pagar, 450,00 € com IVA, dos quais 300,00 € vencidos/);
  assert.match(texto, /1 nota de crédito por abater, 40,00 €/);
  assert.ok(a.metricas.some(m => m.r === 'Faturas por pagar (com IVA)' && /450,00/.test(m.v)));
  assert.ok(a.metricas.length <= 4);
});

test('sem faturas nem artigos parados diz-o, e nunca inventa', () => {
  const p = painel();
  p.ctx.__B = { cliente: 'Casa (7)', maisComprado: null, deixouDeComprar: [], emAberto: { documentos: 0, total: 0, vencido: 0, documentosCredito: 0, creditos: 0 } };
  const texto = JSON.parse(p.ver('JSON.stringify(analiseBriefing(__B, null, null).paragrafos)')).join(' ');
  assert.match(texto, /Continua a comprar os artigos habituais/);
  assert.match(texto, /Não tem faturas por pagar/);
  assert.doesNotMatch(texto, /Artigo mais comprado/);
});

test('a pergunta de briefing vai ao servidor e não ao modelo', async () => {
  const p = painel();
  p.ctx.__chamadas = [];
  p.ctx.fetch = async url => { p.ctx.__chamadas.push(url); return { ok: true, status: 200, json: async () => B() }; };
  const item = await p.ver("perguntar('O que devo saber antes de visitar o cliente Rui Miguel Neves Bandeira Maia?')");
  const chamadas = JSON.parse(p.ver('JSON.stringify(__chamadas)'));
  assert.deepEqual(chamadas, ['/api/clientes/Rui%20Miguel%20Neves%20Bandeira%20Maia/briefing']);   // nenhuma chamada a /perguntar
  assert.equal(p.ver('conversa.length'), 1);
  assert.match(item.analise.titulo, /Antes de visitar Rui Miguel/);
});

test('cliente não encontrado: segue para o modelo, como antes', async () => {
  const p = painel();
  p.ctx.__chamadas = [];
  p.ctx.fetch = async url => { p.ctx.__chamadas.push(url); return { ok: false, status: 404, text: async () => 'Cliente não encontrado.', json: async () => ({}) }; };
  await p.ver("perguntar('O que devo saber antes de visitar o cliente Ninguém?')");
  const chamadas = JSON.parse(p.ver('JSON.stringify(__chamadas)'));
  assert.equal(chamadas.length, 2);
  assert.match(chamadas[0], /briefing$/);
  assert.equal(chamadas[1], '/perguntar');
});

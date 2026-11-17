// Formatação pt-PT da página (src/Vendas.Agente/Pagina/js/01-formatacao.js). Correr: node --test tests/pagina
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { fmt } = require('../../src/Vendas.Agente/Pagina/js/01-formatacao.js');

const s = t => t.replace(/ /g, ' ');   // compara com espaços normais; o NBSP verifica-se à parte

test('milhares com espaço não separável e euro depois do número', () => {
  assert.equal(fmt.eur(2044643), '2 044 643 €');
  assert.equal(s(fmt.eur(2044643.4)), '2 044 643 €');
  assert.equal(s(fmt.inteiro(34816)), '34 816');
  assert.equal(fmt.inteiro(999), '999');
});

test('decimais com vírgula', () => {
  assert.equal(s(fmt.eur2(58.73)), '58,73 €');
  assert.equal(s(fmt.eur2(1234.5)), '1 234,50 €');
  assert.equal(s(fmt.eur2(-84.1)), '−84,10 €');
  assert.equal(fmt.pct(6.25), '6,3%');
  assert.equal(fmt.pct(0), '0,0%');
});

test('sem valor não inventa número', () => {
  for (const v of [null, undefined, NaN, Infinity]) {
    assert.equal(fmt.eur(v), '—');
    assert.equal(fmt.pct(v), '—');
  }
  assert.deepEqual(fmt.variacao(null), { texto: '–', sobe: null });
});

test('subida ▲ e descida ▼', () => {
  assert.deepEqual(fmt.variacao(6.3), { texto: '▲ 6,3%', sobe: true });
  assert.deepEqual(fmt.variacao(-4.14), { texto: '▼ 4,1%', sobe: false });
  assert.equal(fmt.variacao(0).sobe, true);
});

test('arredondar para zero não deixa "−0"', () => {
  assert.equal(fmt.decimal(-0.01, 1), '0,0');
  assert.equal(fmt.inteiro(-0.4), '0');
});

test('datas por extenso', () => {
  assert.equal(fmt.dataLonga('04-09-2026'), '4 de setembro');
  assert.equal(fmt.dataLonga('2026-09-04', true), '4 de setembro de 2026');
  assert.equal(s(fmt.dataCurta('14-08-2026')), '14 ago');
  assert.equal(fmt.dataLonga('lixo'), '');
});

test('valores curtos para eixos', () => {
  assert.equal(s(fmt.curto(198488)), '198 mil €');
  assert.equal(s(fmt.curto(2044643)), '2,04 M€');
  assert.equal(s(fmt.curto(950)), '950 €');
});

test('ler números do servidor', () => {
  assert.equal(fmt.numero('2 044 643,20 €'), 2044643.2);
  assert.equal(fmt.numero('−84,10 €'), -84.1);
  assert.ok(Number.isNaN(fmt.numero('')));
});

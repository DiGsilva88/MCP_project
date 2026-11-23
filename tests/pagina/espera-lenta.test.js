// Chat: enquanto o modelo local responde (em CPU pode levar minutos), a espera diz-o, em vez de só contar segundos (11-flow.js)
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { carregar, BASE } = require('./carregar.js');

const FICHEIROS = [...BASE, '09-vistas', '11-flow'];
const painel = () => carregar(FICHEIROS);

test('nos primeiros segundos a espera é a normal, com o tempo', () => {
  const p = painel();
  assert.equal(p.ver('textoEspera({ segundos: 5 })'), 'A consultar as vendas… 5 s');
});

test('passado um tempo avisa que o assistente está a demorar mais do que o normal', () => {
  const p = painel();
  const texto = p.ver('textoEspera({ segundos: 25 })');
  assert.match(texto, /demorar mais do que o normal/);
  assert.match(texto, /alguns minutos/);
  assert.match(texto, /25 s/);
});

test('o aviso só aparece a partir dos 20 segundos', () => {
  const p = painel();
  assert.doesNotMatch(p.ver('textoEspera({ segundos: 19 })'), /demorar/);
  assert.match(p.ver('textoEspera({ segundos: 20 })'), /demorar/);
});

test('na fila diz-se que há outra pergunta em curso, mesmo com muito tempo', () => {
  const p = painel();
  const texto = p.ver('textoEspera({ segundos: 90, fila: true })');
  assert.match(texto, /Na fila: há outra pergunta em curso/);
  assert.doesNotMatch(texto, /demorar mais/);
});

test('a resposta pendente mostra esse texto', () => {
  const p = painel();
  p.ctx.__item = { pendente: true, segundos: 30 };
  assert.match(p.ver('respostaHtml(__item)'), /demorar mais do que o normal/);
});

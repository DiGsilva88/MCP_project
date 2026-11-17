// Filtros, Guardados e regras de alerta da página (src/Vendas.Agente/Pagina/js/02-estado.js)
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { filtros, guardados, alertasRegras } = require('../../src/Vendas.Agente/Pagina/js/02-estado.js');

test('filtros combinados chegam todos à consulta', () => {
  const F = { cliente: 'Casa Costa', zona: 'BRAGA' };
  assert.deepEqual(filtros.consulta('MesAtual', F), { periodo: 'MesAtual', cliente: 'Casa Costa', zona: 'BRAGA' });
  assert.deepEqual(filtros.consulta('AnoAtual', filtros.vazios()), { periodo: 'AnoAtual' });
});

test('período desconhecido volta ao ano atual', () => {
  assert.equal(filtros.consulta('Sempre', filtros.vazios()).periodo, 'AnoAtual');
});

test('etiquetas dos filtros ativos e remover um deixa o outro', () => {
  const F = { cliente: 'Casa Costa', zona: 'BRAGA' };
  assert.deepEqual(filtros.ativos(F, z => z[0] + z.slice(1).toLowerCase()).map(a => a.rotulo), ['Zona: Braga', 'Cliente: Casa Costa']);
  const sem = filtros.remover(F, 'zona');
  assert.deepEqual(sem, { cliente: 'Casa Costa', zona: '' });
  assert.deepEqual(filtros.ativos(sem).map(a => a.chave), ['cliente']);
  assert.deepEqual(filtros.ativos(filtros.vazios()), []);
});

test('valores do formulário ficam limpos e com tamanho máximo', () => {
  assert.deepEqual(filtros.limpos({ cliente: '  Casa   Costa ', zona: null }), { cliente: 'Casa Costa', zona: '' });
  assert.equal(filtros.limpos({ cliente: 'x'.repeat(300) }).cliente.length, 100);
});

test('guardar a mesma vista duas vezes não duplica', () => {
  const v = { id: 1, tipo: 'vista', separador: 'clientes', periodo: 'AnoAtual', filtros: { zona: 'BRAGA' } };
  const a = guardados.adicionar([], v);
  assert.equal(a.jaExistia, false);
  const b = guardados.adicionar(a.lista, { ...v, id: 2 });
  assert.equal(b.jaExistia, true);
  assert.equal(b.lista.length, 1);
  // outro separador ou outro filtro é outra vista
  assert.equal(guardados.adicionar(a.lista, { ...v, id: 3, separador: 'zonas' }).lista.length, 2);
  assert.equal(guardados.adicionar(a.lista, { ...v, id: 4, filtros: {} }).lista.length, 2);
});

test('respostas guardadas: a mesma pergunta conta uma vez; filtrar e remover', () => {
  const r = { id: 5, tipo: 'resposta', pergunta: 'Como correu agosto?', texto: '...' };
  let l = guardados.adicionar([], r).lista;
  assert.equal(guardados.adicionar(l, { ...r, id: 6, pergunta: 'como correu AGOSTO?' }).jaExistia, true);
  l = guardados.adicionar(l, { id: 7, tipo: 'vista', separador: 'resumo', periodo: 'AnoAtual', filtros: {} }).lista;
  assert.deepEqual(guardados.filtrar(l, 'resposta').map(g => g.id), [5]);
  assert.deepEqual(guardados.filtrar(l, 'todos').map(g => g.id), [7, 5]);
  assert.deepEqual(guardados.remover(l, 7).map(g => g.id), [5]);
});

test('regras de alerta: limite guardado, inválido volta à omissão, desligada não conta', () => {
  assert.equal(alertasRegras.regra({}, 'distrito').limite, 20);
  assert.equal(alertasRegras.regra({ distrito: { limite: 50 } }, 'distrito').limite, 50);
  assert.equal(alertasRegras.regra({ distrito: { limite: 500 } }, 'distrito').limite, 20);
  const queda = { regra: 'distrito', delta: -30 };
  assert.equal(alertasRegras.conta({}, queda), true);
  assert.equal(alertasRegras.conta({ distrito: { limite: 40 } }, queda), false);
  assert.equal(alertasRegras.conta({ distrito: { ligada: false } }, queda), false);
  assert.equal(alertasRegras.conta({}, { regra: null }), true);
});

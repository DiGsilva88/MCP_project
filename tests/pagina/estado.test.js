// Filtros, Guardados e regras de alerta da página (src/Vendas.Agente/Pagina/js/02-estado.js)
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { filtros, guardados, alertasRegras } = require('../../src/Vendas.Agente/Pagina/js/02-estado.js');

const todos = () => ({ de: '2026-02-01', ate: '2026-02-28', comparar: 'nenhum', cliente: 'Casa Costa', zonas: ['BRAGA', 'PORTO'], marcas: ['Bosch'], tipos: ['F', 'N'] });

test('filtros combinados chegam todos à consulta', () => {
  assert.deepEqual(filtros.consulta('MesAtual', todos()), [
    ['periodo', 'MesAtual'], ['de', '01-02-2026'], ['ate', '28-02-2026'], ['cliente', 'Casa Costa'],
    ['zona', 'BRAGA'], ['zona', 'PORTO'], ['marca', 'Bosch'], ['tipodoc', 'F'], ['tipodoc', 'N'], ['comparar', 'nao']]);
  assert.deepEqual(filtros.consulta('AnoAtual', filtros.vazios()), [['periodo', 'AnoAtual']]);
});

test('período desconhecido volta ao ano atual', () => {
  assert.deepEqual(filtros.consulta('Sempre', filtros.vazios())[0], ['periodo', 'AnoAtual']);
});

test('etiquetas dos filtros ativos; remover um deixa os outros', () => {
  const F = todos();
  assert.deepEqual(filtros.ativos(F, { zona: z => z[0] + z.slice(1).toLowerCase(), tipo: t => 'Tipo ' + t }).map(a => a.rotulo),
    ['2026-02-01 a 2026-02-28', 'Sem comparação', 'Cliente: Casa Costa', 'Braga', 'Porto', 'Bosch', 'Tipo F', 'Tipo N']);
  const semPorto = filtros.remover(F, 'zonas', 'PORTO');
  assert.deepEqual(semPorto.zonas, ['BRAGA']);
  assert.deepEqual(semPorto.marcas, ['Bosch']);
  assert.deepEqual([filtros.remover(F, 'datas').de, filtros.remover(F, 'comparar').comparar, filtros.remover(F, 'cliente').cliente], ['', 'ano', '']);
  assert.deepEqual(filtros.ativos(filtros.vazios()), []);
});

test('valores do formulário ficam limpos, sem repetidos e com os limites', () => {
  const l = filtros.limpos({ cliente: '  Casa   Costa ', zonas: ['Braga', ' braga ', ''], tipos: ['F', "F' OR 1=1"], de: '2026-02-01', ate: '' });
  assert.equal(l.cliente, 'Casa Costa');
  assert.deepEqual(l.zonas, ['braga']);
  assert.deepEqual(l.tipos, ['F']);
  assert.equal(l.de, '');                                     // só uma data não conta
  assert.equal(filtros.limpos({ cliente: 'x'.repeat(300) }).cliente.length, 100);
  assert.equal(filtros.limpos({ zonas: Array.from({ length: 50 }, (_, i) => 'Z' + i) }).zonas.length, 30);
  assert.deepEqual(filtros.limpos({ zona: 'BRAGA' }).zonas, ['BRAGA']);   // preferências guardadas antes da fase 2
});

test('validação das datas De/Até', () => {
  const hoje = '2026-11-17';
  assert.equal(filtros.validarDatas('', '', hoje), '');
  assert.equal(filtros.validarDatas('2026-02-01', '2026-02-28', hoje), '');
  assert.match(filtros.validarDatas('2026-03-01', '2026-02-01', hoje), /anterior/);
  assert.match(filtros.validarDatas('2026-02-01', '', hoje), /duas datas/);
  assert.match(filtros.validarDatas('2026-02-01', '2027-01-01', hoje), /hoje/);
});

test('guardar a mesma vista duas vezes não duplica', () => {
  const v = { id: 1, tipo: 'vista', separador: 'clientes', periodo: 'AnoAtual', filtros: { zonas: ['BRAGA'] } };
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

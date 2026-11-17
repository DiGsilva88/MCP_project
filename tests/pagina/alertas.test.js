// Alertas personalizados: validar o que o utilizador escreve, a frase e o que encontram nos dados (src/Vendas.Agente/Pagina/js/02-estado.js)
const { test } = require('node:test');
const assert = require('node:assert/strict');
global.fmt = require('../../src/Vendas.Agente/Pagina/js/01-formatacao.js').fmt;
const { alertasPers, MODELOS_ALERTA } = require('../../src/Vendas.Agente/Pagina/js/02-estado.js');

const F = (o = {}) => ({ nome: 'Quedas de zona', tipo: 'zona', limite: 30, zona: '', freq: 'Uma vez por dia', mail: false, email: '', ...o });
const D = () => ({
  ate: '04-09-2026', anoAnt: '2025',
  zonas: [{ id: 'LISBOA', nome: 'Lisboa', fat: 36, variacao: -99.7 }, { id: 'BRAGA', nome: 'Braga', fat: 5000, variacao: -35 }, { id: 'PORTO', nome: 'Porto', fat: 9000, variacao: 4 }],
  carteira: [
    { codigo: '1', nome: 'Casa A', num: 900, delta: -41, ultima: '14-06-2026', novo: false },     // 82 dias sem comprar
    { codigo: '2', nome: 'Casa B', num: 8000, delta: 5, ultima: '03-09-2026', novo: true },        // novo, 8 000 €
    { codigo: '3', nome: 'Casa C', num: 100, delta: -100, ultima: '02-05-2026', novo: false },     // 125 dias
  ],
  artigosTodos: [{ nome: 'Pneu', num: 100, delta: -20 }, { nome: 'Filtro', num: 90, delta: 4 }],
});

test('há 6 modelos, como no protótipo, e cada um tem frase e limite por omissão', () => {
  assert.deepEqual(MODELOS_ALERTA.map(m => m.tipo), ['zona', 'sem', 'desvio', 'artigo', 'fatura', 'novo']);
  assert.match(alertasPers.frase(F()), /cair mais de 30%/);
  assert.match(alertasPers.frase(F({ tipo: 'zona', zona: 'Braga' })), /da zona Braga/);
  assert.match(alertasPers.frase(F({ tipo: 'fatura', limite: 60 })), /mais de 60 dias por pagar/);
  assert.match(alertasPers.frase(F({ tipo: 'novo', limite: 5000 })), /5 000 €/);
});

test('validar: nome, limite e email da empresa', () => {
  assert.ok(alertasPers.limpar(F()).alerta);
  assert.equal(alertasPers.limpar(F({ nome: '   ' })).campo, 'nome');
  assert.equal(alertasPers.limpar(F({ nome: 'x'.repeat(61) })).campo, 'nome');
  assert.equal(alertasPers.limpar(F({ limite: 0 })).campo, 'limite');
  assert.equal(alertasPers.limpar(F({ limite: 101 })).campo, 'limite');                // % até 100
  assert.equal(alertasPers.limpar(F({ limite: 2.5 })).campo, 'limite');
  assert.equal(alertasPers.limpar(F({ tipo: 'fatura', limite: 3650 })).alerta.limite, 3650);
  assert.equal(alertasPers.limpar(F({ tipo: 'inventado' })).campo, 'tipo');
  assert.equal(alertasPers.limpar(F({ mail: true, email: 'x@gmail.com' })).campo, 'email');   // só @inforap.pt, como o Agendar envio
  assert.equal(alertasPers.limpar(F({ mail: true, email: 'ana@inforap.pt' })).alerta.email, 'ana@inforap.pt');
  assert.equal(alertasPers.limpar(F({ mail: false, email: 'lixo' })).alerta.email, '');        // sem o interruptor, o email não se guarda
});

test('validar: a zona só conta no alerta de zona; no máximo 20 alertas', () => {
  assert.equal(alertasPers.limpar(F({ tipo: 'sem', limite: 60, zona: 'Braga' })).alerta.zona, '');
  assert.equal(alertasPers.limpar(F({ zona: 'Braga' })).alerta.zona, 'Braga');
  const vinte = Array.from({ length: 20 }, (_, i) => ({ id: 'a' + i }));
  assert.match(alertasPers.limpar(F(), vinte).erro, /20 alertas/);
  assert.ok(alertasPers.limpar({ ...F(), id: 'a3' }, vinte).alerta);                   // editar um existente pode
});

test('zona: encontra as quedas acima do limite (e a zona escolhida)', () => {
  const r = alertasPers.avaliar(F({ id: 'z', limite: 30 }), D());
  assert.deepEqual(r.map(x => x.titulo), ['Lisboa caiu 99,7%', 'Braga caiu 35,0%']);
  assert.deepEqual(alertasPers.avaliar(F({ id: 'z', limite: 30, zona: 'BRAGA' }), D()).map(x => x.titulo), ['Braga caiu 35,0%']);
  assert.deepEqual(alertasPers.avaliar(F({ id: 'z', limite: 50 }), D()).map(x => x.titulo), ['Lisboa caiu 99,7%']);
});

test('clientes: sem compras há mais de N dias, com desvio, e novos de valor', () => {
  assert.deepEqual(alertasPers.avaliar(F({ id: 's', tipo: 'sem', limite: 60 }), D()).map(x => x.cliente), ['1', '3']);   // contados desde a última venda registada
  assert.deepEqual(alertasPers.avaliar(F({ id: 's', tipo: 'sem', limite: 100 }), D()).map(x => x.cliente), ['3']);
  assert.deepEqual(alertasPers.avaliar(F({ id: 'd', tipo: 'desvio', limite: 40 }), D()).map(x => x.cliente), ['1', '3']);
  assert.deepEqual(alertasPers.avaliar(F({ id: 'n', tipo: 'novo', limite: 5000 }), D()).map(x => x.cliente), ['2']);
  assert.deepEqual(alertasPers.avaliar(F({ id: 'n', tipo: 'novo', limite: 9000 }), D()), []);
});

test('artigos em queda', () => {
  assert.deepEqual(alertasPers.avaliar(F({ id: 'a', tipo: 'artigo', limite: 15 }), D()).map(x => x.titulo), ['Pneu caiu 20,0%']);
});

test('faturas em atraso: só as com mais dias que o limite; sem dados não inventa', () => {
  const atrasos = { itens: [{ cliente: 'Casa A (1)', documento: 'F 2026/1', dias: 106, valor: 100 }, { cliente: 'Casa B (2)', documento: 'F 2026/2', dias: 30, valor: 50 }], truncado: false };
  const r = alertasPers.avaliar(F({ id: 'f', tipo: 'fatura', limite: 60 }), D(), atrasos);
  assert.equal(r.length, 1);
  assert.match(r[0].titulo, /1 fatura há mais de 60 dias por pagar/);
  assert.match(r[0].texto, /100 €/);
  assert.deepEqual(alertasPers.avaliar(F({ id: 'f', tipo: 'fatura', limite: 60 }), D(), null), []);
  assert.deepEqual(alertasPers.avaliar(F({ id: 'f', tipo: 'fatura', limite: 200 }), D(), atrasos), []);
});

test('último aviso: grava o dia em que o alerta encontrou algo, uma só vez por dia', () => {
  const lista = [F({ id: 'z', ligada: true }), F({ id: 'off', ligada: false }), F({ id: 'nada', limite: 100, ligada: true })];
  const r = alertasPers.marcarUltimos(lista, D(), null, '2026-09-04');
  assert.deepEqual(r.lista.map(a => a.ultimo || ''), ['2026-09-04', '', '']);   // desligado e sem resultados ficam como estavam
  assert.equal(r.mudou, true);
  assert.equal(alertasPers.marcarUltimos(r.lista, D(), null, '2026-09-04').mudou, false);   // já gravado hoje: não volta a gravar
});

test('uma resposta por cliente ou zona, no máximo 10; ids distintos por alerta', () => {
  const muitos = { ...D(), carteira: Array.from({ length: 30 }, (_, i) => ({ codigo: String(i), nome: 'C' + i, num: 1, delta: -90, ultima: '01-08-2026' })) };
  const r = alertasPers.avaliar(F({ id: 'd', tipo: 'desvio', limite: 20 }), muitos);
  assert.equal(r.length, 10);
  assert.equal(new Set(r.map(x => x.id)).size, 10);
  assert.ok(r.every(x => x.id.startsWith('pers|d|')));
});

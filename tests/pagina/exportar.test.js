// Exportar (src/Vendas.Agente/Pagina/js/06-exportar.js): CSV para o Excel em pt-PT e o CRC do .xlsx
const { test } = require('node:test');
const assert = require('node:assert/strict');
global.fmt = require('../../src/Vendas.Agente/Pagina/js/01-formatacao.js').fmt;
const { textoCsv, celulaNumero, crc32 } = require('../../src/Vendas.Agente/Pagina/js/06-exportar.js');

test('números do ecrã voltam a ser números (o Excel soma)', () => {
  assert.equal(celulaNumero('2 044 643 €'), 2044643);
  assert.equal(celulaNumero('58,73 €'), 58.73);
  assert.equal(celulaNumero('6,3%'), 6.3);
  assert.equal(celulaNumero(12.5), 12.5);
  assert.equal(celulaNumero('FT 2026/34816'), null);
  assert.equal(celulaNumero('04-09-2026'), null);
  assert.equal(celulaNumero(''), null);
});

test('CSV com ponto e vírgula, vírgula decimal e BOM', () => {
  const csv = textoCsv([['Cliente', 'Faturação (€)'], ['Casa; Costa', 1234.5], ['=SOMA(A1)', null]]);
  assert.ok(csv.startsWith('﻿'));
  assert.equal(csv.slice(1), 'Cliente;Faturação (€)\r\n"Casa; Costa";1234,5\r\n\'=SOMA(A1);');
});

test('CRC32 do zip (.xlsx)', () => {
  assert.equal(crc32(new TextEncoder().encode('123456789')), 0xCBF43926);
});

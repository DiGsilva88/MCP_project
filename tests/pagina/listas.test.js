// Listas com limite e paginação, e quando o ecrã de entrada se salta (src/Vendas.Agente/Pagina/js/02-estado.js e 09-vistas.js)
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { listas, arranque } = require('../../src/Vendas.Agente/Pagina/js/02-estado.js');
const { carregar, BASE } = require('./carregar.js');

test('paginar: 15 por página, com os limites certos', () => {
  const cem = Array.from({ length: 100 }, (_, i) => i);
  const p = listas.paginar(cem, 0, 15);
  assert.deepEqual([p.itens.length, p.ultima, p.de, p.ate, p.total], [15, 6, 1, 15, 100]);
  const u = listas.paginar(cem, 99, 15);                       // página a mais: fica na última
  assert.deepEqual([u.pag, u.itens.length, u.de, u.ate], [6, 10, 91, 100]);
  assert.equal(listas.paginar(cem, -3, 15).pag, 0);
  assert.deepEqual(listas.paginar([], 0, 15), { itens: [], pag: 0, ultima: 0, total: 0, de: 0, ate: 0 });
});

test('o ecrã de entrada só se salta num reload, num separador pedido ou para o vendedor', () => {
  const base = { vendedor: false, ancoraValida: false, navegacao: 'navigate', marca: true };
  assert.equal(arranque.saltarEntrada(base), false);                                  // marca de uma visita anterior: mostra a entrada
  assert.equal(arranque.saltarEntrada({ ...base, navegacao: 'reload' }), true);       // recarregar volta ao painel
  assert.equal(arranque.saltarEntrada({ ...base, navegacao: 'reload', marca: false }), false);   // recarregar ainda na entrada
  assert.equal(arranque.saltarEntrada({ ...base, ancoraValida: true }), true);        // #clientes, #documento
  assert.equal(arranque.saltarEntrada({ ...base, vendedor: true }), true);            // o vendedor não pode perguntar
});

// --- o que cada separador desenha ---
const FICHEIROS = [...BASE, '09-vistas'];
const itens = (n, prefixo = 'Artigo') => Array.from({ length: n }, (_, i) => ({ nome: `${prefixo} ${i + 1}`, codigo: 'C' + i, num: 1000 - i, delta: 1.5, valor: '1 000,00 €' }));
const kpis = { faturacao: { valor: 1e6, variacao: 1 }, documentos: { valor: 900, variacao: 1 }, medio: { valor: 55, variacao: 1 }, clientes: { valor: 100, variacao: 1 } };
function vista(nome, dados, antes = '') {
  const p = carregar(FICHEIROS);
  p.ctx.__D = dados;
  p.ver(`app.dados = __D; app.aCarregar = false; app.erro = false; ${antes}`);
  return p.ver(`${nome}()`);
}
const linhas = html => (html.match(/<tr[ >]/g) || []).length;
const ARTIGOS = { anoAnt: '2025', kpis, artigos: itens(10), artigosTodos: itens(100), artigosTotal: 100, marcas: itens(20, 'Marca') };

test('Artigos: só os 10 primeiros, com "Ver todos"', () => {
  const html = vista('vistaArtigos', ARTIGOS).split('Faturação por marca')[0];
  assert.ok(linhas(html) <= 11, `${linhas(html)} linhas (cabeçalho + 10)`);
  assert.match(html, /data-ver-todos="artigos"/);
});

test('Artigos: com "Ver todos" pagina de 15 em 15', () => {
  const html = vista('vistaArtigos', ARTIGOS, 'app.ver.artigos = true;').split('Faturação por marca')[0];
  assert.ok(linhas(html) <= 16, `${linhas(html)} linhas (cabeçalho + 15)`);
  assert.match(html, /data-pag="artigos\|1"/);
  assert.match(html, /1–15 de 100/);
});

test('Artigos: as marcas voltam ao top 6', () => {
  const html = vista('vistaArtigos', ARTIGOS).split('Faturação por marca')[1];
  assert.ok(linhas(html) <= 7, `${linhas(html)} linhas (cabeçalho + 6)`);
  assert.match(html, /data-ver-todos="marcas"/);
});

test('Clientes: 15 por página, nunca as 100 de uma vez', () => {
  const carteira = itens(100, 'Cliente').map(c => ({ ...c, zona: 'BRAGA', ultima: '04-09-2026', documentos: 3, estado: 'Ativo' }));
  const html = vista('vistaClientes', { anoAnt: '2025', kpis, parados: 3, carteira, artigos: [] });
  assert.ok(linhas(html) <= 16, `${linhas(html)} linhas (cabeçalho + 15)`);
  assert.match(html, /data-pag="clientes\|1"/);
});

test('Zonas: top 8, com "Ver todos"', () => {
  const zonas = Array.from({ length: 20 }, (_, i) => ({ id: 'Z' + i, nome: 'Zona ' + i, sem: false, fat: 1000 - i, variacao: 1, clientes: 3 }));
  const html = vista('vistaZonas', { anoAnt: '2025', kpis, zonas, localidades: null, artigos: [] });
  assert.ok(linhas(html) <= 9, `${linhas(html)} linhas (cabeçalho + 8)`);
  assert.match(html, /data-ver-todos="zonas"/);
});

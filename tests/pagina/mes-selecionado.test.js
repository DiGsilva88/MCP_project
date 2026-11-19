// Resumo: o gráfico "Vendas por mês" e o cartão "Mês selecionado" (08-dados.js, 04-componentes.js, 09-vistas.js)
// Os valores de agosto de 2026 (210 443,55 € contra 198 488,02 €) são os da BD de testes; documentos e clientes são de exemplo.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { carregar, BASE } = require('./carregar.js');

const FICHEIROS = [...BASE, '09-vistas'];
const NBSP = String.fromCharCode(160);
const MES = (m, atual, anterior, extra = {}) => ({ a: 2026, m, atual, anterior, delta: anterior ? (atual - anterior) / anterior * 100 : null, parcial: false, ...extra });
const SERIE = () => ({ anoAtual: 2026, anoAnt: '2025', meses: [MES(6, 255448, 250414), MES(7, 278519, 270070), MES(8, 210443.55, 198488.02), MES(9, 38087, 41079, { parcial: true })] });
const DADOS = () => ({ ate: '04-09-2026', anoAnt: '2025', alertas: [], clientes: [], serie: SERIE(),
  kpis: { faturacao: { valor: 2044643, variacao: 6.3 }, documentos: { valor: 34816, variacao: 3.8 }, medio: { valor: 58.73, variacao: 2.4 }, clientes: { valor: 233, variacao: -4.1 } } });
const M = (atual, anterior, extra = {}) => ({ ano: 2026, mes: 8, incompleto: false, atual, anterior, ...extra });
const AGOSTO = () => M({ faturacao: 210443.55, documentos: 3583, clientes: 233 }, { faturacao: 198488.02, documentos: 3350, clientes: 240 });
const painel = (antes = '') => {
  const p = carregar(FICHEIROS);
  p.ctx.__D = DADOS();
  p.ver(`app.dados = __D; app.aCarregar = false; app.erro = false; ${antes}`);
  return p;
};
const blocos = (p, m, incompleto = false) => { p.ctx.__M = m; return [...p.ver(`blocosMes(__M, ${incompleto})`)]; };

test('mês incompleto: o do calendário, ou aquele em que acabam os dados', () => {
  const p = carregar(BASE), hoje = 'new Date(2026, 10, 19)';   // 19 de novembro de 2026
  assert.equal(p.ver(`mesIncompleto(9, 2026, '04-09-2026', ${hoje})`), true);    // setembro: a última venda é do dia 4
  assert.equal(p.ver(`mesIncompleto(8, 2026, '04-09-2026', ${hoje})`), false);
  assert.equal(p.ver(`mesIncompleto(11, 2026, '', ${hoje})`), true);            // o mês a decorrer
  assert.equal(p.ver(`mesIncompleto(9, 2026, '30-09-2026', ${hoje})`), false);   // dados até ao último dia: completo
  assert.equal(p.ver('mesIncompleto(2, 2028, \'29-02-2028\', new Date(2028, 4, 1))'), false);   // ano bissexto
});

test('mês por omissão: o último mês completo', () => {
  const p = carregar(BASE);
  p.ctx.__s = SERIE().meses;
  assert.equal(p.ver('mesPorOmissao(__s)'), 2);   // agosto, não setembro (parcial)
  p.ctx.__s = SERIE().meses.slice(3);
  assert.equal(p.ver('mesPorOmissao(__s)'), 0);   // só há o parcial: abre esse
});

test('o resumo abre em agosto 2026 e a legenda abrevia o mês incompleto', () => {
  const html = painel().ver('vistaResumo()');
  assert.match(html, /<h2 id="mes-t"[^>]*>Agosto 2026<\/h2>/);
  assert.match(html, /Set\., parcial/);
});

test('os 4 indicadores do mês face ao mesmo mês do ano anterior', () => {
  const b = blocos(painel(), AGOSTO());
  assert.deepEqual(b.map(x => x.rotulo), ['Faturação', 'Documentos', 'Valor médio', 'Clientes ativos']);
  assert.equal(b[0].valor, `210${NBSP}444${NBSP}€`);
  assert.match(b[0].nota, /▲.*6,0%.* face a 2025/);                 // +6,0% (198 488,02 € -> 210 443,55 €)
  assert.match(b[1].nota, /▲.*7,0%/);                               // 3350 -> 3583
  assert.equal(b[2].valor, `58,73${NBSP}€`);                        // 210 443,55 / 3583
  assert.match(b[3].nota, /▼.*2,9%/);                               // 240 -> 233
  assert.match(b[3].nota, /class="dn"/);                            // descida a preto
  assert.match(b[0].nota, /class="up"/);                            // subida a azul
});

test('valor médio com zero documentos mostra —', () => {
  const b = blocos(painel(), M({ faturacao: 0, documentos: 0, clientes: 0 }, { faturacao: 100, documentos: 2, clientes: 1 }));
  assert.equal(b[2].valor, '—');
  assert.doesNotMatch(JSON.stringify(b), /NaN|Infinity/);
});

test('ano anterior a zero ou sem dados: "Sem dados de <ano>", sem dividir por zero', () => {
  const b = blocos(painel(), M({ faturacao: 500, documentos: 5, clientes: 2 }, { faturacao: 0, documentos: 0, clientes: 0 }));
  assert.deepEqual(b.map(x => x.nota), Array(4).fill('Sem dados de 2025'));
  assert.doesNotMatch(JSON.stringify(b), /NaN|Infinity/);
});

test('mês incompleto: as 4 notas dizem "Mês em curso"', () => {
  const b = blocos(painel(), M({ faturacao: 38087, documentos: 663, clientes: 96 }, { faturacao: 41079, documentos: 700, clientes: 90 }), true);
  assert.deepEqual(b.map(x => x.nota), Array(4).fill('Mês em curso'));
});

test('o cartão: rótulo, título, grelha, ajuda e botão que pergunta "Como correu agosto?"', () => {
  const p = painel('app.mes = 2;');
  p.ver(`indicadoresMes.set(chaveMes(2026, 8), { estado: 'ok', dados: ${JSON.stringify(AGOSTO())} })`);
  const html = p.ver('vistaResumo()');
  assert.match(html, /Mês selecionado/);
  assert.match(html, /Agosto 2026/);
  assert.equal((html.match(/class="k"/g) || []).length, 8);         // 4 do topo + 4 do mês
  assert.match(html, /Clique noutro mês do gráfico para ver os indicadores desse mês\./);
  assert.match(html, /data-perguntar="Como correu agosto\?"[^>]*>.*Perguntar ao Flow sobre agosto/s);
  assert.doesNotMatch(html, /sel-mes/);                             // a faixa por baixo do gráfico desapareceu
});

test('a carregar: 4 blocos com o mesmo tamanho; erro: mensagem simples e "Tentar outra vez"', () => {
  const p = painel('app.mes = 2;');
  let html = p.ver('vistaResumo()');
  assert.equal((html.match(/class="k k-espera"/g) || []).length, 4);
  assert.match(html, /aria-busy="true"/);
  p.ver("indicadoresMes.set(chaveMes(2026, 8), { estado: 'erro' })");
  html = p.ver('vistaResumo()');
  assert.match(html, /Não foi possível carregar os dados deste mês\./);
  assert.match(html, /data-repetir-mes[^>]*>Tentar outra vez/);
  assert.doesNotMatch(html, /HTTP|Error|undefined/);
});

test('o pedido do mês leva os filtros do painel e não o período', () => {
  const p = painel("app.periodo = 'TrimestreAtual'; app.filtros = { ...filtros.vazios(), zonas: ['Braga'], tipos: ['F'], cliente: '1001', de: '2026-08-01', ate: '2026-08-15' };");
  const q = new URLSearchParams(p.ver('chaveMes(2026, 8)'));
  assert.equal(q.get('ano'), '2026');
  assert.equal(q.get('mes'), '8');
  assert.deepEqual([q.get('zona'), q.get('tipodoc'), q.get('cliente'), q.get('de'), q.get('ate')], ['Braga', 'F', '1001', '01-08-2026', '15-08-2026']);
  assert.equal(q.has('periodo'), false);
  // outros filtros, outro pedido: nunca se reaproveita o resultado de outros filtros
  assert.notEqual(p.ver('chaveMes(2026, 8)'), painel().ver('chaveMes(2026, 8)'));
});

test('gráfico: o mês incompleto é um ponto solto "até dia 4", sem linha a ligá-lo', () => {
  const p = carregar(BASE);
  p.ctx.__m = SERIE().meses;
  const html = p.ver("graficoMeses({ meses: __m, sel: 2, ultimaData: '04-09-2026', W: 640, H: 270 })");
  assert.match(html, /viewBox="0 0 640 270"/);
  assert.match(html, /class="g-parcial"/);
  assert.match(html, /até dia 4/);
  const atual = html.match(/class="g-atual" points="([^"]+)"/)[1].trim().split(' ');
  assert.equal(atual.length, 3);                                    // jun, jul e ago: setembro fica de fora
});

test('gráfico: cada mês é focável e escolhe-se com Enter (tabindex, role e nome)', () => {
  const p = carregar(BASE);
  p.ctx.__m = SERIE().meses;
  const html = p.ver('graficoMeses({ meses: __m, sel: 2 })');
  const zonas = html.match(/<rect class="g-zona"[^>]*>/g);
  assert.equal(zonas.length, 4);
  assert.ok(zonas.every(z => /tabindex="0"/.test(z) && /role="button"/.test(z) && /data-mes="\d"/.test(z) && /aria-label="[^"]+ 2026: ver os indicadores do mês"/.test(z)));
  assert.match(zonas[2], /aria-pressed="true"/);
});

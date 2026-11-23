// Faturação: botão "Ver mais" em cada documento. Previsões: cartão "Mês em curso" (09-vistas.js)
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { carregar, BASE } = require('./carregar.js');

const FICHEIROS = [...BASE, '09-vistas'];
const MES = (m, atual, anterior, extra = {}) => ({ a: 2026, m, atual, anterior, delta: null, parcial: false, ...extra });
const REAIS = () => [MES(3, 268120, 220733), MES(4, 256446, 219605), MES(5, 243962, 238751), MES(6, 255448, 250414), MES(7, 278519, 270070), MES(8, 210443, 198488)];
const FUTUROS = (m = 9) => [m, m + 1, m + 2].map(x => ({ a: 2026, m: x, provavel: 241000, conservadora: 226000, otimista: 256000 }));
const DADOS = (extra = {}) => ({
  ate: '04-09-2026', anoAnt: '2025', alertas: [], clientes: [],
  serie: { anoAtual: 2026, anoAnt: '2025', meses: [...REAIS(), MES(9, 38087, 41079, { parcial: true })] },
  kpis: { faturacao: { valor: 1, variacao: 1 }, documentos: { valor: 1, variacao: 1 }, medio: { valor: 1, variacao: 1 }, clientes: { valor: 1, variacao: 1 } },
  faturacao: { docCreditos: 1, documentos: 10, bruto: 100, creditos: 0, liquido: 100 }, tiposDoc: [],
  ultimosDocs: [{ documento: 'FT 1/2026/7', tipo: 'F', data: '04-09-2026', cliente: 'CASA COSTA', total: 60.25 }, { documento: 'N 1/2026/3', tipo: 'N', data: '02-09-2026', cliente: 'CASA COSTA', total: -10 }],
  previsao: { reais: REAIS(), futuros: FUTUROS() }, semPrevisao: '', ...extra });
const painel = (extra) => {
  const p = carregar(FICHEIROS);
  p.ctx.__D = DADOS(extra);
  p.ver('app.dados = __D; app.aCarregar = false; app.erro = false; app.cenario = "provavel";');
  return p;
};

test('Faturação: cada documento tem "Ver mais", que abre os artigos desse documento', () => {
  const html = painel().ver('vistaFaturacao()');
  const botoes = html.match(/<button type="button" class="ligacao" data-doc="[^"]+" aria-label="[^"]+">Ver mais<\/button>/g);
  assert.equal(botoes.length, 2);
  assert.match(botoes[0], /data-doc="FT-1-2026-7"/);
  assert.match(botoes[0], /aria-label="Ver os artigos do documento FT 1\/2026\/7 de Casa Costa"/);
  assert.match(html, /Clique num documento para ver os seus artigos\./);
  assert.doesNotMatch(html, /pré-visualizar/i);
});

test('Previsões: o cartão "Mês em curso" mostra o faturado, o ritmo e o que falta por dia', () => {
  const html = painel().ver('vistaPrevisoes()');
  assert.match(html, /Mês em curso/);
  assert.match(html, /<h2 id="mes-t">Setembro 2026<\/h2>/);
  assert.match(html, /38 087 € de 241 000 €/);   // faturado e previsto provável
  assert.match(html, /4 de 30/);                                      // dias decorridos até à última venda
  assert.match(html, /Fecho ao ritmo atual/);
  assert.match(html, /Por dia para cumprir/);
  assert.match(html, /data-perguntar="Setembro vai cumprir a previsão\?"/);
  assert.doesNotMatch(html, /NaN|Infinity|Mês selecionado/);
});

test('Previsões: sem mês incompleto como primeiro mês previsto, não há cartão', () => {
  const html = painel({ serie: { anoAtual: 2026, anoAnt: '2025', meses: REAIS() } }).ver('vistaPrevisoes()');
  assert.doesNotMatch(html, /Mês em curso/);
  assert.doesNotMatch(html, /mes-linha/);
});

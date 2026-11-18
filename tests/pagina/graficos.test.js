// Gráficos PNG das ferramentas: o servidor manda "\u0001imagem data:..." depois do texto; a página junta-os à resposta (11-flow.js)
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { carregar, BASE } = require('./carregar.js');

const FICHEIROS = [...BASE, '11-flow'];
const PNG = 'data:image/png;base64,iVBORw0KGgo=';

test('separarImagens tira as imagens do texto, mesmo com a imagem longa e depois do texto', () => {
  const p = carregar(FICHEIROS);
  p.ctx.__t = `Vendas por mês: 2 meses.\n\u0001imagem ${PNG}\n`;
  const r = p.ver('separarImagens(__t)');
  assert.equal(r.texto, 'Vendas por mês: 2 meses.\n');
  assert.deepEqual([...r.imagens], [PNG]);
});

test('separarImagens ignora o que não é PNG em base64', () => {
  const p = carregar(FICHEIROS);
  p.ctx.__t = 'x\n\u0001imagem javascript:alert(1)\n\u0001imagem data:image/png;base64,"><script>\n';
  const r = p.ver('separarImagens(__t)');
  assert.deepEqual([...r.imagens], []);
  assert.equal(r.texto, 'x\n');
});

test('o chat mostra o gráfico com texto alternativo', () => {
  const p = carregar(FICHEIROS);
  const analise = { titulo: '', paragrafos: ['Vendas por mês'], metricas: [], avisos: [], opcoes: [], painel: null, linhas: null, sugestoes: [] };
  p.ctx.__i = { id: 1, pergunta: 'q', texto: 't', analise, imagens: [PNG] };
  const html = p.ver('respostaHtml(__i)');
  assert.match(html, new RegExp(`<img[^>]+src="${PNG}"`));
  assert.match(html, /<img[^>]+alt="[^"]+"/);
});

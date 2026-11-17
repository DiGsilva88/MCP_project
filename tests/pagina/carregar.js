// Ajuda dos testes: corre ficheiros da página (src/Vendas.Agente/Pagina/js) num contexto de node com um DOM de mentira,
// para testar a lógica (o HTML que gera, os cliques) sem browser.
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const DIR = path.join(__dirname, '../../src/Vendas.Agente/Pagina/js');

function elemento() {
  return {
    _h: {}, dataset: {}, style: {}, value: '', innerHTML: '', textContent: '', hidden: false, disabled: false,
    classList: { add() {}, remove() {}, toggle() {}, contains: () => false },
    addEventListener(tipo, f) { this._h[tipo] = f; },
    setAttribute() {}, removeAttribute() {}, append() {}, remove() {}, focus() {}, scrollTo() {},
    querySelector: () => null, querySelectorAll: () => [], closest: () => null,
  };
}

// ficheiros: nomes sem extensão, pela ordem da página (ex.: ['01-formatacao', '02-estado', ...])
function carregar(ficheiros, { vendedor = '' } = {}) {
  const porId = new Map(), toasts = [];
  const document = {
    body: Object.assign(elemento(), { dataset: { utilizador: 'Teste', vendedor }, append: x => toasts.push(x) }),
    getElementById: id => { if (!porId.has(id)) porId.set(id, elemento()); return porId.get(id); },
    createElement: () => elemento(), addEventListener() {}, querySelector: () => elemento(), querySelectorAll: () => [],
  };
  const ctx = vm.createContext({
    console, document, URL, URLSearchParams, TextEncoder, TextDecoder, Blob, AbortController, setTimeout, clearTimeout, setInterval, clearInterval,
    sessionStorage: { getItem: () => null, setItem() {}, removeItem() {} }, location: { hash: '' }, history: { replaceState() {} },
    matchMedia: () => ({ matches: false }), fetch: () => Promise.reject(new Error('sem rede')),
  });
  for (const f of ficheiros) vm.runInContext(fs.readFileSync(path.join(DIR, f + '.js'), 'utf8'), ctx, { filename: f });
  return { ctx, porId, toasts, ver: expr => vm.runInContext(expr, ctx) };
}

const BASE = ['01-formatacao', '02-estado', '03-utilitarios', '04-componentes', '05-leitura-respostas', '06-exportar', '07-respostas', '08-dados'];
module.exports = { carregar, BASE };

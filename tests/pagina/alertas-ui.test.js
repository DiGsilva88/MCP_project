// O separador Alertas: modelos, "Os meus alertas" e o que os alertas personalizados acrescentam aos recebidos (09-vistas.js e 08-dados.js)
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { carregar, BASE } = require('./carregar.js');

const FICHEIROS = [...BASE, '09-vistas'];
const D = () => ({
  ate: '04-09-2026', anoAnt: '2025', alertas: [], kpis: {}, carteira: [], artigosTodos: [],
  zonas: [{ id: 'LISBOA', nome: 'Lisboa', fat: 36, variacao: -99.7 }, { id: 'PORTO', nome: 'Porto', fat: 9000, variacao: 4 }],
});
const meu = (o = {}) => ({ id: 'a1', nome: 'Quedas de zona', tipo: 'zona', limite: 30, zona: '', freq: 'Uma vez por dia', mail: false, email: '', ligada: true, ultimo: '', ...o });
function painel(opcoes = {}, antes = '') {
  const p = carregar(FICHEIROS, opcoes);
  p.ctx.__D = D();
  p.ver(`app.dados = __D; app.aCarregar = false; app.erro = false; ${antes}`);
  return p;
}

test('Alertas personalizados: os 6 modelos e o botão Criar alerta', () => {
  const html = painel({}, "app.alertaAba = 'personalizados';").ver('vistaAlertas()');
  assert.match(html, /Começar a partir de um modelo/);
  assert.equal((html.match(/class="modelo"/g) || []).length, 6);
  assert.match(html, /data-novo-alerta="fatura"/);
  assert.match(html, /Criar alerta/);
});

test('o vendedor não vê o modelo de faturas em atraso (não tem o vendedor do cliente)', () => {
  const html = painel({ vendedor: '1' }, "app.alertaAba = 'personalizados';").ver('vistaAlertas()');
  assert.equal((html.match(/class="modelo"/g) || []).length, 5);
  assert.doesNotMatch(html, /data-novo-alerta="fatura"/);
});

test('Os meus alertas: nome, condição, frequência e último aviso', () => {
  const html = painel({}, "app.alertaAba = 'personalizados'; app.prefs.alertasPers = [{ id: 'a1', nome: 'Quedas em Braga', tipo: 'zona', limite: 30, zona: 'BRAGA', freq: 'Uma vez por semana', mail: true, email: 'ana@inforap.pt', ligada: true, ultimo: '2026-09-04' }];").ver('vistaAlertas()');
  assert.match(html, /Quedas em Braga/);
  assert.match(html, /da zona Braga cair mais de 30%/);
  assert.match(html, /Uma vez por semana/);
  assert.match(html, /4 de setembro/);
  assert.match(html, /<span class="tag">Email<\/span>/);
  assert.match(html, /data-ligar-alerta="a1"/);
});

test('sem aviso ainda: diz "Ainda não avisou"', () => {
  const html = painel({}, "app.alertaAba = 'personalizados'; app.prefs.alertasPers = [{ id: 'a1', nome: 'X', tipo: 'zona', limite: 30, zona: '', freq: 'Uma vez por dia', mail: false, email: '', ligada: true, ultimo: '' }];").ver('vistaAlertas()');
  assert.match(html, /Ainda não avisou/);
});

test('o que um alerta personalizado encontra aparece nos Alertas recebidos; desligado, não', () => {
  const ligado = painel({}, "app.prefs.alertasPers = [{ id: 'a1', nome: 'Quedas', tipo: 'zona', limite: 30, zona: '', freq: 'Uma vez por dia', mail: false, email: '', ligada: true, ultimo: '' }];");
  // JSON: o array vem de outro contexto de execução e o deepEqual estrito compara protótipos
  assert.deepEqual(JSON.parse(ligado.ver('JSON.stringify(alertasAtivos(app.dados).map(a => a.titulo))')), ['Lisboa caiu 99,7%']);
  assert.match(ligado.ver('vistaAlertas()'), /Lisboa caiu 99,7%/);

  const desligado = painel({}, "app.prefs.alertasPers = [{ id: 'a1', nome: 'Quedas', tipo: 'zona', limite: 30, zona: '', freq: 'Uma vez por dia', mail: false, email: '', ligada: false, ultimo: '' }];");
  assert.deepEqual(desligado.ver('alertasAtivos(app.dados).length'), 0);
});

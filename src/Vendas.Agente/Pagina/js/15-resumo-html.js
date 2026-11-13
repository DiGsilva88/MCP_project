    // Os indicadores vivem no topo (barraResumoHtml) e no painel lateral (desenharLateral)
    function resumoHtml(D) {
      // Previsão
      const P = D.previsao;
      let htmlPrev, htmlPrevKpis = '', htmlPrevMeses = '';
      if (!P) {
        htmlPrev = `<section class="res-cartao res-cartao--largo" data-bloco="prev"><div><h3>Previsão de faturação mensal</h3><p class="res-sub">Tendência dos últimos 6 meses completos</p></div>
          <p class="chat-texto">${rEsc(D.semPrevisao)}</p></section>`;
      } else {
        const { passo, max, min } = rEscala(P.real.concat(P.superior, P.inferior));
        const n = P.meses.length, off = P.real.length - 1;
        const X = i => 40 + i * (920 / (n - 1));
        const Y = v => 15 + (max - v) / (max - min) * 270;
        const pt = (arr, start) => arr.map((v, i) => [X(i + start), Y(v)]);
        const hi = pt(P.superior, off), lo = pt(P.inferior, off);
        const grelha = []; for (let g = Math.ceil(min / passo) * passo; g <= max; g += passo) grelha.push(g);
        const soma = a => a.slice(1).reduce((s, v) => s + v, 0);
        htmlPrev = `<section class="res-cartao res-cartao--largo" data-bloco="prev">
          <div><h3>Previsão de faturação mensal</h3><p class="res-sub">Tendência dos últimos 6 meses completos</p></div>
          <div class="res-legenda">
            <span><svg width="26" height="10" aria-hidden="true"><line x1="0" y1="5" x2="26" y2="5" stroke="var(--marca)" stroke-width="3"/><circle cx="13" cy="5" r="4" fill="var(--marca)"/></svg>Vendas</span>
            <span><svg width="26" height="10" aria-hidden="true"><line x1="0" y1="5" x2="26" y2="5" stroke="var(--marca)" stroke-width="3" stroke-dasharray="5 3"/><rect x="9" y="1" width="8" height="8" fill="#fff" stroke="var(--marca)" stroke-width="2" transform="rotate(45 13 5)"/></svg>Previsão</span>
            <span><svg width="26" height="10" aria-hidden="true"><line x1="0" y1="5" x2="26" y2="5" stroke="#9db7ff" stroke-width="2" stroke-dasharray="2 3"/></svg>Limite superior e inferior</span>
          </div>
          <div class="res-grafico" style="height:240px;margin-top:6px">
            ${grelha.map(g => `<span class="res-y" style="top:${Y(g) / 3}%">${rCurto(g)}</span>`).join('')}
            <svg viewBox="0 0 1000 300" preserveAspectRatio="none" aria-hidden="true">
              ${grelha.map(g => `<line x1="0" y1="${Y(g)}" x2="1000" y2="${Y(g)}" stroke="var(--linha)" vector-effect="non-scaling-stroke"/>`).join('')}
              <line x1="${X(off)}" y1="0" x2="${X(off)}" y2="300" stroke="var(--borda-forte)" stroke-dasharray="4 4" vector-effect="non-scaling-stroke"/>
              <path d="${rCurva(hi)} ${rCurva(lo.slice().reverse()).replace('M', 'L')} Z" fill="#dce6ff" fill-opacity="0.7"/>
              <path d="${rCurva(hi)}" fill="none" stroke="#9db7ff" stroke-width="2" stroke-dasharray="2 4" vector-effect="non-scaling-stroke"/>
              <path d="${rCurva(lo)}" fill="none" stroke="#9db7ff" stroke-width="2" stroke-dasharray="2 4" vector-effect="non-scaling-stroke"/>
              <path d="${rCurva(pt(P.real, 0))}" fill="none" stroke="var(--marca)" stroke-width="3" vector-effect="non-scaling-stroke"/>
              <path d="${rCurva(pt(P.previsto, off))}" fill="none" stroke="var(--marca)" stroke-width="3" stroke-dasharray="8 5" vector-effect="non-scaling-stroke"/>
            </svg>
            <span class="res-nota" style="position:absolute;top:-4px;left:${X(off) / 10}%;transform:translateX(-50%);background:var(--cartao);padding:0 4px;white-space:nowrap;font-size:12px">Último mês completo</span>
            ${P.real.map((v, i) => `<span class="res-ponto" style="left:${X(i) / 10}%;top:${Y(v) / 3}%;width:11px;height:11px;border-radius:50%;background:var(--marca);border:2px solid var(--cartao)"></span>
              <span class="res-rotulo" style="left:${X(i) / 10}%;top:calc(${Y(v) / 3}% - 9px);color:var(--texto)">${rCurto(v)}</span>`).join('')}
            ${P.previsto.slice(1).map((v, j) => { const i = j + 1 + off; return `<span class="res-ponto" style="left:${X(i) / 10}%;top:${Y(v) / 3}%;width:10px;height:10px;background:var(--cartao);border:2px solid var(--marca);transform:translate(-50%,-50%) rotate(45deg)"></span>
              <span class="res-rotulo" style="left:${X(i) / 10}%;top:calc(${Y(v) / 3}% - 10px);color:var(--marca)">${rCurto(v)}</span>`; }).join('')}
            ${P.meses.map((m, i) => { const real = i <= off;
              const aria = real ? `${P.nomes[i]}: vendas ${rEur(P.real[i])}` : `${P.nomes[i]}: previsão ${rEur(P.previsto[i - off])}, intervalo provável de ${rEur(P.inferior[i - off])} a ${rEur(P.superior[i - off])}`;
              return `<button type="button" class="res-zona" data-prev="${i}" aria-label="${rEsc(aria)}" style="left:${((X(i) - 46) / 10).toFixed(2)}%;width:9.2%"></button>`; }).join('')}
            <div class="res-tooltip" id="res-tooltip" role="status" hidden></div>
          </div>
          <div class="res-x">${P.meses.map((m, i) => `<span style="left:${X(i) / 10}%;${i > off ? 'font-style:italic;color:var(--marca)' : ''}">${m}</span>`).join('')}</div>
          <p class="chat-destaque"><strong>Até ${P.nomes[n - 1].toLowerCase()}, prevê-se cerca de ${rCurto(soma(P.previsto))}</strong> (entre ${rCurto(soma(P.inferior))} e ${rCurto(soma(P.superior))}).</p>
        </section>`;
        // Indicadores por cima do gráfico e um cartão por mês previsto por baixo (como no protótipo)
        const ultimo = P.real[off], obj = D.objetivo;   // D.objetivo: objetivo anual em € (ainda por vir na consulta)
        const pctObj = obj > 0 ? Math.min(100, D.total / obj * 100) : 0;
        htmlPrevKpis = `<div class="flow-kpis-grelha">
          <div class="flow-kpi flow-kpi--destaque"><span>${svgIc('previsoes', 15)}Previsão até ${P.nomes[n - 1].toLowerCase()}</span><strong>${rEur(soma(P.previsto))}</strong><span>entre ${rCurto(soma(P.inferior))} e ${rCurto(soma(P.superior))}</span></div>
          <div class="flow-kpi"><span>${svgIc('calendarioOk', 15)}Último mês completo · ${P.nomes[off].toLowerCase()}</span><strong>${rEur(ultimo)}</strong><span>Valor real</span></div>
          <div class="flow-kpi"><span>${svgIc('etiqueta', 15)}Objetivo anual${obj > 0 ? ' · ' + rEur(obj) : ''}</span><strong>${obj > 0 ? rPct(D.total / obj * 100) : '—'}</strong>
            ${obj > 0 ? `<span class="res-quota" aria-hidden="true"><span style="width:${pctObj.toFixed(1)}%"></span></span>` : '<span>Ainda sem dados</span>'}</div>
        </div>`;
        htmlPrevMeses = `<div class="res-prev-meses">${P.previsto.slice(1).map((v, j) => `<div class="flow-kpi"><span>${P.nomes[off + j + 1]}</span><strong>${rEur(v)}</strong>
          <span>entre ${rCurto(P.inferior[j + 1])} e ${rCurto(P.superior[j + 1])}</span></div>`).join('')}</div>`;
      }

      // Precisa de atenção
      const IC = { dados: ['calendarioX', 'color:var(--aviso-texto)'], artigo: ['artigos', 'color:var(--suave)'],
                   local: ['aviso', 'color:var(--desce)'], cliente: ['semCliente', 'color:var(--ligacao)'] };
      // Protótipo: ícone, o que se passa (tag) e o detalhe; com pergunta, a linha toda prepara-a na conversa
      const alertaHtml = a => {
        const corpo = `<span class="res-alerta-ic" style="${(IC[a.tipo] || IC.dados)[1]}">${svgIc((IC[a.tipo] || IC.dados)[0], 18)}</span>
          <span class="res-alerta-txt"><b>${rEsc(a.titulo)}</b><span>${rEsc(a.texto)}</span></span>`;
        return a.pergunta ? `<li><button type="button" class="res-alerta" data-perguntar="${rEsc(a.pergunta)}" title="Perguntar ao Flow">${corpo}</button></li>` : `<li><div class="res-alerta">${corpo}</div></li>`;
      };
      const emAtencao = alertasAtivos(D);
      const htmlAten = `<section class="res-cartao res-cartao--atencao" data-bloco="aten">
        <div class="res-cab"><h3>Precisa de atenção</h3><span class="res-contagem">${emAtencao.length}</span>${emAtencao.length ? '<button type="button" class="res-ver-alertas" data-ver-secao="alertas">Ver alertas</button>' : ''}</div>
        ${emAtencao.length ? `<ul class="res-alertas">${emAtencao.slice(0, 3).map(alertaHtml).join('')}</ul>` : '<p class="res-nota">Nada a assinalar neste período.</p>'}
      </section>`;

      const pctTotal = a => D.total > 0 ? rPct(a.num / D.total * 100) : '–';


      // Variação numa célula de tabela: sem vendas no ano anterior diz "Novo" (o título explica)
      const celVar = i => i.delta == null && i.novo ? `<span class="res-tab-nota" title="Sem vendas em ${rEsc(i.novo)}">Novo</span>` : `<span class="${rVarCls(i.delta)}">${rVar(i.delta)}</span>`;
      const vsAnt = 'vs ' + rEsc(D.anoAnt || 'ano anterior');
      // Cartão à espera de colunas que a consulta ainda não devolve (contrato mais abaixo, antes dos cartões da Faturação)
      const semDados = texto => `<p class="res-vazio">${svgIc('relogio', 16)}<span><b>Ainda sem dados</b>${texto}</span></p>`;

      // Artigos: gráfico circular do total geral de vendas por artigo (os 5 maiores e o resto em "Outros"),
      // com a % escrita nas fatias que têm espaço e a legenda ao lado. Por baixo, o top 5 em lista.
      const CORES_ART = ['--serie-1', '--serie-2', '--serie-3', '--serie-4', '--serie-5'];
      const topArt = D.artigos.slice(0, CORES_ART.length);
      const somaTopArt = topArt.reduce((s, a) => s + a.num, 0);
      const baseArt = Math.max(D.artigosValor || D.total || 0, somaTopArt);
      const outrosArt = Math.max(0, baseArt - somaTopArt);
      const nOutrosArt = D.artigosTotal > topArt.length ? D.artigosTotal - topArt.length : null;
      let htmlArtRoda = '';
      if (topArt.length && baseArt > 0) {
        const partes = topArt.map((a, k) => ({ nome: a.nome, num: a.num, cor: `var(${CORES_ART[k]})`, texto: '#fff' }));
        if (outrosArt > 0) partes.push({ nome: nOutrosArt ? `Restantes ${rGrupos(nOutrosArt)} artigos` : 'Restantes artigos', num: outrosArt, cor: 'var(--borda-forte)', texto: 'var(--texto)' });
        const folga = partes.length > 1 ? 0.6 : 0;
        let ini = 0;
        const fatias = [], rotulos = [];
        partes.forEach(p => {
          const parte = p.num / baseArt * 100, len = Math.max(0.4, parte - folga);
          fatias.push(`<circle cx="60" cy="60" r="46" pathLength="100" stroke-dasharray="${len.toFixed(2)} ${(100 - len).toFixed(2)}" stroke-dashoffset="${(25 - ini).toFixed(2)}" style="stroke:${p.cor}"><title>${rEsc(p.nome)}: ${rEur(p.num)} (${rPct(parte)})</title></circle>`);
          // A % só vai dentro da fatia quando cabe (≥ 6%); as outras estão no top 5 por baixo e na dica
          if (parte >= 6) {
            const ang = (ini + parte / 2) / 100 * 2 * Math.PI - Math.PI / 2;
            rotulos.push(`<text x="${(60 + 46 * Math.cos(ang)).toFixed(1)}" y="${(60 + 46 * Math.sin(ang)).toFixed(1)}" style="fill:${p.texto}">${rPct(parte)}</text>`);
          }
          ini += parte;
        });
        htmlArtRoda = `<div class="res-art-circulo">
          <div class="chat-circulo-roda res-art-roda"><svg viewBox="0 0 120 120" role="img" aria-label="${rEsc(partes.map(p => `${p.nome} ${rPct(p.num / baseArt * 100)}`).join(', '))}">${fatias.join('')}<g class="res-art-pct" aria-hidden="true">${rotulos.join('')}</g></svg>
            <button type="button" class="chat-circulo-centro res-art-total" data-art-ver aria-expanded="${resumoEstado.artVer}" title="Total de vendas: ${rEur2(baseArt)}. ${resumoEstado.artVer ? 'Ver só o top 5' : 'Ver a lista dos artigos'}"
              aria-label="Total de vendas ${rEur2(baseArt)}. ${resumoEstado.artVer ? 'Ver só o top 5' : 'Ver a lista dos artigos'}"><span>Total de vendas</span><strong>${rCurto(baseArt)}</strong><small>${resumoEstado.artVer ? 'Ver só o top 5' : 'Ver artigos'}</small></button></div>
        </div>
        ${outrosArt > 0 ? `<p class="res-art-nota"><i class="res-art-ponto" style="background:var(--borda-forte)" aria-hidden="true"></i><span>A cinzento: ${nOutrosArt ? `os restantes ${rGrupos(nOutrosArt)} artigos` : 'os restantes artigos'} (${rPct(outrosArt / baseArt * 100)} das vendas), cada um com menos de ${rEur(topArt[topArt.length - 1].num)}.</span></p>` : ''}`;
      }
      const pontoArt = k => k < CORES_ART.length ? `<i class="res-art-ponto" style="background:var(${CORES_ART[k]})" aria-hidden="true"></i>` : '';

      // Artigos mais vendidos: gráfico circular e, por baixo, o top 5 em lista.
      // Clicar no total mostra os artigos todos que a consulta trouxe (os 100 maiores), em páginas.
      // Valor | Unidades: em unidades é outra lista (os que mais unidades venderam), pedida só quando se carrega no botão.
      const porUn = resumoEstado.artMetrica === 'unidades' && !!D.artigosQtd;
      const todosArt = porUn ? D.artigosQtd : D.artigosTodos || D.artigos;
      const topLista = porUn ? todosArt.slice(0, CORES_ART.length) : topArt;
      const verTodos = resumoEstado.artVer && todosArt.length > topLista.length;
      const { itens: listaArt, html: paginacaoArt } = verTodos ? paginar(todosArt, 'artPag', 'data-art-pag', 'Páginas de artigos') : { itens: topLista, html: '' };
      const inicioArt = verTodos ? todosArt.indexOf(listaArt[0]) : 0;
      const estadoQtd = resumoEstado.artQtdACarregar ? '<span class="res-sub" role="status">A carregar as unidades…</span>'
        : resumoEstado.artQtdErro ? '<span class="res-sub" role="status">Não foi possível carregar as unidades. Tente de novo.</span>' : '';
      const metricasArt = `<div class="res-pilulas" role="group" aria-label="Ordenar artigos por">${[['valor', 'Valor'], ['unidades', 'Unidades']].map(([id, nome]) =>
        `<button type="button" class="res-pilula" data-art-metrica="${id}" aria-pressed="${resumoEstado.artMetrica === id}">${nome}</button>`).join('')}${estadoQtd}</div>`;
      const nomeArt = (a, k) => `<span class="res-tab-nome" title="${rEsc(a.nome)}">${porUn ? '' : pontoArt(k)}${verTodos ? `${k + 1}. ` : ''}${rEsc(a.nome)}${a.codigo || a.familia ? `<small>${[a.codigo, a.familia].filter(Boolean).map(rEsc).join(' · ')}</small>` : ''}</span>`;
      // Em unidades: "18 390 unidades" e "436 176,07 €" vêm assim da ferramenta; a variação é a das unidades
      const linhaArt = (a, k) => porUn
        ? `<div class="res-tab-linha res-tab--art-qtd">${nomeArt(a, k)}<strong>${rEsc(a.valor.replace(/\s*unidades$/, ''))}</strong><span>${a.pct ? rEur(paraNumero(a.pct)) : '–'}</span>${celVar(a)}</div>`
        : `<div class="res-tab-linha res-tab--art">${nomeArt(a, k)}
            <strong>${rEur(a.num)}</strong><span>${baseArt > 0 ? rPct(a.num / baseArt * 100) : '–'}</span>${celVar(a)}</div>`;
      const cabArt = porUn ? `<span>Unidades</span><span>Faturação</span>` : `<span>Faturação</span><span>% total</span>`;
      const htmlArt = `<section class="res-cartao res-tabela" data-bloco="art">
        <div class="res-cab"><h3>Artigos mais vendidos</h3><span class="res-sub">Por ${porUn ? 'unidades' : 'faturação'} · total ${rEur2(baseArt)}${D.artigosTotal ? ` · de ${rGrupos(D.artigosTotal)} artigos` : ''}</span></div>
        ${htmlArtRoda}
        ${topArt.length ? metricasArt : ''}${topLista.length ? `<div class="res-tab-linha res-tab-cab ${porUn ? 'res-tab--art-qtd' : 'res-tab--art'}" aria-hidden="true"><span>${verTodos ? `Os ${todosArt.length} maiores` : `Top ${topLista.length}`}</span>${cabArt}<span>${vsAnt}</span></div>
          ${listaArt.map((a, i) => linhaArt(a, inicioArt + i)).join('')}
          ${paginacaoArt}`
          : '<p class="res-nota">Sem vendas de artigos neste período.</p>'}
      </section>`;

      // Marcas: ficha rápida como a dos clientes. O gráfico (este ano e o anterior) escolhe a marca (as 6 que mais faturam).
      // A família na BD é quase toda "Familia Geral", por isso usa-se a marca.
      // As marcas vêm como códigos do ERP ("INA_", "LUK_"): tira-se o "_" de enchimento.
      let htmlArtLado = '';
      if (D.artigos.length) {
        const marcas = D.marcas || [];
        const nomeMarca = n => n.replace(/_+$/, '');
        const mSel = marcas.find(m => m.nome === resumoEstado.marcaSel) || marcas[0];
        const posicao = mSel ? marcas.indexOf(mSel) + 1 : 0;
        const maxM = Math.max(1, ...marcas.flatMap(m => [m.valor, m.anterior || 0]));
        const larg = v => (v / maxM * 100).toFixed(1) + '%';
        htmlArtLado = `<section class="res-cartao res-cartao--lado res-ficha-rapida" data-bloco="art-lado">
          <h3>Vendas por marca</h3>
          ${mSel ? `${D.anoAnt ? `<div class="res-legenda"><span><i aria-hidden="true"></i>${rEsc(D.serie ? D.serie.anoAtual : 'Este ano')}</span><span><i class="res-barra-ant" aria-hidden="true"></i>${rEsc(D.anoAnt)}</span></div>` : ''}
            <div class="res-marcas" role="group" aria-label="Vendas por marca: escolher marca">${marcas.map(m =>
              `<button type="button" class="res-marca" data-marca-sel="${rEsc(m.nome)}" aria-pressed="${m === mSel}" title="${rEsc(m.nome)}: ${rEur(m.valor)}${m.anterior ? ` · ${rEsc(D.anoAnt)}: ${rEur(m.anterior)}` : ''}">
                <span>${rEsc(nomeMarca(m.nome))}</span><strong>${rCurto(m.valor)}</strong>
                <span class="res-marca-barras" aria-hidden="true"><i style="width:${larg(m.valor)}"></i>${m.anterior ? `<i class="res-barra-ant" style="width:${larg(m.anterior)}"></i>` : ''}</span></button>`).join('')}</div>
            <h4>${rEsc(nomeMarca(mSel.nome))}</h4>
            <small>${posicao}.ª marca em faturação${D.marcasTotal ? `, de ${rGrupos(D.marcasTotal)}` : ''}</small>
            <div class="res-ficha-num">
              <div><span>Faturação</span><strong>${rEur(mSel.valor)}</strong></div>
              <div><span>${vsAnt}</span><strong class="${rVarCls(mSel.delta)}">${rVar(mSel.delta)}</strong></div>
              <div><span>Peso nas vendas</span><strong>${baseArt > 0 ? rPct(mSel.valor / baseArt * 100) : '–'}</strong></div>
              <div><span>Valor médio</span><strong>${mSel.media ? rEsc(mSel.media) : '–'}</strong></div>
            </div>
            <button type="button" class="res-btn-flow" data-perguntar="Comparar as vendas por marca com o ano passado">${svgIc('brilho', 15)}Perguntar ao Flow</button>`
            : semDados('A faturação de cada marca aparece aqui.')}
        </section>`;
      }

      // Zonas (como na demonstração): um mosaico por distrito e, por baixo, o detalhe do escolhido.
      // Clicar filtra o painel por esse distrito; o mosaico mostra sempre todos. Num cliente não se mostra.
      let htmlLoc = '', htmlZonaDet = '';
      if (D.locais && D.locais.length) {
        const somaLocais = D.locais.reduce((s, l) => s + l.fat, 0), maxLocal = Math.max(...D.locais.map(l => l.fat));
        htmlLoc = `<div class="res-zonas" role="group" aria-label="Vendas por distrito">${D.locais.map(l => `<button type="button" class="res-zona-tile" data-zona="${rEsc(l.id)}" aria-pressed="${resumoEstado.zona === l.id}">
            <b>${rEsc(l.nome)}</b><strong>${rEur(l.fat)}</strong>
            <span>${l.variacao == null ? (l.novo ? `<span class="res-tab-nota">Sem vendas em ${rEsc(l.novo)}</span>` : '') : chipVar(l.variacao)}${l.clientes != null ? `<small>${rGrupos(l.clientes)} ${l.clientes === 1 ? 'cliente' : 'clientes'}</small>` : ''}</span>
            <span class="res-quota ${l.variacao < 0 ? 'res-quota--desce' : ''}" aria-hidden="true"><span style="width:${(l.fat / maxLocal * 100).toFixed(1)}%"></span></span></button>`).join('')}</div>`;

        const sel = D.locais.find(l => l.id === resumoEstado.zona);
        const ms = D.serie ? D.serie.meses : [], maxMes = Math.max(1, ...ms.flatMap(x => [x.atual || 0, x.anterior || 0]));
        const colsZ = `style="grid-template-columns:repeat(${ms.length},minmax(0,1fr))"`;
        const parcial = x => MESES[x.m - 1] === D.mesIncompleto;
        const altura = v => ((v || 0) > 0 ? v / maxMes * 100 : 0).toFixed(1);
        const corAtual = sel && sel.variacao < 0 ? 'res-barra-desce' : '';
        const htmlBarras = ms.length ? `<div class="res-barras res-barras--par" ${colsZ}>${ms.map(x => `<div title="${MESES[x.m - 1]}: ${rEur(x.atual || 0)}${x.anterior != null ? ` · ${rEsc(D.anoAnt)}: ${rEur(x.anterior)}` : ''}"><i class="res-barra-ant" style="height:${altura(x.anterior)}%"></i><i class="${corAtual} ${parcial(x) ? 'res-barra-parcial' : ''}" style="height:${altura(x.atual)}%"></i></div>`).join('')}</div>
          <div class="res-barras-rot" aria-hidden="true" ${colsZ}>${ms.map(x => `<span>${MESES[x.m - 1].slice(0, 3).toLowerCase()}${parcial(x) ? '*' : ''}</span>`).join('')}</div>` : '<p class="res-nota">Sem vendas por mês neste período.</p>';
        const clientesZona = (D.clientes || []).slice(0, 6);
        htmlZonaDet = `<section class="res-cartao res-zona-graf" data-bloco="zona-det">
            <div class="res-cab"><h3>${rEsc(sel ? sel.nome : 'Todos os distritos')} · faturação mensal</h3>
              <span class="res-sub">${sel ? `${rPct(sel.fat / somaLocais * 100)} do total${sel.clientes ? ` · média ${rEur(sel.fat / sel.clientes)} por cliente` : ''}` : 'Clique num distrito para ver o detalhe'}</span></div>
            <div class="res-legenda"><span><i aria-hidden="true"></i>${rEsc(D.serie ? D.serie.anoAtual : '')}</span>${D.anoAnt ? `<span><i class="res-barra-ant" aria-hidden="true"></i>${rEsc(D.anoAnt)}</span>` : ''}</div>
            ${htmlBarras}
          </section>
          <section class="res-cartao res-cartao--lado" data-bloco="zona-cli">
            <h3>${sel ? 'Clientes no distrito' : 'Principais clientes'}</h3>
            ${clientesZona.length ? clientesZona.map(c => `<button type="button" class="res-zona-cli" data-cliente="${rEsc(c.codigo || c.nome)}" title="${rEsc(c.nome)}: ver resumo"><span>${rEsc(nomeProprio(c.nome))}</span><span class="${rVarCls(c.delta)}">${rVar(c.delta)}</span></button>`).join('') : '<p class="res-nota">Sem clientes com vendas neste período.</p>'}
            ${sel && !sel.sem ? `<button type="button" class="res-btn-flow" data-perguntar="${rEsc(`Comparar as vendas do distrito ${sel.nome} mês a mês com o ano passado`)}">${svgIc('brilho', 15)}Analisar distrito com o Flow</button>` : ''}
          </section>`;
      }

      // Localidades (no estilo dos Artigos): tabela e, ao lado, o peso das maiores. Clicar numa prepara uma pergunta ao Flow.
      let htmlLocTab = '', htmlLocLado = '';
      const locs = D.localidades || [];
      if (locs.length) {
        const nomeLoc = l => l.semDados ? 'Sem localidade na ficha' : nomeProprio(l.nome);
        const maxL = Math.max(...locs.map(l => l.num));
        const celulas = l => `<span class="res-tab-nome" title="${rEsc(l.nome)}">${rEsc(nomeLoc(l))}</span><strong>${rEur(l.num)}</strong><span>${pctTotal(l)}</span>${celVar(l)}`;
        htmlLocTab = `<section class="res-cartao res-tabela" data-bloco="loc-tab">
          <div class="res-cab"><h3>Localidades com mais vendas</h3><span class="res-sub">Por faturação${D.localidadesTotal > locs.length ? ` · as ${locs.length} maiores, de ${D.localidadesTotal}` : ''}</span></div>
          <div class="res-tab-linha res-tab-cab res-tab--art" aria-hidden="true"><span>Localidade</span><span>Faturação</span><span>% total</span><span>${vsAnt}</span></div>
          ${locs.map(l => l.semDados ? `<div class="res-tab-linha res-tab--art">${celulas(l)}</div>`
            : `<button type="button" class="res-tab-linha res-tab--art" data-perguntar="${rEsc(`Comparar as vendas da localidade ${nomeProprio(l.nome)} mês a mês com o ano passado`)}">${celulas(l)}</button>`).join('')}
        </section>`;
        htmlLocLado = `<section class="res-cartao res-cartao--lado" data-bloco="loc-lado">
          <h3>Peso das maiores localidades</h3>
          ${locs.slice(0, 6).map(l => `<div class="res-peso"><div><span title="${rEsc(l.nome)}">${rEsc(nomeLoc(l))}</span><strong>${rCurto(l.num)}</strong></div>
            <span class="res-quota"><span style="width:${(l.num / maxL * 100).toFixed(1)}%"></span></span></div>`).join('')}
          ${resDestaques(D.localidadesSobe, D.localidadesDesce, D.anoAnt, i => nomeProprio(i.nome))}
          <button type="button" class="res-btn-flow" data-perguntar="Que localidades venderam mais este ano?">${svgIc('brilho', 15)}Perguntar ao Flow</button>
        </section>`;
      }

      // Resumo — principais clientes (protótipo): nome, faturação e variação; a linha abre o resumo do cliente
      let htmlCli = '';
      if (D.clientes && D.clientes.length) {
        htmlCli = `<section class="res-cartao res-principais" data-bloco="cli">
          <div class="res-cab"><h3>Principais clientes</h3><button type="button" class="res-ver-todos" data-ver-secao="clientes">Ver todos</button></div>
          ${D.clientes.slice(0, 6).map(c => `<button type="button" class="res-principal" data-cliente="${rEsc(c.codigo || c.nome)}" title="${rEsc(c.nome)}: ver resumo">
            <span>${rEsc(nomeProprio(c.nome))}</span><strong>${rEur(c.num)}</strong><span class="res-mes-var ${c.delta == null ? '' : c.delta >= 0 ? 'res-mes-var--sobe' : 'res-mes-var--desce'}">${rVar(c.delta)}</span></button>`).join('')}
        </section>`;
      }

      // Vista Clientes: separadores por estado, tabela e a ficha rápida do cliente escolhido
      let htmlCliTab = '', htmlCliLado = '';
      const carteira = D.carteira || [];
      if (carteira.length) {
        const CLS_ESTADO = { Ativo: 'res-estado--ativo', 'Em risco': 'res-estado--risco', Perdido: 'res-estado--perdido' };
        const chipEstado = c => c.estado ? `<span class="res-estado ${CLS_ESTADO[c.estado]}">${c.estado}</span>` : '';
        const zonaDe = c => !c.zona ? '–' : /^\(/.test(c.zona) ? 'Sem zona' : c.zona;
        // Sem a consulta da carteira (erro) não há estados: a tabela aparece inteira, sem separadores
        const comEstado = carteira.some(c => c.estado);
        const seg = comEstado ? resumoEstado.cliSeg : 'Todos';
        const lista = seg === 'Todos' ? carteira : carteira.filter(c => c.estado === seg);
        const { itens: pagina, html: paginacao } = paginar(lista, 'cliPag', 'data-cli-pag', 'Páginas de clientes');
        const cliSel = pagina.find(c => (c.codigo || c.nome) === resumoEstado.cliSel) || pagina[0];
        const separadores = [['Todos', 'Todos'], ['Ativo', 'Ativos'], ['Em risco', 'Em risco'], ['Perdido', 'Perdidos']].map(([id, nome]) =>
          `<button type="button" class="res-pilula" data-cli-seg="${id}" aria-pressed="${seg === id}">${nome}<span>${id === 'Todos' ? carteira.length : carteira.filter(c => c.estado === id).length}</span></button>`).join('');
        const nota = D.clientesTotal > carteira.length ? `<span class="res-sub">Os ${carteira.length} maiores, de ${rGrupos(D.clientesTotal)}</span>` : '';
        htmlCliTab = `<section class="res-cartao res-tabela" data-bloco="cli-tab">
          ${comEstado ? `<div class="res-pilulas" role="group" aria-label="Filtrar clientes por estado">${separadores}${nota}</div>` : ''}
          <div class="res-tab-linha res-tab-cab res-tab--cli" aria-hidden="true"><span>Cliente</span><span>Zona</span><span>Faturação</span><span>${vsAnt}</span><span>Última</span><span>Estado</span></div>
          ${pagina.map(c => `<button type="button" class="res-tab-linha res-tab--cli" data-cli-sel="${rEsc(c.codigo || c.nome)}" aria-pressed="${c === cliSel}" title="${rEsc(c.nome)}${c.codigo ? ` (${rEsc(c.codigo)})` : ''}: ver ficha rápida">
            <span class="res-tab-nome">${rEsc(c.nome)}</span><span>${rEsc(zonaDe(c))}</span><strong>${rEur(c.num)}</strong>
            <span class="${rVarCls(c.delta)}">${rVar(c.delta)}</span><span title="${rEsc(c.ultima || '')}">${c.ultima ? dataCurta(c.ultima) : '–'}</span><span>${chipEstado(c)}</span></button>`).join('')
            || '<p class="res-nota">Nenhum cliente com este estado. Escolha outro separador.</p>'}
          ${paginacao}
        </section>`;
        if (cliSel) {
          const idCli = cliSel.codigo || cliSel.nome, nomeCli = nomeProprio(cliSel.nome);
          const anoDoPeriodo = resumoEstado.periodo === 'AnoAtual' ? ' ' + new Date().getFullYear() : '';
          const subtitulo = [cliSel.zona && zonaDe(cliSel), cliSel.documentos != null && `${rGrupos(cliSel.documentos)} ${cliSel.documentos === 1 ? 'documento' : 'documentos'}`].filter(Boolean).join(' · ')
            || (cliSel.codigo ? 'Cliente ' + cliSel.codigo : '');
          // Um valor por mês do período, com zero nos meses sem compras
          const meses = D.serie ? D.serie.meses : [];
          const compras = new Map((cliSel.meses || []).map(x => [x.mes, x.valor]));
          const compraDe = x => compras.get(`${x.a}-${String(x.m).padStart(2, '0')}`) || 0;
          const maxCompra = Math.max(1, ...meses.map(compraDe));
          const barras = cliSel.meses && meses.length ? `<div class="res-compras"><span>Compras por mês${anoDoPeriodo}</span>
            <div class="res-compras-barras ${cliSel.estado === 'Ativo' ? '' : 'res-compras-barras--parado'}" role="img" aria-label="${rEsc(meses.map(x => `${MESES[x.m - 1]}: ${rEur(compraDe(x))}`).join(', '))}">${meses.map(x =>
              `<i title="${MESES[x.m - 1]}: ${rEur(compraDe(x))}" style="height:${Math.max(4, compraDe(x) / maxCompra * 100).toFixed(0)}%${x.parcial ? ';opacity:.45' : ''}"></i>`).join('')}</div></div>` : '';
          htmlCliLado = `<section class="res-cartao res-cartao--lado res-ficha-rapida" data-bloco="cli-lado">
            <h3>Ficha rápida</h3>
            <h4>${rEsc(nomeCli)}</h4>
            ${subtitulo ? `<small>${rEsc(subtitulo)}</small>` : ''}
            ${chipEstado(cliSel)}
            <div class="res-ficha-num">
              <div><span>Faturação${anoDoPeriodo}</span><strong>${rEur(cliSel.num)}</strong></div>
              <div><span>${vsAnt}</span><strong class="${rVarCls(cliSel.delta)}">${rVar(cliSel.delta)}</strong></div>
              <div><span>Valor médio</span><strong>${cliSel.media ? rEsc(cliSel.media) : '–'}</strong></div>
              <div><span>Última compra</span><strong>${cliSel.ultima ? dataCurta(cliSel.ultima) : '–'}</strong></div>
            </div>
            ${barras}
            <button type="button" class="res-btn-flow" data-perguntar="${rEsc(`Que artigos comprou o cliente ${nomeCli} este ano?`)}">${svgIc('brilho', 15)}Perguntar ao Flow</button>
            <button type="button" class="res-btn-negro" data-ficha="${rEsc(idCli)}" data-ficha-nome="${rEsc(cliSel.nome)}">${svgIc('ficha', 15)}Abrir ficha completa</button>
          </section>`;
        }
      }

      // Cartões do protótipo. Campos de D, todos opcionais; sem eles o cartão mostra "Ainda sem dados":
      //   D.faturacao     faturacaoDe(): { fat, rec, total }, cada um com bruto, creditos, liquido, taxa, documentos
      //   D.tiposDoc      [{ nome, valor, docs }]   (um por TipoDocID, para a exportação)
      //   D.docsPorMes    [{ m (1-12), a, n (n.º de documentos), parcial?, fat, rec, total }]
      //   D.creditos, D.ultimosDocs   [{ doc (DocumentoID), cliente, data ('dd-mm-aaaa'), total }] (créditos: também tipo)
      // Ainda à espera de colunas que a consulta não devolve:
      //   D.novosClientes { n, dif (diferença face ao ano anterior) }
      //   D.objetivo  (objetivo anual em €)
      //   em cada artigo de D.artigos: familia
      // Faturação — líquido em destaque e um cartão por fluxo (faturas e faturas-recibo)
      const Fat = D.faturacao;
      const taxaDe = f => f.taxa == null ? '–' : rPct(f.taxa);
      // Rótulos curtos para caber tudo numa linha; o nome completo fica na dica
      const fila = (nome, valor, dica, cls = '') => `<div><dt title="${dica}">${nome}</dt><dd class="${cls}">${valor}</dd></div>`;
      const cartaoFluxo = (nome, nomeCredito, dicaCredito, f, base) => `<div class="flow-kpi"><span>${svgIc('faturacao', 15)}${nome}</span><strong>${rEur(f.bruto)}</strong><span>Bruto · ${rGrupos(f.documentos)} doc.</span>
        <dl class="res-fluxo">${fila(nomeCredito, rEur(f.creditos), dicaCredito, 'res-neg')}${fila('Taxa de crédito', taxaDe(f), 'Em percentagem ' + base)}${fila('Líquido', rEur(f.liquido), 'Bruto menos créditos')}</dl></div>`;
      const htmlTipos = Fat ? `<div class="flow-kpis-grelha flow-kpis-grelha--fat">
        <div class="flow-kpi flow-kpi--destaque"><span>${svgIc('euro', 15)}Faturação líquida</span><strong>${rEur(Fat.total.liquido)}</strong><span>Sem IVA, após créditos</span>
          <dl class="res-fluxo">${fila('Bruto', rEur(Fat.total.bruto), 'Faturas e faturas-recibo')}${fila('Créditos', rEur(Fat.total.creditos), 'Notas de crédito e créditos')}${fila('Taxa de crédito', taxaDe(Fat.total), 'Em percentagem do bruto')}</dl></div>
        ${cartaoFluxo(TIPOS_DOC.F, 'Notas de crédito', TIPOS_DOC.N, Fat.fat, 'do bruto de faturas')}
        ${cartaoFluxo(TIPOS_DOC.V, 'Créditos', TIPOS_DOC.C, Fat.rec, 'do bruto de faturas-recibo')}
        <div class="flow-kpi"><span>${svgIc('faturacao', 15)}Total de documentos</span><strong>${rGrupos(Fat.total.documentos + Fat.total.docCreditos)}</strong><span>No período escolhido</span>
          <dl class="res-fluxo">${fila('Vendas (F + V)', rGrupos(Fat.total.documentos), 'Faturas e faturas-recibo')}${fila('Créditos (N + C)', rGrupos(Fat.total.docCreditos), 'Notas de crédito e créditos')}</dl></div>
      </div>` : '';

      // Vista Faturação em 3 secções: números (htmlTipos), evolução (por mês ou acumulado) e documentos (créditos ou últimos emitidos)
      const separador = (atr, id, nome, ativo) => `<button type="button" class="res-pilula" data-${atr}="${id}" aria-pressed="${ativo}">${nome}</button>`;

      // Acumulado vs. ano anterior (os mesmos dias do ano anterior): a frase fica sempre à vista; o gráfico é a 2.ª vista da evolução
      let fraseAcum = '', acumCorpo = '';
      const A = D.acumulado;
      if (A) {
        const n = A.rotulos.length, na = A.atual.length;
        const topo = rEscala([...A.anterior, ...A.atual]).max;
        const BX = i => 20 + i * (960 / Math.max(1, n - 1)), BY = v => 300 - v / topo * 285;
        const linha = arr => arr.map((v, i) => (i ? 'L' : 'M') + BX(i).toFixed(1) + ',' + BY(v).toFixed(1)).join(' ');
        const at = A.atual[na - 1], an = A.anterior[na - 1], dif = at - an;
        // Último ponto perto do fim do eixo: os rótulos vão para a esquerda (à direita saíam do cartão)
        const ladoRotulo = BX(na - 1) > 800 ? 'translate(calc(-100% - 6px),-100%)' : 'translate(6px,-100%)';
        fraseAcum = an > 0 ? `<p class="chat-destaque"><strong>Até ${A.nomes[na - 1]}, vai ${rDec(Math.abs(dif / an * 100), 1)}% ${dif >= 0 ? 'à frente de' : 'atrás de'} ${A.anoAnterior}</strong> (${dif >= 0 ? 'mais' : 'menos'} ${rEur(Math.abs(dif))}).</p>` : '';
        acumCorpo = `<div class="res-legenda"><span><svg width="26" height="10" aria-hidden="true"><line x1="0" y1="5" x2="26" y2="5" stroke="var(--marca)" stroke-width="3"/></svg>${A.anoAtual}</span>
            <span><svg width="26" height="10" aria-hidden="true"><line x1="0" y1="5" x2="26" y2="5" stroke="var(--anterior-linha)" stroke-width="2" stroke-dasharray="5 3"/></svg>${A.anoAnterior}</span></div>
          <div class="res-grafico" style="height:200px;margin-left:50px">
            ${[0, topo / 2, topo].map(g => `<span class="res-y" style="left:-50px;width:44px;top:${BY(g) / 3}%">${g ? rCurto(g) : '0'}</span>`).join('')}
            <svg viewBox="0 0 1000 300" preserveAspectRatio="none" role="img" aria-label="${rEsc(`Faturação acumulada: ${A.anoAtual} chega a ${rCurto(at)} em ${A.nomes[na - 1]}; em ${A.anoAnterior}, no mesmo mês, eram ${rCurto(an)}.`)}">
              <line x1="0" y1="${BY(topo / 2)}" x2="1000" y2="${BY(topo / 2)}" stroke="var(--linha)" vector-effect="non-scaling-stroke"/>
              <line x1="0" y1="300" x2="1000" y2="300" stroke="var(--borda-forte)" vector-effect="non-scaling-stroke"/>
              <path d="${linha(A.anterior)}" fill="none" stroke="var(--anterior-linha)" stroke-width="2" stroke-dasharray="6 4" vector-effect="non-scaling-stroke"/>
              <path d="${linha(A.atual)}" fill="none" stroke="var(--marca)" stroke-width="3" vector-effect="non-scaling-stroke"/>
            </svg>
            <span class="res-rotulo" style="left:${BX(na - 1) / 10}%;top:calc(${BY(at) / 3}% - 6px);transform:${ladoRotulo};color:var(--texto);background:var(--cartao);padding:0 3px">${A.anoAtual}: ${rCurto(at)}</span>
            <span class="res-rotulo" style="left:${BX(na - 1) / 10}%;top:calc(${BY(an) / 3}% + 20px);transform:${ladoRotulo};color:var(--suave);background:var(--cartao);padding:0 3px">${A.anoAnterior}: ${rCurto(an)}</span>
          </div>
          <div class="res-x" style="margin-left:50px">${A.rotulos.map((m, i) => `<span style="left:${BX(i) / 10}%;color:var(--nota)">${m}</span>`).join('')}</div>`;
      }

      // 2 · Evolução: por mês (F, V, N, C e o líquido) ou acumulado face ao ano anterior
      const vistaFat = A && resumoEstado.fatVista === 'acum' ? 'acum' : 'mes';
      const htmlEvolucao = `<section class="res-cartao res-cartao--inteiro" data-bloco="fat-evolucao">
        <div class="res-cab"><h3>Evolução da faturação</h3><span class="res-sub">${vistaFat === 'acum' ? 'Sem objetivo definido: compara com o mesmo período do ano anterior' : 'Valores em euros, sem IVA'}</span></div>
        ${fraseAcum}
        ${A ? `<div class="res-pilulas" role="group" aria-label="Ver a evolução">${separador('fat-vista', 'mes', 'Por mês', vistaFat === 'mes')}${separador('fat-vista', 'acum', 'Acumulado vs. ' + rEsc(A.anoAnterior), vistaFat === 'acum')}</div>` : ''}
        ${vistaFat === 'acum' ? acumCorpo : D.docsPorMes && D.docsPorMes.length ? graficoFaturacaoHtml(D.docsPorMes) : semDados('O valor de cada mês aparece aqui.')}
      </section>`;

      // 3 · Documentos: notas de crédito e créditos (com pesquisa) ou os últimos emitidos
      const vistaDocs = D.creditos && resumoEstado.fatDocs !== 'ultimos' ? 'cred' : 'ultimos';
      const ud = D.ultimosDocs || [];
      const ultimosCorpo = ud.length ? `<div class="res-tab-linha res-tab-cab res-tab--doc" aria-hidden="true"><span>Documento</span><span>Cliente</span><span>Data</span><span>Total</span></div>
          ${ud.map(d => `<div class="res-tab-linha res-tab--doc" data-doc-linha="${rEsc(docId(d.doc))}"><span><button type="button" class="doc-link res-tab-doc" data-doc-abrir="${rEsc(docId(d.doc))}" aria-label="Abrir o documento ${rEsc(d.doc)}">${rEsc(d.doc)}</button></span><span class="res-tab-nome">${rEsc(nomeProprio(d.cliente))}</span><span>${rEsc(d.data)}</span><strong>${rEur(d.total)}</strong></div>`).join('')}`
        : semDados('Os documentos emitidos mais recentes aparecem aqui.');
      const htmlDocs = `<section class="res-cartao res-cartao--inteiro res-tabela" data-bloco="fat-docs">
        <div class="res-cab"><h3>Documentos</h3><span class="res-sub">${vistaDocs === 'cred' ? 'Mais recentes primeiro' : 'Últimos emitidos'}</span></div>
        ${D.creditos ? `<div class="res-pilulas" role="group" aria-label="Que documentos ver">${separador('fat-docs', 'cred', 'Notas de crédito e créditos', vistaDocs === 'cred')}${separador('fat-docs', 'ultimos', 'Últimos emitidos', vistaDocs === 'ultimos')}</div>` : ''}
        ${vistaDocs === 'cred' ? `<div class="res-cred-busca"><label class="so-leitor" for="res-cred-busca">Procurar por documento ou cliente</label><input id="res-cred-busca" maxlength="100" autocomplete="off" placeholder="Procurar documento ou cliente…" value="${rEsc(resumoEstado.credBusca)}"></div>
        <div id="res-creditos-corpo">${creditosHtml()}</div>` : ultimosCorpo}
      </section>`;

      // Cada vista mostra só os seus cartões (a série por mês junta-se em folhaResumo, nas vistas que a têm)
      // Vista Clientes: quatro números sobre os clientes (só com o que a consulta traz)
      let htmlCliKpis = '';
      const ativos = D.kpis.find(k => k.nome === 'Clientes ativos');
      if (D.clientes && D.clientes.length && D.total > 0) {
        const somaTop = D.clientes.reduce((s, c) => s + c.num, 0), nc = D.novosClientes;
        htmlCliKpis = `<div class="flow-kpis-grelha">
          ${ativos ? `<div class="flow-kpi"><span>${svgIc('clientes', 15)}Clientes ativos</span><strong>${rValorKpi(ativos)}</strong><span>${ativos.variacao == null ? '' : chipVar(ativos.variacao) + ' ' + vsAnt}</span></div>` : ''}
          <div class="flow-kpi"><span>${svgIc('clientes', 15)}Novos clientes</span><strong>${nc ? rGrupos(nc.n) : '—'}</strong><span>${!nc ? 'Ainda sem dados' : nc.dif == null ? 'No período escolhido' : `${nc.dif >= 0 ? '▲' : '▼'} ${rGrupos(Math.abs(nc.dif))} ${vsAnt}`}</span></div>
          ${ativos ? `<div class="flow-kpi"><span>${svgIc('euro', 15)}Faturação média por cliente</span><strong>${ativos.valor > 0 ? rEur(D.total / ativos.valor) : '–'}</strong><span>No período escolhido</span></div>` : ''}
          <div class="flow-kpi flow-kpi--destaque"><span>${svgIc('clientes', 15)}Concentração</span><strong>${rPct(somaTop / D.total * 100)}</strong><span>da faturação vem dos ${D.clientes.length} maiores clientes</span></div>
        </div>`;
      }

      // Cada linha da vista: um ou mais cartões lado a lado
      const porSecao = { geral: [htmlCli + htmlAten], alertas: [alertasHtml(D)], clientes: [htmlCliKpis, htmlCliTab + htmlCliLado], faturacao: [htmlTipos, htmlEvolucao, htmlDocs], artigos: [htmlArt + htmlArtLado],
                         zonas: [htmlLoc, htmlZonaDet, htmlLocTab + htmlLocLado], previsoes: [htmlPrevKpis, htmlPrev, htmlPrevMeses] }[secao].filter(Boolean);
      return porSecao.length || secaoComSerie() ? porSecao.map(h => `<div class="res-grelha">${h}</div>`).join('')
        : `<p class="res-nota">Sem dados para a vista ${rEsc(nomeSecao())} com estes filtros.</p>`;
    }
    // Vista Faturação: os números que estavam no resumo do painel lateral, em cartões (o 1.º em destaque)
    // Resumo: os 4 indicadores (o primeiro em destaque)
    function kpisHtml(D) {
      const vs = D.anoAnt ? ' vs ' + rEsc(D.anoAnt) : '';
      const cartao = (ic, nome, valor, extra, cls = '') => `<div class="flow-kpi ${cls}"><span>${svgIc(ic, 15)}${rEsc(nome)}</span><strong>${valor}</strong>${extra ? `<span>${extra}</span>` : ''}</div>`;
      const ICK = ['euro', 'documentos', 'etiqueta', 'clientes'];   // pela ordem dos indicadores (Faturação, Documentos, Valor médio, Clientes)
      return `<div class="flow-kpis-grelha">${D.kpis.map((k, i) => cartao(ICK[i] || 'resumo', k.nome.replace('N.º de documentos', 'Documentos').replace(' por documento', ''),
          rValorKpi(k), k.variacao == null ? '' : chipVar(k.variacao) + vs, i ? '' : 'flow-kpi--destaque')).join('')}</div>`;
    }
    const secaoComSerie = () => secao === 'geral' || secao === 'faturacao';

    const resChipsPeriodo = () => `<div class="res-chips" role="group" aria-label="Período">${RES_PERIODOS.map(p => `<button type="button" class="res-chip" data-periodo="${p[0]}" aria-pressed="${resumoEstado.periodo === p[0]}">${p[1]}</button>`).join('')}</div>`;
    // Enter ou "Ver": abre o resumo desse cliente; vazio = resumo geral
    const resFormCliente = botao => `<form class="res-cliente"><label class="so-leitor" for="res-cliente">ID ou nome do cliente</label>
      <input id="res-cliente" name="cliente" maxlength="100" autocomplete="off" placeholder="Cliente: ID ou início do nome (opcional)" value="${rEsc(resumoEstado.cliente || '')}">
      <button class="res-btn res-btn--primario">${botao}</button></form>`;

    function barraResumoHtml() {
      const D = resumoEstado.dados || {};
      const ano = new Date().getFullYear();
      // "Ano 2026" em vez de "Este ano": o período é o título da página
      const periodo = { AnoAtual: `Ano ${ano}`, AnoAnterior: `Ano ${ano - 1}` }[resumoEstado.periodo] || nomePeriodo(resumoEstado.periodo);
      const aberto = resumoEstado.menu === 'filtros';
      const hoje = new Date().toLocaleDateString('pt-PT', { weekday: 'long', day: 'numeric', month: 'long' });
      const nFiltros = (resumoEstado.zona ? 1 : 0) + (resumoEstado.cliente ? 1 : 0);
      // Os três períodos mais usados à vista (como no protótipo); os outros estão em Filtros
      const segmentos = [['MesAtual', 'Mês'], ['TrimestreAtual', 'Trimestre'], ['AnoAtual', `Ano ${ano}`]]
        .map(p => `<button type="button" data-periodo="${p[0]}" aria-pressed="${resumoEstado.periodo === p[0]}">${p[1]}</button>`).join('');
      // Barra do topo (onde está e até quando há dados), depois o título da vista com o período e os filtros
      return `<div class="res-cabecalho">
        ${D.ate ? `<span class="res-dados-ate">${svgIc('relogio', 14)}Dados até ${dataCurta(D.ate)}${D.mesIncompleto ? ' · ' + rEsc(D.mesIncompleto) + ' incompleto' : ''}</span>` : ''}
        ${D.alertas ? `<button type="button" class="res-sino" data-ver-secao="alertas" title="Alertas" aria-label="Alertas: ${nAlertasPorLer(D)} por ler">${svgIc('alertas', 17)}${nAlertasPorLer(D) ? `<span aria-hidden="true">${nAlertasPorLer(D)}</span>` : ''}</button>` : ''}
      </div>
      <div class="res-caminho"><button type="button" class="res-casa" data-inicio>Início</button><span aria-hidden="true">/</span>${migalhas().outerHTML}</div>
      <div class="flow-topo">
        <div class="flow-topo-ola">
          <h2 class="res-titulo">${rEsc(nomeSecao())}${D.cliente ? ' · ' + rEsc(D.cliente) : ''}</h2>
          <p class="flow-hoje">${ola()}${nomeUtilizador ? ', ' + rEsc(nomeUtilizador.split(/\s+/)[0]) : ''} · ${hoje}${segmentos.includes(`"${resumoEstado.periodo}"`) ? '' : ' · ' + rEsc(periodo)} · valores sem IVA</p>
        </div>
        <div class="res-topo-dir">
          <div id="acoes-vaga"></div>
          <div class="res-topo-ctrl">
          <div class="res-periodos" role="group" aria-label="Período">${segmentos}</div>
          ${resumoEstado.cliente ? `<button type="button" class="res-ficha-btn" data-ficha="${rEsc(resumoEstado.cliente)}">${svgIc('ficha', 15)}Ficha do cliente</button>` : ''}
          <div class="res-menu-wrap">
            <button type="button" class="res-filtros-btn" data-menu="filtros" aria-expanded="${aberto}" aria-controls="res-menu-filtros" aria-pressed="${nFiltros > 0}">${svgIc('filtro', 16)}Filtros${nFiltros ? `<span class="res-filtros-n">${nFiltros}</span>` : ''}</button>
            <div class="res-menu" id="res-menu-filtros" role="dialog" aria-label="Filtros" ${aberto ? '' : 'hidden'}>
              <div><h3>Período</h3>${resChipsPeriodo()}</div>
              ${!resumoEstado.cliente && resumoEstado.zonas.length ? `<div><h3>Distrito</h3><div class="res-chips" role="group" aria-label="Distrito">${[{ id: '', nome: 'Todos' }].concat(resumoEstado.zonas).map(l => `<button type="button" class="res-chip" data-zona="${rEsc(l.id)}" aria-pressed="${(resumoEstado.zona || '') === l.id}">${rEsc(l.nome)}</button>`).join('')}</div></div>` : ''}
              <div><h3>Cliente</h3>${resFormCliente('Ver')}</div>
              <div class="res-menu-rodape"><button type="button" class="chat-acao" data-limpar>Limpar filtros</button><button type="button" class="res-btn" data-fechar>Fechar</button></div>
            </div>
          </div>
          </div>
        </div>
      </div>
      <div class="res-barra" ${nFiltros ? '' : 'hidden'}>
        <div class="res-info">
          <span>Filtros ativos:</span>
          ${resumoEstado.zona ? `<button type="button" class="res-filtro-ativo" data-zona="" aria-label="Remover filtro de distrito">Distrito: <strong>${rEsc(nomeDistrito(resumoEstado.zona))}</strong> ×</button>` : ''}
          ${resumoEstado.cliente ? `<button type="button" class="res-filtro-ativo" data-sem-cliente aria-label="Remover filtro de cliente">Cliente: <strong>${rEsc(D.cliente || resumoEstado.cliente)}</strong> ×</button>` : ''}
        </div>
      </div>`;
    }

    // Enquanto carrega: a forma do Resumo (vendas por mês e os 6 cartões), para se ver onde vão aparecer os dados
    function esqueletoResumoHtml() {
      const cartao = (alto, cls = '') => `<section class="res-cartao ${cls}"><span style="width:45%;height:16px"></span><span style="width:30%;height:10px"></span><span style="height:${alto}px"></span></section>`;
      return `<p class="res-carregar" role="status">A carregar os dados das vendas…</p>
        <div class="res-esqueleto res-conteudo" aria-hidden="true">
          <div class="flow-serie"><span style="width:300px;max-width:80%;height:26px"></span><span style="height:260px"></span></div>
          <div class="res-grelha">${cartao(220, 'res-cartao--largo')}${cartao(220)}</div>
          <div class="res-grelha">${cartao(260)}${cartao(260)}</div>
          <div class="res-grelha">${cartao(240)}${cartao(240)}</div>
        </div>`;
    }

    // Resumo — "Vendas por mês" como no protótipo: área do ano atual, ano anterior tracejado, mês incompleto pontilhado.
    // Cada coluna é um botão (data-mes) que escolhe o mês do cartão ao lado; começa no último mês completo.
    function graficoResumoHtml(s) {
      const n = s.meses.length, W = n * 100;
      const max = Math.max(1, ...s.meses.flatMap(x => [x.atual || 0, x.anterior || 0]));
      const X = i => i * 100 + 50, Y = v => 200 - v / max * 180;   // 180: folga por cima do ponto mais alto
      const pts = campo => s.meses.map((x, i) => ({ i, v: x[campo] })).filter(p => p.v > 0);
      const traco = ps => ps.map((p, k) => (k ? 'L' : 'M') + X(p.i) + ' ' + Y(p.v).toFixed(1)).join(' ');
      const comp = pts('atual').filter(p => !s.meses[p.i].parcial), parcial = pts('atual').find(p => s.meses[p.i].parcial), ult = comp.at(-1);
      // Dados novos (outro período, zona ou cliente): volta ao último mês completo
      if (resumoEstado.mesSerie !== s) Object.assign(resumoEstado, { mesSerie: s, mes: ult ? ult.i : pts('atual').at(-1)?.i });
      const sel = resumoEstado.mes, x = s.meses[sel];
      if (!x) return '';   // nenhum mês com vendas
      const mesNome = m => MESES[m.m - 1], curto = m => mesNome(m).slice(0, 3).toLowerCase();
      const desc = m => `${mesNome(m)} ${m.a}: ${m.atual == null ? 'sem vendas' : rEur(m.atual)}${m.anterior != null && s.anoAnt ? `, ${s.anoAnt}: ${rEur(m.anterior)}` : ''}${m.parcial ? ', mês incompleto' : m.delta != null ? ', ' + rVar(m.delta) : ''}`;
      const svg = `<svg class="res-vm-svg" viewBox="0 0 ${W} 200" preserveAspectRatio="none" aria-hidden="true">
          <defs><linearGradient id="res-vm-grad" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1d5bff" stop-opacity=".18"/><stop offset="1" stop-color="#1d5bff" stop-opacity="0"/></linearGradient></defs>
          ${[0, 100].map(y => `<line x1="0" y1="${y}" x2="${W}" y2="${y}" stroke="#eef0f3" vector-effect="non-scaling-stroke"/>`).join('')}
          <line x1="0" y1="200" x2="${W}" y2="200" stroke="#d7dbe2" vector-effect="non-scaling-stroke"/>
          ${comp.length ? `<path d="${traco(comp)} L${X(ult.i)} 200 L${X(comp[0].i)} 200 Z" fill="url(#res-vm-grad)"/>` : ''}
          ${s.anoAnt ? `<path d="${traco(pts('anterior'))}" fill="none" stroke="#9aa3b2" stroke-width="2" stroke-dasharray="5 4" vector-effect="non-scaling-stroke"/>` : ''}
          ${comp.length ? `<path d="${traco(comp)}" fill="none" stroke="#1d5bff" stroke-width="2.5" stroke-linejoin="round" vector-effect="non-scaling-stroke"/>` : ''}
          ${parcial && ult ? `<path d="${traco([ult, parcial])}" fill="none" stroke="#1d5bff" stroke-width="2" stroke-dasharray="2 4" vector-effect="non-scaling-stroke"/>` : ''}
        </svg>`;
      const pontos = pts('atual').map(p => `<span class="res-vm-ponto${p.i === sel ? ' res-vm-ponto--sel' : ''}${s.meses[p.i].parcial ? ' res-vm-ponto--parcial' : ''}" style="left:${X(p.i) / W * 100}%;top:${Y(p.v)}px"></span>`).join('');
      const dica = x.atual != null ? `<span class="res-vm-dica" style="left:${X(sel) / W * 100}%;top:${Y(x.atual)}px">${curto(x)} · ${rCurto(x.atual)}</span>` : '';
      const colunas = s.meses.map((m, i) => `<button type="button" data-mes="${i}" aria-pressed="${i === sel}" aria-label="${rEsc(desc(m))}" ${m.atual == null ? 'disabled' : ''}></button>`).join('');
      const rotulos = s.meses.map((m, i) => `<span${i === sel ? ' class="res-vm-rotulo--sel"' : ''}>${curto(m)}${m.parcial ? '*' : ''}</span>`).join('');
      const grelha = `style="grid-template-columns:repeat(${n},minmax(0,1fr))"`;
      // Cartão do mês escolhido
      const nota = x.parcial ? 'Mês incompleto' : x.delta == null && s.anoAnt ? `Sem vendas em ${s.anoAnt} para comparar` : 'Mês completo';
      const variacao = !x.parcial && x.delta != null ? `<span class="res-mes-var ${x.delta >= 0 ? 'res-mes-var--sobe' : 'res-mes-var--desce'}">${rVar(x.delta)} vs ${rEsc(s.anoAnt)}</span>` : '';
      const pergunta = `Vendas de ${mesNome(x).toLowerCase()} de ${x.a}` + (s.anoAnt ? ` comparadas com ${mesNome(x).toLowerCase()} de ${s.anoAnt}` : '');
      return `<div class="res-grelha res-vm">
        <section class="res-cartao res-vm-cartao">
          <div class="res-vm-cab">
            <div><h3>Vendas por mês</h3><p class="res-sub">Clique num mês para ver o detalhe</p></div>
            <div class="res-vm-legenda"><span><i class="res-vm-leg-atual"></i>${s.anoAtual}</span>${s.anoAnt ? `<span><i class="res-vm-leg-ant"></i>${rEsc(s.anoAnt)}</span>` : ''}</div>
          </div>
          <div class="res-vm-grafico">
            <div class="res-vm-colunas" role="group" aria-label="Vendas por mês: escolha um mês" ${grelha}>${colunas}</div>
            ${svg}${pontos}${dica}
            <div class="res-vm-rotulos" aria-hidden="true" ${grelha}>${rotulos}</div>
          </div>
        </section>
        <section class="res-mes" aria-live="polite" aria-label="Mês selecionado">
          <h3>Mês selecionado</h3>
          <strong class="res-mes-nome">${mesNome(x)} ${x.a}</strong>
          <span class="res-mes-nota">${nota}</span>
          <span class="res-mes-valor">${x.atual == null ? '—' : rEur(x.atual)}</span>
          ${s.anoAnt ? `<span class="res-mes-nota">${rEsc(s.anoAnt)}: ${x.anterior == null ? '—' : rEur(x.anterior)}</span>` : ''}
          ${variacao}
          <button type="button" class="res-btn-flow" data-perguntar="${rEsc(pergunta)}">${svgIc('brilho', 15)}Perguntar ao Flow</button>
        </section>
      </div>`;
    }

    function folhaResumo() {
      const raiz = el('article', 'chat-folha chat-folha--painel');
      raiz.style.animation = 'none';   // redesenha a cada clique: sem a animação de entrada, não pisca
      ligarResumo(raiz);
      if (resumoEstado.aCarregar) {
        raiz.innerHTML = barraResumoHtml() + esqueletoResumoHtml();
        return raiz;
      }
      const D = resumoEstado.dados;
      if (resumoEstado.erro || !D || D.erro) {
        // Cliente não encontrado ou ambíguo: os candidatos em botões; sem vendas ou sem ligação: a mensagem
        const opcoes = D && D.opcoes || [];
        raiz.innerHTML = barraResumoHtml() + `<div class="chat-painel-vazio res-pedir" role="alert">
          <p class="chat-texto">${rEsc(resumoEstado.erro || (D && D.erro) || 'Sem dados.')}</p>
          ${opcoes.length ? `<div class="res-chips" style="justify-content:center">${opcoes.map(([id, nome]) => `<button type="button" class="res-chip" data-cliente="${rEsc(id)}">${rEsc(nome)} (${rEsc(id)})</button>`).join('')}</div>` : ''}
          ${resumoEstado.erro ? '<button type="button" class="res-btn res-btn--primario" data-repetir style="align-self:center">Tentar de novo</button>' : ''}
        </div>`;
        return raiz;
      }
      raiz.innerHTML = barraResumoHtml() + '<div class="res-conteudo">' + resumoHtml(D) + '</div>';
      // Resumo: o gráfico e o cartão do mês desenhados como no protótipo; sem rodapé à vista (o Exportar do topo usa-o)
      raiz.classList.toggle('res-geral', secao === 'geral');
      if (secao === 'geral' && D.serie && D.serie.meses.length > 1) raiz.querySelector('.res-conteudo').insertAdjacentHTML('beforebegin', graficoResumoHtml(D.serie));
      if (secao === 'geral') raiz.querySelector('.res-vm, .res-conteudo').insertAdjacentHTML('beforebegin', kpisHtml(D));
      // Copiar e Exportar CSV: os mesmos botões das outras respostas (faturação por mês)
      rodapeDados(raiz.querySelector('.res-conteudo'), linhasResumo(D), 'resumo das vendas' + (D.cliente ? ' ' + D.cliente : ''), '', imagemSecao(D));
      ligarPrevisao(raiz, D.previsao);
      return raiz;
    }

    // "Guardar imagem" da vista aberta: antes era sempre o gráfico por mês, mesmo nas vistas que não o mostram.
    // Previsões não tem desenho em PNG: sem imagem (null) em vez de uma imagem errada.
    function imagemSecao(D) {
      const sub = D.periodoTexto, de = D.cliente ? ' · ' + D.cliente : '';
      const ranking = (lista, titulo) => lista && lista.length ? () => pngRanking(lista.map((i, k) => ({ pos: k + 1, nome: i.nome, codigo: i.codigo, num: i.num,
        valor: rEur(i.num), pct: D.total > 0 ? rPct(i.num / D.total * 100) : '' })), titulo + de, sub) : null;
      if (secaoComSerie()) return D.serie && D.serie.meses.length > 1 ? () => pngSerie(D.serie, 'Vendas por mês' + de, sub) : null;
      if (secao === 'clientes') return ranking(D.clientes, 'Top clientes');
      if (secao === 'artigos') return ranking(D.artigos, 'Artigos mais vendidos');
      if (secao === 'zonas') return ranking((D.locais || []).map(l => ({ nome: l.nome, num: l.fat })), 'Vendas por distrito');
      return null;
    }

    // Tudo o que o Resumo mostra numa só folha (Copiar e Exportar CSV): uma linha por número, com a secção de onde vem.
    // Os valores vão em número (vírgula decimal) para o Excel somar; o € fica na coluna Unidade.
    function linhasResumo(D) {
      const n = v => v == null || !isFinite(v) ? '' : String(Math.round(v * 100) / 100).replace('.', ',');
      const variacao = (a, b) => b > 0 && a != null ? n((a / b - 1) * 100) : '';
      const linhas = [['Secção', 'Nome', 'Código', 'Valor', 'Unidade', 'Ano anterior', 'Variação (%)', 'Notas']];
      const juntar = (seccao, nome, codigo, valor, unidade, anterior, delta, notas) =>
        linhas.push([seccao, nome, codigo || '', n(valor), unidade, n(anterior), typeof delta === 'string' ? delta : n(delta), notas || '']);
      D.kpis.forEach(k => juntar('Indicadores', k.nome, '', k.valor, k.tipo.startsWith('eur') ? '€' : 'n.º', null, k.variacao, k.contexto));
      (D.serie ? D.serie.meses : []).filter(x => x.atual != null || x.anterior != null).forEach(x =>
        juntar('Vendas por mês', `${MESES[x.m - 1]} ${x.a || ''}`.trim(), '', x.atual, '€', x.anterior, x.delta ?? variacao(x.atual, x.anterior), [x.parcial ? 'Mês incompleto' : '', x.media ? 'Valor médio ' + x.media : ''].filter(Boolean).join(' · ')));
      D.artigos.forEach(i => juntar('Artigos mais vendidos', i.nome, i.codigo, i.num, '€', null, i.delta));
      (D.locais || []).forEach(l => juntar('Vendas por distrito', l.nome, '', l.fat, '€', null, l.variacao, l.novo ? 'Sem vendas em ' + l.novo : ''));
      (D.localidades || []).forEach(l => juntar('Vendas por localidade', l.semDados ? 'Sem localidade na ficha' : nomeProprio(l.nome), '', l.num, '€', null, l.delta));
      (D.clientes || []).forEach(c => juntar('Top clientes', c.nome, c.codigo, c.num, '€', null, c.delta));
      (D.tiposDoc || []).forEach(t => juntar('Documentos por tipo', t.nome, '', t.valor, '€', null, '', `${t.docs} documentos`));
      (D.docsPorMes || []).forEach(x => juntar('Documentos por mês', `${MESES[x.m - 1]} ${x.a}`, '', x.n, 'n.º', null, '', x.parcial ? 'Mês incompleto' : ''));
      (D.creditos || []).forEach(d => juntar('Notas de crédito e créditos', d.doc, '', d.total, '€', null, '', `${d.cliente}, ${d.data}`));
      (D.ultimosDocs || []).forEach(d => juntar('Últimos documentos', d.doc, '', d.total, '€', null, '', `${d.cliente}, ${d.data}`));
      const P = D.previsao;
      if (P) P.previsto.slice(1).forEach((v, j) => juntar('Previsão', P.nomes[P.real.length + j], '', v, '€', null, '',
        `Intervalo provável: ${n(P.inferior[j + 1])} a ${n(P.superior[j + 1])} €`));
      const A = D.acumulado;
      if (A) A.atual.forEach((v, i) => juntar('Faturação acumulada', `Até ${A.nomes[i]}`, '', v, '€', A.anterior[i], variacao(v, A.anterior[i])));
      alertasAtivos(D).forEach(a => juntar('Alertas', a.titulo, '', null, '', null, '', `${a.sev}: ${a.texto}`));
      return linhas;
    }

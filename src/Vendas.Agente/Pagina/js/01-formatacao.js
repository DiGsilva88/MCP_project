    /* =========================================================
       Formatação pt-PT: a única forma de escrever números e datas na página.
       Milhares com espaço não separável, vírgula decimal, € depois do número.
       ========================================================= */
    const NBSP = ' ', MENOS = '−';
    const MESES = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'];
    const fmt = {
      // "2044643" -> "2 044 643"; o sinal é o menos tipográfico
      inteiro(n) {
        if (n == null || !isFinite(n)) return '—';
        const r = Math.round(n), s = String(Math.abs(r)).replace(/\B(?=(\d{3})+(?!\d))/g, NBSP);
        return (r < 0 ? MENOS : '') + s;
      },
      decimal(n, casas = 1) {
        if (n == null || !isFinite(n)) return '—';
        const [i, d] = Math.abs(n).toFixed(casas).split('.');
        const zero = +Math.abs(n).toFixed(casas) === 0;
        return (n < 0 && !zero ? MENOS : '') + fmt.inteiro(+i) + (d ? ',' + d : '');
      },
      eur: n => n == null || !isFinite(n) ? '—' : fmt.inteiro(n) + NBSP + '€',
      eur2: n => n == null || !isFinite(n) ? '—' : fmt.decimal(n, 2) + NBSP + '€',
      // Eixos e cartões pequenos: "198 mil €", "2,04 M€"
      curto: n => n == null || !isFinite(n) ? '—' : Math.abs(n) >= 1e6 ? fmt.decimal(n / 1e6, 2) + NBSP + 'M€'
        : Math.abs(n) >= 1e4 ? fmt.inteiro(n / 1000) + NBSP + 'mil' + NBSP + '€' : fmt.eur(n),
      pct: (n, casas = 1) => n == null || !isFinite(n) ? '—' : fmt.decimal(n, casas) + '%',
      // "▲ 6,3%" (subida) / "▼ 4,1%" (descida); sobe diz a cor (azul ou preto)
      variacao(n) {
        if (n == null || !isFinite(n)) return { texto: '–', sobe: null };
        return { texto: (n >= 0 ? '▲' : '▼') + NBSP + fmt.decimal(Math.abs(n), 1) + '%', sobe: n >= 0 };
      },
      // "04-09-2026" (servidor) ou Date -> { d, m, a }
      lerData(v) {
        if (v instanceof Date) return { d: v.getDate(), m: v.getMonth() + 1, a: v.getFullYear() };
        const x = /^(\d{2})-(\d{2})-(\d{4})/.exec(v || '') || /^(\d{4})-(\d{2})-(\d{2})/.exec(v || '');
        if (!x) return null;
        return x[1].length === 4 ? { d: +x[3], m: +x[2], a: +x[1] } : { d: +x[1], m: +x[2], a: +x[3] };
      },
      // "4 de setembro" (comAno: "4 de setembro de 2026")
      dataLonga(v, comAno = false) {
        const x = fmt.lerData(v);
        return x ? `${x.d} de ${MESES[x.m - 1].toLowerCase()}${comAno ? ' de ' + x.a : ''}` : '';
      },
      // "4 set"
      dataCurta(v) {
        const x = fmt.lerData(v);
        return x ? `${x.d}${NBSP}${MESES[x.m - 1].slice(0, 3).toLowerCase()}` : '';
      },
      mesCurto: m => MESES[m - 1].slice(0, 3).toLowerCase(),
      // Texto vindo do servidor ("2 044 643,20 €", "58,73") -> número
      numero(s) {
        if (typeof s === 'number') return s;
        const t = String(s ?? '').replace(/[\s  .]/g, '').replace(MENOS, '-').replace(/[^\d,-]/g, '').replace(',', '.');
        return t && t !== '-' ? parseFloat(t) : NaN;
      }
    };
    if (typeof module === 'object') module.exports = { fmt, MESES, NBSP };

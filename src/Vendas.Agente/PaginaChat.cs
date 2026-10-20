namespace Vendas.Agente;

// A página do modo web (opção 3). Tudo inline: o agente não serve outros ficheiros.
// As respostas entram com textContent, nunca innerHTML: o texto vem da base de dados.
public static class PaginaChat
{
    public const string Html = """
        <!doctype html>
        <html lang="pt-PT">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Assistente de Vendas</title>
        <style>
          :root { --fundo:#f5f6f8; --painel:#fff; --texto:#1d2330; --suave:#5c6577; --eu:#2457c5; --eu-texto:#fff; --borda:#dde1e8; }
          @media (prefers-color-scheme: dark) {
            :root { --fundo:#14171c; --painel:#1d2129; --texto:#e7e9ee; --suave:#9aa3b2; --eu:#3b6fe0; --eu-texto:#fff; --borda:#2c323d; }
          }
          * { box-sizing:border-box; }
          body { margin:0; background:var(--fundo); color:var(--texto); font:16px/1.5 system-ui, "Segoe UI", sans-serif;
                 display:flex; flex-direction:column; height:100dvh; }
          header { padding:12px 16px; border-bottom:1px solid var(--borda); background:var(--painel); }
          header h1 { margin:0; font-size:18px; }
          header p { margin:2px 0 0; color:var(--suave); font-size:14px; }
          main { flex:1; overflow-y:auto; padding:16px; display:flex; flex-direction:column; gap:12px; max-width:820px; width:100%; margin:0 auto; }
          .msg { padding:10px 14px; border-radius:12px; max-width:90%; white-space:pre-wrap; overflow-wrap:anywhere; }
          .eu { align-self:flex-end; background:var(--eu); color:var(--eu-texto); }
          .ia { align-self:flex-start; background:var(--painel); border:1px solid var(--borda); }
          .ia.espera { color:var(--suave); font-style:italic; }
          .sugestoes { display:flex; flex-wrap:wrap; gap:8px; }
          .sugestoes button { background:var(--painel); color:var(--texto); border:1px solid var(--borda); border-radius:16px; padding:6px 12px; font:inherit; font-size:14px; cursor:pointer; }
          form { display:flex; gap:8px; padding:12px 16px; border-top:1px solid var(--borda); background:var(--painel); max-width:820px; width:100%; margin:0 auto; }
          input { flex:1; min-width:0; padding:10px 12px; border:1px solid var(--borda); border-radius:8px; background:var(--fundo); color:var(--texto); font:inherit; }
          form button { padding:10px 16px; border:0; border-radius:8px; background:var(--eu); color:var(--eu-texto); font:inherit; cursor:pointer; }
          button:disabled, input:disabled { opacity:.6; cursor:wait; }
          :focus-visible { outline:2px solid var(--eu); outline-offset:2px; }
        </style>
        </head>
        <body>
        <header>
          <h1>Assistente de Vendas</h1>
          <p>Pergunte sobre clientes, faturação, artigos, famílias e marcas.</p>
        </header>
        <main id="conversa" aria-live="polite">
          <div class="msg ia">Olá! Em que posso ajudar? Experimente uma destas perguntas:</div>
          <div class="sugestoes">
            <button type="button">Que família de artigos vendeu mais no mês passado?</button>
            <button type="button">Quais são os 5 melhores clientes este ano?</button>
            <button type="button">Quantos clientes há por zona?</button>
            <button type="button">Como evoluíram as vendas mês a mês este ano?</button>
          </div>
        </main>
        <form id="form">
          <label for="pergunta" hidden>Pergunta</label>
          <input id="pergunta" maxlength="500" autocomplete="off" placeholder="Escreva a sua pergunta…" required>
          <button>Enviar</button>
        </form>
        <script>
          const conversa = document.getElementById('conversa');
          const form = document.getElementById('form');
          const campo = document.getElementById('pergunta');
          const botao = form.querySelector('button');

          function mensagem(texto, classe) {
            const div = document.createElement('div');
            div.className = 'msg ' + classe;
            div.textContent = texto;
            conversa.appendChild(div);
            conversa.scrollTop = conversa.scrollHeight;
            return div;
          }

          async function perguntar(texto) {
            mensagem(texto, 'eu');
            const resposta = mensagem('A consultar os dados…', 'ia espera');
            campo.disabled = botao.disabled = true;
            try {
              const r = await fetch('/perguntar', { method:'POST', body:texto });
              const leitor = r.body.getReader();
              const descodificador = new TextDecoder();
              let primeiro = true;
              for (;;) {
                const { done, value } = await leitor.read();
                if (done) break;
                if (primeiro) { resposta.textContent = ''; resposta.classList.remove('espera'); primeiro = false; }
                resposta.textContent += descodificador.decode(value, { stream:true });
                conversa.scrollTop = conversa.scrollHeight;
              }
              if (primeiro) { resposta.textContent = 'Sem resposta. Tente novamente.'; resposta.classList.remove('espera'); }
            } catch {
              resposta.textContent = 'O assistente não está disponível. Confirme que o agente está a correr.';
              resposta.classList.remove('espera');
            } finally {
              campo.disabled = botao.disabled = false;
              campo.focus();
            }
          }

          form.addEventListener('submit', e => {
            e.preventDefault();
            const texto = campo.value.trim();
            if (!texto || campo.disabled) return;
            campo.value = '';
            perguntar(texto);
          });
          document.querySelectorAll('.sugestoes button').forEach(b =>
            b.addEventListener('click', () => { if (!campo.disabled) perguntar(b.textContent); }));
          campo.focus();
        </script>
        </body>
        </html>
        """;
}

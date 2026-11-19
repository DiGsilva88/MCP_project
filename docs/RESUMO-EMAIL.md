# Resumo por email (semanal/mensal): o que é preciso para funcionar

> Estado: o código está feito e testado (`Agendamentos`, `--enviar-resumo`). **Nunca foi ensaiado com a caixa de correio real nem como tarefa agendada num servidor.** Este ficheiro regista o que falta configurar. A descrição funcional está no [README](../README.md#resumo-por-email-à-segunda).

## Como funciona

1. Na página, **Enviar por email à segunda** guarda `{ email, frequência }` em `%LOCALAPPDATA%\Vendas\agendamentos.json` (só endereços **@inforap.pt**; um endereço tem um só agendamento).
2. Uma **tarefa do Agendador do Windows** corre `Vendas.Agente.exe --enviar-resumo` e sai. Não usa o Ollama: os números vêm das ferramentas do servidor MCP.
3. Semanal: todas as segundas, com a semana anterior. Mensal: na 1.ª segunda do mês, com o mês anterior.

## O que é preciso (por ordem)

| # | O quê | Quem | Como confirmar |
| --- | --- | --- | --- |
| 1 | Caixa de correio de serviço (ex.: `caixa-de-servico@inforap.pt`) com **SMTP AUTH ativo** no Office 365 | IT | Pedir ao IT; sem MFA obrigatório para esta conta |
| 2 | Confirmar que o Office 365 da empresa **ainda aceita autenticação básica por SMTP**. A Microsoft tem vindo a retirá-la; se não aceitar, o código tem de mudar para OAuth (trabalho novo, não existe) | IT | Teste com um envio real (ponto 7) |
| 3 | Conta Windows que corre a tarefa (a mesma que corre o agente e que criou o `sql.pwd`) | Equipa | O DPAPI só deixa essa conta ler os `.pwd` |
| 4 | `VENDAS_SMTP_UTILIZADOR` (em `Enviar-Resumo.cmd`) e `smtp.pwd` criado **por essa conta** | Equipa | `Test-Path "$env:LOCALAPPDATA\Vendas\smtp.pwd"` (na conta do serviço) |
| 5 | **`VENDAS_SQL` no ambiente da tarefa.** O `--enviar-resumo` arranca o servidor MCP e precisa da base de dados (e do `sql.pwd`). No cenário IIS o `Enviar-Resumo.cmd` lê-o do `Iniciar-Flow.cmd`: não se repete | Equipa | `resumo.log` diz "Preencha VENDAS_SQL" ou "Não consegui arrancar/ligar ao servidor MCP" |
| 6 | A tarefa agendada, às segundas (ex.: 08:00), criada com `/RU <conta> /RP` para correr **sem sessão iniciada** | Equipa | `schtasks /Query /TN "Vendas - resumo por email"` |
| 7 | Ensaio: primeiro para uma pasta (`VENDAS_SMTP_PASTA`), depois um envio real para si | Equipa | Ver "Testar" |

### Configuração no cenário IIS (conta do serviço, ex.: `INFORAP\svc-flow`)

O `Instalar-IIS.ps1` já copia `iis\Enviar-Resumo.cmd` para `C:\Flow\`. Depois:

1. Em `C:\Flow\Enviar-Resumo.cmd`, preencher `VENDAS_SMTP_UTILIZADOR` (e `VENDAS_SMTP_HOST`/`PORTA` se não for o Office 365). O `VENDAS_SQL` vem do `Iniciar-Flow.cmd`, que também tem de estar preenchido.
2. Criar o `smtp.pwd` **como a conta do serviço** (ex.: `runas /user:INFORAP\svc-flow powershell`):

   ```powershell
   New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Vendas" | Out-Null
   Read-Host "Password SMTP" -AsSecureString | ConvertFrom-SecureString | Set-Content "$env:LOCALAPPDATA\Vendas\smtp.pwd"
   ```

3. Criar a tarefa (pede a palavra-passe da conta; corre sem sessão iniciada):

   ```powershell
   schtasks /Create /TN "Flow - resumo por email" /SC WEEKLY /D MON /ST 08:00 /RU INFORAP\svc-flow /RP /TR "C:\Flow\Enviar-Resumo.cmd"
   ```

4. O resultado de cada execução fica em `C:\Flow\resumo.log`. Reenviar uma segunda falhada: `C:\Flow\Enviar-Resumo.cmd --dia 02-11-2026`.

### Configuração num posto (sem IIS)

```powershell
setx VENDAS_SMTP_UTILIZADOR "caixa-de-servico@inforap.pt"
setx VENDAS_SQL "Server=tcp:<servidor>,<porta>;Database=IAVSGIX;User Id=mcpserver;Encrypt=True;TrustServerCertificate=True"
Read-Host "Password SMTP" -AsSecureString | ConvertFrom-SecureString | Set-Content "$env:LOCALAPPDATA\Vendas\smtp.pwd"
schtasks /Create /TN "Vendas - resumo por email" /SC WEEKLY /D MON /ST 08:00 /RU <conta> /RP /TR "\"C:\Flow\Vendas.Agente.exe\" --enviar-resumo"
```

- `setx` só vale para processos novos: criar a tarefa **depois** dele.
- Outro servidor SMTP: `VENDAS_SMTP_HOST` e `VENDAS_SMTP_PORTA` (por omissão `smtp.office365.com:587`, STARTTLS).
- Com o IIS (`docs/IIS.md`) a tarefa tem de correr como a **mesma conta** que o agente (ex.: `svc-flow`): é nesse perfil que a página grava o `agendamentos.json`. Com outra conta a tarefa lê uma lista vazia e **não envia nada, sem dar erro**.

## Testar

1. **Sem enviar:** `$env:VENDAS_SMTP_PASTA = "C:\temp\emails"` e correr `Vendas.Agente.exe --enviar-resumo --dia <segunda dd-MM-aaaa>` (com o IIS, correr o `Enviar-Resumo.cmd --dia …` com essa variável definida na consola). Grava `.eml` na pasta (não precisa do `smtp.pwd`). Abrir um `.eml` e ver os números.
2. **Envio real:** agendar o seu próprio email na página, correr o comando acima com `--dia` numa segunda, sem `VENDAS_SMTP_PASTA`. Deve receber o email.
3. **Tarefa:** `schtasks /Run /TN "Flow - resumo por email"` (num posto: `"Vendas - resumo por email"`). Fora de uma segunda não há nada devido e sai com 0 ("0 resumo(s) para enviar hoje").

## Limitações conhecidas

- Se o PC ou servidor estiver desligado à segunda, esse email **perde-se**; reenvia-se à mão com `--dia`.
- Uma falha só marca a tarefa como falhada no Agendador (código 1): ninguém é avisado. Convém uma verificação periódica ou um alerta.
- O motivo das falhas vai para a consola (nunca o endereço de email); no cenário IIS o `Enviar-Resumo.cmd` grava-o em `resumo.log`.
- Os dados do resumo vão até à última venda registada na base (aparece no email se estiverem desatualizados).

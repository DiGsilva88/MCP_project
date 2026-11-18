# Flow (Vendas.Agente) em IIS — análise, arquitetura e passo a passo

> Estado: **por validar num IIS real.** O guia e os scripts foram escritos a partir da análise do código; não foram executados em IIS/ARR/Kerberos. Nada neste pacote altera o repositório: a alteração de código necessária vai à parte, como *patch* de proposta (`codigo/proxy-iis.patch`).

## 1. O que o projeto é (análise)

| Peça | O que é | Consequência para o IIS |
| --- | --- | --- |
| `Vendas.Agente` | **Aplicação de consola** .NET 10 (`Vendas.Agente.exe --web`). O "website" é um `HttpListener` próprio (`Program.cs`, `ModoWebAsync`) que serve a página (embebida no `.exe`), `/perguntar` (chat com streaming), `/resumo` e `/api/*`. | **Não é ASP.NET**: o IIS não a consegue alojar (nem em processo, nem com o Hosting Bundle). O IIS só pode ficar à frente, como proxy inverso. |
| `Vendas.Servidor` | Servidor MCP por **stdio**, lançado como processo filho pelo agente (`dotnet Vendas.Servidor.dll`). Liga ao SQL Server (`VENDAS_SQL`, `sql.pwd` cifrado com DPAPI). | Tem de correr na mesma conta e máquina que o agente. Não se publica no IIS. |
| Autenticação | O agente exige **Windows integrado na própria porta** e resolve o utilizador por `perfis.json`. | Atrás de um proxy essa identidade perde-se. É o problema central (secção 3). |
| Origem | `ApiPainel.Recusa` recusa POST cuja `Origin` não seja o endereço do agente e `GET /resumo` e `/api/*` sem `Sec-Fetch-Site: same-origin`. `VENDAS_ENDERECO` tem de ser `https://`. | Atrás do IIS o browser envia o endereço público, não `localhost:5080`. |
| Estado | Ficheiros em `%LOCALAPPDATA%\Vendas` (`perfis.json`, `sql.pwd`, `agendamentos.json`, `auditoria.log`, preferências por utilizador). Resumos em cache de memória; **uma pergunta ao modelo de cada vez**. | Uma só instância central, com conta de serviço fixa. |

## 2. Arquitetura

```text
Browser (INFORAP\utilizador, Negotiate/Kerberos)
   │  HTTPS 443  https://flow.inforap.local/
   ▼
IIS — site "Flow" (pool sem código gerido, conta de domínio com SPN)
   │  Windows Authentication; regra URL Rewrite + ARR
   │  escreve  X-Remote-User = {LOGON_USER}   e   X-Proxy-Secret = <segredo>
   ▼  HTTP  http://localhost:5080  (só loopback)
Vendas.Agente.exe --web   (tarefa agendada, conta de serviço)
   │  stdio
   ▼
Vendas.Servidor.dll (MCP)  ──►  SQL Server (views do schema mcp)
```

O VB6 **não entra nesta cadeia**: só abre o URL (secção 9).

## 3. Alteração de código necessária (proposta, não aplicada)

Sem ela, o IIS só serve para HTTPS/DNS, e o agente recebe os pedidos sem identidade (401) ou com a identidade do proxy. O `codigo/proxy-iis.patch` (aplica-se limpo à versão analisada; **ainda não compilado**, porque o ambiente da análise não tem o SDK .NET) acrescenta um **modo proxy opt-in**, ligado por `VENDAS_PROXY_SEGREDO`. Sem essa variável, o comportamento é exatamente o atual.

- `Perfis.LoginDoProxy(...)`: aceita o login de `X-Remote-User` **só** se o pedido vem de loopback **e** `X-Proxy-Secret` coincide com o segredo (comparação em tempo constante). Caso contrário devolve `null` e o pedido recebe 403, como hoje.
- Em modo proxy: o listener fica `Anonymous` (o Windows Auth é do IIS), só em `http://localhost:<porta>/`, não abre o browser, e `VENDAS_ENDERECO_PUBLICO` (HTTPS) passa a ser a referência da verificação de `Origin`.
- Testes novos em `LoginDoProxyTests.cs` (segredo certo/errado/ausente, fora de loopback, sem utilizador, segredo não configurado).

Quem aplicar: `git apply -p1 codigo/proxy-iis.patch`, depois `dotnet build Vendas.slnx -c Release --warnaserror` e `dotnet test Vendas.slnx -c Release --filter "Categoria!=Integração"`.

## 4. Pré-requisitos

1. **Windows Server** com a função IIS. (O IIS Manager do screenshot, INF-DESKTOP01, é Windows cliente: serve para ensaiar; produção em servidor. Os módulos que se veem — ASP, CGI, FTP, ASP.NET 4 — não são usados.)
2. **Windows Authentication** instalado (o ícone *Autenticação* existe, mas o fornecedor pode faltar; o `Instalar-IIS.ps1` ativa-o).
3. **Faltam no IIS do screenshot** (não há *Reescrita de URL* nem *Application Request Routing*):
   - URL Rewrite 2.1 — `https://www.iis.net/downloads/microsoft/url-rewrite`
   - Application Request Routing 3.0 — `https://www.iis.net/downloads/microsoft/application-request-routing`
   - Instalar os dois MSI (o Web Platform Installer já não existe). Depois de ARR, reabrir o IIS Manager: aparece *Application Request Routing Cache* no servidor.
4. **.NET 10 Runtime** (Microsoft.NETCore.App 10.x) para o agente — **não** o Hosting Bundle (o IIS não executa .NET aqui).
5. Certificado HTTPS no `Cert:\LocalMachine\My` e **registo DNS** (A/CNAME, nunca por IP) para `flow.inforap.local`.
6. Conta de serviço de domínio (ex. `INFORAP\svc-flow`) com leitura às views `mcp` (ou o `mcpserver` SQL + `sql.pwd`).

### Versão do .NET (e o VB6)

| Situação | O que fazer |
| --- | --- |
| Servidor com Windows Server 2012 ou mais recente | Instalar o **.NET 10 Runtime**. Convive lado a lado com outras versões; não afeta o VB6 nem outro software. |
| Não se quer/pode instalar o runtime | `dotnet publish src/Vendas.Agente -c Release -r win-x64 --self-contained` (e o mesmo para `src/Vendas.Servidor` em `servidor\`). Nota: `Program.cs` lança `dotnet Vendas.Servidor.dll`; em *self-contained* passa a ser o `Vendas.Servidor.exe` (2 linhas), por validar. |
| Servidor mais antigo que o Windows Server 2012 | Só aqui se justifica baixar o `TargetFramework` (4 `.csproj` + pacotes `Microsoft.Extensions.*`/`ModelContextProtocol`) e repetir build e testes. Não recomendado: o .NET 8 e o 9 já acabaram o suporte (nov/2026 e mai/2026); o 10 é LTS até nov/2028. |

Confirmar no servidor com `winver` e `dotnet --list-runtimes`. O VB6 só **lança um `.exe` ou abre um URL**; a versão do .NET do agente é-lhe invisível.

## 5. Passo a passo

### Passo 1 — Publicar o agente e o servidor

No PC de desenvolvimento, na raiz do repositório:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1
```

Copiar `pacote\Flow` para `C:\Flow\` no servidor (o servidor MCP fica em `C:\Flow\servidor\`).

### Passo 2 — Conta de serviço, SQL e perfis

Na conta `INFORAP\svc-flow` (a mesma que corre a tarefa do passo 3, porque o DPAPI só a ela decifra):

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Vendas" | Out-Null
Read-Host "Password SQL" -AsSecureString | ConvertFrom-SecureString | Set-Content "$env:LOCALAPPDATA\Vendas\sql.pwd"
```

(Com `Integrated Security=True` na `VENDAS_SQL` dispensa-se o `sql.pwd`.) Criar `%LOCALAPPDATA%\Vendas\perfis.json` com **cada login do domínio** e o vendedor, ou `"*"` para ver tudo. Sem o ficheiro, só a própria conta de serviço entra — nenhum utilizador real:

```json
{ "INFORAP\\diana.silva": "*", "INFORAP\\joao": "João Silva" }
```

### Passo 3 — Arrancar o agente sempre (Agendador de Tarefas)

1. Copiar `iis\Iniciar-Flow.cmd` para `C:\Flow\`, preencher `VENDAS_SQL` e `VENDAS_PROXY_SEGREDO` (o segredo vem do passo 5) e restringir o ficheiro (tem o segredo):

   ```powershell
   icacls C:\Flow\Iniciar-Flow.cmd /inheritance:r /grant:r "Administrators:F" "SYSTEM:F" "INFORAP\svc-flow:RX"
   ```

2. Criar a tarefa, ao iniciar o sistema, com reinício em caso de falha:

   ```powershell
   $acao = New-ScheduledTaskAction -Execute 'C:\Flow\Iniciar-Flow.cmd'
   $quando = New-ScheduledTaskTrigger -AtStartup
   $def = New-ScheduledTaskSettingsSet -RestartCount 5 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero) -StartWhenAvailable
   $c = Get-Credential INFORAP\svc-flow
   Register-ScheduledTask -TaskName 'Flow (agente)' -Action $acao -Trigger $quando -Settings $def `
       -User $c.UserName -Password $c.GetNetworkCredential().Password -RunLevel Limited
   Start-ScheduledTask -TaskName 'Flow (agente)'
   ```

3. Confirmar que ouve só em loopback: `netstat -ano | findstr :5080` → `127.0.0.1:5080`. A porta **5080 não pode estar aberta na firewall**.

### Passo 4 — Instalar URL Rewrite e ARR

Ver secção 4. Confirmar no IIS Manager que aparecem *Reescrita de URL* e *Application Request Routing Cache*.

### Passo 5 — Criar o site (script ou à mão)

**Com o script** (como Administrador; começar com `-WhatIf`):

```powershell
cd <pasta deste pacote>\iis
.\Instalar-IIS.ps1 -Nome flow.inforap.local -Thumbprint <thumbprint> -ContaPool INFORAP\svc-flow -WhatIf
.\Instalar-IIS.ps1 -Nome flow.inforap.local -Thumbprint <thumbprint> -ContaPool INFORAP\svc-flow
```

Faz: ativa Windows Authentication; liga o proxy ARR (**timeout 10 min** e **`responseBufferLimit=0`**, para o chat aparecer à medida que é gerado; **Host original não preservado**, senão o `HttpListener` dá 400); permite as variáveis `HTTP_X_REMOTE_USER` e `HTTP_X_PROXY_SECRET`; cria o pool (sem código gerido, conta de domínio) e o site em `C:\inetpub\flow` com binding HTTPS e certificado; escreve o `web.config` (regra de rewrite + segredo, com permissões restritas); Windows ON / Anónima OFF. No fim imprime o **segredo gerado** e os comandos do SPN.

**À mão** (equivalente): *Sites → Adicionar Web Site* (pasta `C:\inetpub\flow`, HTTPS, nome do anfitrião, certificado) · pool *Sem código gerido* com a conta de domínio · *Autenticação*: Windows = Ativado, Anónima = Desativado · *Application Request Routing Cache → Server Proxy Settings*: Enable proxy, Time-out 600 s, Response buffer threshold = 0, Preserve client IP/Reverse rewrite host desligados · *Reescrita de URL → Ver variáveis de servidor* (nível servidor): adicionar `HTTP_X_REMOTE_USER` e `HTTP_X_PROXY_SECRET` · copiar `iis\web.config` para a pasta do site, substituindo `SEGREDO_PARTILHADO`.

### Passo 6 — SPN (necessário)

A autenticação **Kerberos** só funciona com o SPN registado na conta do pool; sem ele o browser cai para NTLM, que atrás de proxy falha (o IIS e o agente não conseguem reaproveitar a credencial). Num controlador de domínio (administrador de domínio):

```powershell
setspn -S HTTP/flow.inforap.local INFORAP\svc-flow
setspn -S HTTP/flow INFORAP\svc-flow
setspn -L INFORAP\svc-flow     # confirma
setspn -X                      # procura SPN duplicados (um duplicado quebra o Kerberos)
```

- O nome do site tem de ser **DNS** (nunca IP) e igual ao do SPN.
- No site, *Autenticação Windows → Definições avançadas*: modo kernel ativo **com** "Usar credenciais do pool" (já feito pelo script), ou modo kernel desligado.
- Nos postos, o site tem de estar na zona **Intranet local** (GPO *Lista de atribuição de sites a zonas*: `https://flow.inforap.local` = 1), para o browser enviar o login sem pedir password. No Chrome/Edge, a política `AuthServerAllowlist` faz o mesmo.
- Testar num posto: abrir o site e, numa consola, `klist` deve mostrar `HTTP/flow.inforap.local`.

### Passo 7 — Verificação

| # | Teste | Esperado |
| --- | --- | --- |
| 1 | No servidor: `curl.exe -i http://localhost:5080/` | **403** (sem cabeçalhos de identidade) |
| 2 | No servidor: `curl.exe -i -H "X-Remote-User: INFORAP\diana.silva" -H "X-Proxy-Secret: <segredo>" http://localhost:5080/` | **200** (só com o login em `perfis.json`) |
| 3 | De um posto: `https://flow.inforap.local/` | Abre a página, sem pedir password |
| 4 | De fora, com um cabeçalho forjado: `curl.exe -k --negotiate -u : -H "X-Remote-User: INFORAP\outro" https://flow.inforap.local/` | Entra **como o utilizador autenticado**, nunca como `outro` (o IIS sobrescreve o cabeçalho) |
| 5 | Fazer uma pergunta no chat | A resposta aparece **aos poucos** (não só no fim) |
| 6 | Painel: resumo, clientes, créditos | Sem 403 |
| 7 | `%LOCALAPPDATA%\Vendas\auditoria.log` (conta de serviço) | Login do utilizador real |
| 8 | De um posto: `http://servidor:5080/` | **Não liga** (firewall) |

### Passo 8 — Operação

- Atualizar versão: parar a tarefa (`Stop-ScheduledTask 'Flow (agente)'`), copiar o novo `pacote\Flow`, preservar `Iniciar-Flow.cmd`, arrancar. O site IIS não muda.
- Alterou `perfis.json`: reiniciar a tarefa (é lido no arranque).
- Logs do agente: redirecionar no `Iniciar-Flow.cmd` com `>> C:\Flow\flow.log 2>&1`; pedidos do IIS em `C:\inetpub\logs\LogFiles`.

## 6. Segurança

- O cabeçalho de identidade é seguro **só** com as três condições juntas: pedido de loopback, segredo correto, porta 5080 fechada à rede. Qualquer processo local que conheça o segredo pode falsificar um utilizador: o segredo só pode estar no `web.config` e no `Iniciar-Flow.cmd`, ambos com NTFS restrito.
- Em **RDS/servidor partilhado**, outros utilizadores com sessão na máquina chegam ao `localhost:5080`: sem o segredo, recebem 403. Rodar o segredo se alguém não autorizado o viu (alterar nos dois ficheiros e reiniciar a tarefa).
- HTTPS obrigatório no IIS. O patch recusa arrancar em modo proxy sem `VENDAS_ENDERECO_PUBLICO` em HTTPS e com escuta fora de loopback.
- `perfis.json`: só entram os logins listados (vendedor vê o painel filtrado, sem chat nem email).

## 7. Limitações

- Uma instância central; **uma pergunta ao modelo de cada vez** (os outros veem "Na fila").
- ARR com Kerberos exige SPN e conta de domínio no pool; quem gerir o domínio tem de o criar.
- `GET /api/documentos/<id>/pdf` continua por ligar (404), como na versão atual.

## 8. Resolução de problemas

| Sintoma | Causa provável |
| --- | --- |
| Pede password em ciclo / 401.1 | Sem SPN, SPN duplicado (`setspn -X`) ou site fora da zona Intranet. |
| 401.2/500.19 ao abrir | Windows Auth não instalado, ou autenticação definida no `web.config` (está bloqueada: usar o `applicationHost` via `appcmd`). |
| 502.3 / 502.4 | Agente parado (`Get-ScheduledTask 'Flow (agente)'`), porta diferente no `web.config`, ou o timeout do ARR menor que o do chat. |
| 400 Bad Request do agente | *Preserve host header* ligado: o `HttpListener` só responde a `localhost:5080`. |
| O chat só mostra tudo no fim | `responseBufferLimit` ≠ 0 ou compressão dinâmica ativa. |
| 403 em POST (`/perguntar`, `/preferencias`) | `VENDAS_ENDERECO_PUBLICO` diferente do endereço que o browser usa (o `Origin` tem de coincidir com ele). |
| 403 "sem acesso" a um utilizador | Login em falta em `perfis.json` (formato `DOMINIO\\nome`), ou segredo diferente entre `web.config` e `Iniciar-Flow.cmd`. |
| Servidor MCP não liga / "Não consegui arrancar" | `VENDAS_SERVIDOR_DLL` errado, ou `sql.pwd` criado por outra conta (DPAPI). |

**Rollback:** `Stop-Website Flow`, parar a tarefa, e continuar a usar o atalho "Inforap Flow" (`Vendas.Agente.exe --web`) sem `VENDAS_PROXY_SEGREDO`, como hoje.

## 9. Integração com o VB6

Com o IIS, o botão do VB6 deixa de lançar o `Vendas.Agente.exe`: abre só o endereço do site (o agente já corre como tarefa). O browser envia o login Windows do utilizador ao IIS.

```vb
Private Declare Function ShellExecute Lib "shell32.dll" Alias "ShellExecuteA" _
    (ByVal hwnd As Long, ByVal lpOperation As String, ByVal lpFile As String, _
     ByVal lpParameters As String, ByVal lpDirectory As String, ByVal nShowCmd As Long) As Long

Private Sub cmdFlow_Click()
    ShellExecute 0&, "open", "https://flow.inforap.local/", vbNullString, vbNullString, 1
End Sub
```

Se o VB6 estiver a lançar o `.exe` e a verificar a resposta, trocar por este `ShellExecute`; não há mais nada a mudar do lado do VB6.

# Flow (Vendas.Agente) em IIS — análise, arquitetura e passo a passo

> Estado: **por validar num IIS real.** A alteração de código (modo proxy) já está no repositório, compila e tem testes. Os scripts de `iis\` passaram só a análise de sintaxe: não foram executados em IIS/ARR/Kerberos. O `Instalar-IIS.ps1` valida cada passo e pára no primeiro que falhe; o `Verificar-Flow.ps1` corre os testes do passo 7.

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

## 3. Alteração de código (já aplicada)

Sem ela, o IIS só serve para HTTPS/DNS, e o agente recebe os pedidos sem identidade (401) ou com a identidade do proxy. O **modo proxy opt-in**, ligado por `VENDAS_PROXY_SEGREDO`, está em `Program.cs` e `Perfis.cs`. Sem essa variável, o comportamento é exatamente o atual.

- `Perfis.LoginDoProxy(...)`: aceita o login de `X-Remote-User` **só** se o pedido vem de loopback **e** `X-Proxy-Secret` coincide com o segredo (comparação em tempo constante). Caso contrário devolve `null` e o pedido recebe 403, como hoje.
- Em modo proxy: o listener fica `Anonymous` (o Windows Auth é do IIS), só em `http://localhost:<porta>/`, não abre o browser, e `VENDAS_ENDERECO_PUBLICO` (HTTPS) passa a ser a referência da verificação de `Origin`.
- Testes em `LoginDoProxyTests.cs` (segredo certo/errado/ausente, fora de loopback, sem utilizador, segredo não configurado).

Validar antes de publicar: `dotnet build Vendas.slnx -c Release --warnaserror` e `dotnet test Vendas.slnx -c Release --filter "Categoria!=Integração"`.

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

Copiar `pacote\Flow` para `C:\Flow\` no servidor (o servidor MCP fica em `C:\Flow\servidor\`, os scripts do IIS em `C:\Flow\iis\`). **Validar:** existem `C:\Flow\Vendas.Agente.exe`, `C:\Flow\servidor\Vendas.Servidor.dll` e `C:\Flow\iis\Instalar-IIS.ps1`. O `Configurar.cmd` do pacote é para postos, não se usa no servidor.

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

1. Fazer **antes** o passo 5: o `Instalar-IIS.ps1` copia `iis\Iniciar-Flow.cmd` para `C:\Flow\`, grava lá o segredo, o endereço público e a porta, e restringe as permissões (o ficheiro tem o segredo). Depois editar só a linha `VENDAS_SQL` (sem a palavra-passe).

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

3. O **email semanal/mensal** é outra tarefa agendada, `iis\Enviar-Resumo.cmd` (copiado pelo instalador): ver [RESUMO-EMAIL.md](RESUMO-EMAIL.md). Tem de correr na mesma conta.
4. Confirmar que ouve só em loopback: `netstat -ano | findstr :5080` → `127.0.0.1:5080`. A porta **5080 não pode estar aberta na firewall**. Se não ouvir, `C:\Flow\flow.log` diz porquê (`dotnet` fora do PATH da conta, SQL, `sql.pwd` de outra conta).

### Passo 4 — Instalar URL Rewrite e ARR

Ver secção 4. Confirmar no IIS Manager que aparecem *Reescrita de URL* e *Application Request Routing Cache*.

### Passo 5 — Criar o site (script ou à mão)

**Com o script** (como Administrador; começar com `-WhatIf`, que só chega ao fim com o IIS já instalado):

```powershell
cd C:\Flow\iis
.\Instalar-IIS.ps1 -Nome flow.inforap.local -Thumbprint <thumbprint> -ContaPool INFORAP\svc-flow -WhatIf
.\Instalar-IIS.ps1 -Nome flow.inforap.local -Thumbprint <thumbprint> -ContaPool INFORAP\svc-flow
```

Cada passo imprime `OK` ou pára com `FALHOU: <motivo>`; repetir depois de corrigir é seguro (o que existe não é recriado, o segredo é reaproveitado). Valida, por ordem: funções do Windows, URL Rewrite e ARR, certificado (chave privada, validade, nome), DNS, pacote e .NET 10, segredo e `Iniciar-Flow.cmd`, proxy ARR, variáveis de servidor, pool, site e certificado no 443, autenticação, `web.config`, arranque e um teste de fumo. Faz: ativa Windows Authentication; liga o proxy ARR (**timeout 10 min** e **`responseBufferLimit=0`**, para o chat aparecer à medida que é gerado; **Host original não preservado**, senão o `HttpListener` dá 400); permite as variáveis `HTTP_X_REMOTE_USER` e `HTTP_X_PROXY_SECRET`; cria o pool (sem código gerido, conta de domínio) e o site em `C:\inetpub\flow` com binding HTTPS e certificado; escreve o `web.config` (regra de rewrite + segredo, com permissões restritas); Windows ON / Anónima OFF. O segredo não é impresso: fica só no `web.config` e no `Iniciar-Flow.cmd` (ambos restritos). No fim lembra o SPN, a tarefa e o teste.

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

`.\Verificar-Flow.ps1 -Nome flow.inforap.local -Login INFORAP\diana.silva` corre os testes 1, 2, 3, 4 e o http, mais a porta 5080 só em loopback, e sai com 1 se algum falhar. Os restantes à mão:

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
- Logs do agente: `C:\Flow\flow.log` (o `Iniciar-Flow.cmd` já redireciona para lá); pedidos do IIS em `C:\inetpub\logs\LogFiles`.

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

## 9. Ensaio local (um só PC, sem domínio)

Valida o IIS, o ARR, o segredo e o chat a aparecer aos poucos. **Não** valida SPN, Kerberos, zona Intranet nem o login do domínio: isso só no servidor real. PowerShell como Administrador; instalar antes URL Rewrite e ARR (secção 4).

1. **Pacote local:** `.\scripts\publicar.ps1` e `Copy-Item pacote\Flow C:\Flow -Recurse`.
2. **Certificado autoassinado, confiado, e nome no `hosts`:**

   ```powershell
   $c = New-SelfSignedCertificate -DnsName flow.local -CertStoreLocation Cert:\LocalMachine\My -NotAfter (Get-Date).AddYears(1)
   Export-Certificate $c -FilePath $env:TEMP\flow.cer | Out-Null
   Import-Certificate $env:TEMP\flow.cer -CertStoreLocation Cert:\LocalMachine\Root | Out-Null   # senão o browser e o Verificar-Flow recusam
   Add-Content C:\Windows\System32\drivers\etc\hosts "127.0.0.1 flow.local"
   $c.Thumbprint
   ```

3. **Evitar o 401 do *loopback check*** (abrir `https://flow.local` no próprio PC, com um nome que não é o do computador):

   ```powershell
   New-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\Lsa\MSV1_0 -Name BackConnectionHostNames -PropertyType MultiString -Value flow.local -Force
   iisreset   # se continuar a dar 401, reiniciar o PC
   ```

4. **Instalar o IIS** com a conta do próprio utilizador no pool (o aviso de DNS pode ignorar-se neste ensaio):

   ```powershell
   cd C:\Flow\iis
   .\Instalar-IIS.ps1 -Nome flow.local -Thumbprint <thumbprint> -ContaPool "$env:USERDOMAIN\$env:USERNAME" -WhatIf
   .\Instalar-IIS.ps1 -Nome flow.local -Thumbprint <thumbprint> -ContaPool "$env:USERDOMAIN\$env:USERNAME"
   ```

5. **Agente à mão**, numa consola que fica aberta (para ver os erros), com a porta 5080 livre (parar outro `Vendas.Agente`): preencher `VENDAS_SQL` em `C:\Flow\Iniciar-Flow.cmd` (o `sql.pwd` tem de ser da mesma conta), criar `%LOCALAPPDATA%\Vendas\perfis.json` com o login, ter o Ollama a correr (ou `AGENTE_LLM=claude`) e correr `C:\Flow\Iniciar-Flow.cmd`.
6. **Verificar:** `C:\Flow\iis\Verificar-Flow.ps1 -Nome flow.local -Login "$env:USERDOMAIN\$env:USERNAME"`; abrir `https://flow.local/` e fazer uma pergunta (a resposta tem de aparecer aos poucos).
7. **Limpeza:** `Remove-Website Flow; Remove-WebAppPool Flow`; apagar o certificado de `LocalMachine\My` e `LocalMachine\Root`, a linha do `hosts` e o valor `BackConnectionHostNames`. O proxy do ARR fica ligado ao nível do servidor (inofensivo; desligar se a máquina for partilhada).

## 10. Integração com o VB6

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

## 11. Demonstração remota: o Visual autentica (passo a passo)

Para mostrar o Flow a clientes fora do domínio, por um endereço público (DynDNS + Let's Encrypt). **Não usa Windows Auth nem Kerberos/SPN**: quem autentica é o Visual, que abre o Flow com um link assinado. As secções 1 a 8 continuam válidas para o modo interno; esta substitui os passos 5 (autenticação), 6 (SPN) e 7 (verificação).

> Estado: o código (agente, testes de assinatura e cookie) compila e passa os testes. Os scripts passaram só a análise de sintaxe e o `FlowLink.bas` não foi compilado em VB6. Nada correu ainda num IIS real: validar cada passo pela ordem abaixo.

### Como funciona

```text
Visual (PC do cliente, utilizador já autenticado)
   │  botão "Ligar o Flow": assina  login|validade(60 s)  com o segredo
   ▼  abre  https://<nome>/?u=<login>&e=<validade>&s=<assinatura>
IIS (Anónima, só HTTPS 443, encaminha)  ──►  agente (localhost:5080)
                                             valida assinatura, validade e uso único
                                             → cookie de sessão (8 h) → redireciona para /
```

- Sem link nem cookie válidos o agente responde **403**. O link é de **uso único** e vale **60 s** a contar do clique no botão (não da entrada no Visual); depois manda o cookie.
- O `login` do link é procurado no `perfis.json`: o que o utilizador vê (tudo, ou só um vendedor) define-se aí.
- O segredo (`VENDAS_LINK_SEGREDO`) só existe no `Iniciar-Flow.cmd` (restrito) e no Visual.

### Passo 1 — Pré-requisitos no servidor

1. Windows Server (ou Windows com IIS, para ensaiar), **.NET 10 Runtime** (`dotnet --list-runtimes`), **URL Rewrite 2.1** e **Application Request Routing 3.0** (secção 4).
2. Nome público a apontar para o servidor (DynDNS): `joaqmendes.dyndns.org` ou outro. Usado em todos os passos como `<nome>`.
3. **BD de demonstração** e login SQL só de leitura. Nunca a de produção.
4. Router/firewall: porta **443** (e **80**, só durante a emissão e renovação do certificado) para o servidor. A **5080 nunca**.

### Passo 2 — Pacote e dados

1. No PC de desenvolvimento: `powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1`; copiar `pacote\Flow` para `C:\Flow\` no servidor.
2. Escolher a conta que corre o agente e o pool (conta local dedicada, ex. `SERVIDOR\svc-flow`; não precisa de ser de domínio). Nessa conta:

   ```powershell
   New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Vendas" | Out-Null
   Read-Host "Password SQL" -AsSecureString | ConvertFrom-SecureString | Set-Content "$env:LOCALAPPDATA\Vendas\sql.pwd"
   ```

3. Criar `%LOCALAPPDATA%\Vendas\perfis.json` com **os logins que o Visual vai enviar** (cada um com o vendedor, ou `"*"`):

   ```json
   { "cliente\\ana": "*", "cliente\\rui": "João Silva" }
   ```

### Passo 3 — Certificado temporário e site IIS

O `Instalar-IIS.ps1` exige um certificado para criar o site; o definitivo vem no passo 5.

```powershell
$c = New-SelfSignedCertificate -DnsName <nome> -CertStoreLocation Cert:\LocalMachine\My -NotAfter (Get-Date).AddMonths(3)
cd C:\Flow\iis
.\Instalar-IIS.ps1 -Nome <nome> -Thumbprint $c.Thumbprint -ContaPool SERVIDOR\svc-flow -LinkAssinado -WhatIf
.\Instalar-IIS.ps1 -Nome <nome> -Thumbprint $c.Thumbprint -ContaPool SERVIDOR\svc-flow -LinkAssinado
```

O script (como Administrador) faz o proxy ARR, o pool, o site em HTTPS, a autenticação **Anónima**, o `web.config`, e gera o `VENDAS_LINK_SEGREDO` no `C:\Flow\Iniciar-Flow.cmd`. Pára no primeiro passo que falhe; repetir é seguro. Depois, editar em `Iniciar-Flow.cmd` só a linha `VENDAS_SQL` (sem a palavra-passe). O aviso de DNS ignora-se se o nome ainda não resolver aqui.

### Passo 4 — Arrancar o agente

Criar a tarefa agendada `Flow (agente)` com a conta `SERVIDOR\svc-flow` (secção 5, passo 3) e iniciá-la. Confirmar:

- `netstat -ano | findstr :5080` mostra só `127.0.0.1:5080`;
- `C:\Flow\flow.log` sem erros (se o agente recusar arrancar, o motivo está aí).

### Passo 5 — Certificado Let's Encrypt (win-acme)

1. Descarregar o win-acme (https://www.win-acme.com/), descompactar em `C:\win-acme`.
2. Como Administrador: `cd C:\win-acme` e `.\wacs.exe`. Usar a opção **M — Create certificate (full options)** e escolher:
   - alvo: *Manual input* → `<nome>`;
   - validação: **Self-hosting** (o win-acme abre a porta 80 durante a validação; por isso o site Flow não tem binding na 80, e o 401 da autenticação não interfere);
   - armazenamento: **Windows Certificate Store**, loja **My** (o `Instalar-IIS.ps1` procura em `LocalMachine\My`);
   - instalação: **IIS** (atualiza o binding 443 do site Flow e agenda a renovação).
3. Confirmar no IIS Manager (*Flow → Bindings*) o binding `https 443 <nome>` com o certificado novo, e apagar o autoassinado do passo 3.
4. Se voltar a correr o `Instalar-IIS.ps1`, passar o thumbprint **novo** em `-Thumbprint` (o de `Get-ChildItem Cert:\LocalMachine\My`).

### Passo 6 — Verificação no servidor

```powershell
.\Verificar-Flow.ps1 -Nome <nome> -Login "cliente\ana" -LinkAssinado
```

Testa: o agente só em loopback, 403 sem identidade, link válido (302), mesmo link segunda vez (403), link expirado (403), login trocado (403), cabeçalho forjado ignorado (403) e HTTP sem TLS. Os testes de links usam o nome público: do próprio servidor pode falhar a resolução (hairpin NAT); nesse caso correr de outra máquina ou pôr `<nome>` no `hosts` a apontar para 127.0.0.1.

Manuais: abrir um link gerado (secção "Assinatura" abaixo) num browser e fazer uma pergunta no chat (a resposta aparece aos poucos); `auditoria.log` mostra `entrada por link` com o login e o IP.

### Passo 7 — O botão no Visual

1. Importar [FlowLink.bas](FlowLink.bas) no projeto VB6.
2. Ligar o botão a `AbrirFlow "https://<nome>", <login do Visual>, <VENDAS_LINK_SEGREDO>`. O segredo lê-se de configuração do Visual, não fica em texto no código. O login tem de existir no `perfis.json` e ser ASCII.
3. **Primeiro** comparar a assinatura do `.bas` com a do exemplo PowerShell abaixo, para o mesmo login e a mesma validade.
4. O relógio do PC do cliente tem de estar certo (margem de uns segundos), porque o link vale 60 s.

**Assinatura:** `s = HMAC-SHA256(chave = VENDAS_LINK_SEGREDO em texto UTF-8, mensagem = login & "|" & e)`, em hexadecimal **minúsculo**; `e` = validade em segundos Unix (UTC), no máximo 60 s à frente; `u` codificado em URL (`\` → `%5C`). Link de teste:

```powershell
$seg = '<VENDAS_LINK_SEGREDO>'; $u = 'cliente\ana'; $e = [DateTimeOffset]::UtcNow.AddSeconds(30).ToUnixTimeSeconds()
$h = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($seg))
$s = -join ($h.ComputeHash([Text.Encoding]::UTF8.GetBytes("$u|$e")) | % { $_.ToString('x2') })
"https://<nome>/?u=$([uri]::EscapeDataString($u))&e=$e&s=$s"   # abrir em menos de 30 s
```

### Passo 8 — Teste final, de fora da rede

Do telemóvel (dados móveis): `https://<nome>/` sem link → mensagem de acesso negado (403). Depois, a partir do Visual, o botão abre o Flow já com o utilizador. Um segundo clique gera outro link; o link antigo, reaberto, dá 403.

### Operação e fim da demonstração

- **Rodar o segredo** (no fim, ou se foi visto por quem não devia): apagar o valor de `VENDAS_LINK_SEGREDO` em `Iniciar-Flow.cmd`, repetir o `Instalar-IIS.ps1 -LinkAssinado`, reiniciar a tarefa e atualizar o Visual. As sessões abertas caem (o cookie deixa de ser válido).
- Alterou `perfis.json`: reiniciar a tarefa. Atualizar versão: secção 5, passo 8.
- **Terminar a demo:** `Stop-Website Flow`, parar a tarefa e fechar o 80/443 no router.

### Limites e segurança

- Quem tiver o segredo entra como qualquer login: guardá-lo fora do repositório e usar um segredo por cliente quando possível.
- O link é de uso único (o agente recusa a 2.ª utilização; a memória perde-se num reinício, mas o link já expirou), vale 60 s e fica nos logs do IIS. Cada entrada fica no `auditoria.log`.
- O cookie dura 8 h e não é revogável individualmente: só rodando o segredo.
- Neste modo o cabeçalho `X-Remote-User` é ignorado pelo agente.
- Não há limite de tentativas falhadas (o HMAC de 256 bits não se adivinha). Se o site ficar muito tempo no ar, acrescentar uma regra no router/firewall.
- Usar BD de demonstração, perfis com o mínimo necessário e, no modo Claude, um limite de custo.

### Resolução de problemas (modo link)

| Sintoma | Causa provável |
| --- | --- |
| 403 ao abrir o link do Visual | Login em falta no `perfis.json` (ou com maiúsculas/barra diferentes), segredo diferente entre o Visual e o `Iniciar-Flow.cmd`, relógio do PC desfasado, link já usado ou expirado. |
| A assinatura do VB6 difere da do PowerShell | Mensagem diferente (`login|e`, sem espaços), segredo com espaço ou quebra de linha, `e` diferente, hex em maiúsculas. |
| O agente não arranca e fala de `VENDAS_LINK_SEGREDO` | Falta o modo proxy (`VENDAS_PROXY_SEGREDO`) ou o segredo tem menos de 32 caracteres. |
| win-acme falha a validar | Porta 80 ocupada ou fechada no router, DNS não aponta para o IP público, ou o site Flow tem binding na 80. |
| `Instalar-IIS.ps1` não encontra o certificado | O win-acme guardou-o na loja `WebHosting`; repetir com a loja `My`. |
| 502 | Agente parado (`Get-ScheduledTask 'Flow (agente)'`). Outros sintomas: secção 8. |

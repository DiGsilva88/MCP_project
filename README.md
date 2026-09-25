# Vendas.Servidor — Servidor MCP de Vendas

Servidor [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) escrito em C# (.NET 10), que expõe ferramentas ("tools") para consultar dados de clientes a um cliente/agente de IA (ex.: Claude Desktop, MCP Inspector, ou o `Vendas.Agente` deste repositório com Ollama).

## O que é o MCP, neste projeto

O MCP é um protocolo baseado em JSON-RPC 2.0 que permite a um cliente de IA descobrir e invocar "ferramentas" expostas por um servidor. Este servidor comunica por **stdio** (entrada/saída standard do processo) — não abre nenhuma porta de rede. O cliente (o "host") lança o executável, troca mensagens JSON-RPC linha a linha, e pode chamar as tools registadas.

## Estrutura do projeto

```text
projeto_Mcp/
├── Vendas.slnx                      Solution do .NET
├── inspector.json                   Config local do MCP Inspector (gitignored)
└── src/
    ├── Vendas.Servidor/             Servidor MCP
    │   ├── Program.cs               Ponto de entrada: host, DI, ligação ao SQL e servidor MCP
    │   ├── Vendas.Servidor.csproj
    │   ├── Ferramentas/
    │   │   └── VistasTools.cs       Tools clientes_consultar e faturacao_consultar
    │   └── Modelos/
    │       ├── ContagemCliente.cs   enum Campo, record ContagemCliente
    │       ├── ClienteGrupo.cs      record PaginaClientes
    │       ├── Vendas.cs, VendasPorCliente.cs, VendasPorProduto.cs, ClientesInativos.cs
    │       └── Dados/
    │           ├── IVendasRepo.cs       Contrato do repositório
    │           ├── RepoSql.cs           Implementação em SQL Server (única — sem fallback em memória)
    │           └── ConsultaClientes.cs  Construção das queries
    └── Vendas.Agente/               Host de consola: Ollama (qwen2.5) + servidor MCP
        ├── Program.cs               Lança o servidor, liga o modelo às tools, ciclo de perguntas
        └── PoliticasSeguranca.cs    Regras de segurança (prompt de sistema)
```

## Como funciona

1. **`Program.cs`** cria um `Host` genérico do .NET (`Host.CreateApplicationBuilder`), configura logging para `stderr` (obrigatório — o `stdout` fica reservado para o protocolo MCP) e liga o servidor MCP:

   ```csharp
   builder.Services
       .AddMcpServer()
       .WithStdioServerTransport()
       .WithToolsFromAssembly();
   ```

   `WithToolsFromAssembly()` regista automaticamente os métodos marcados com `[McpServerTool]` nas classes `[McpServerToolType]`.

2. **Ligação ao SQL Server.** A connection string vem da variável de ambiente `VENDAS_SQL`, lida uma vez no arranque. Se existir `%LOCALAPPDATA%\Vendas\sql.pwd`, a password é lida desse ficheiro (encriptado com DPAPI) e acrescentada à ligação — assim o `VENDAS_SQL` não precisa de ter `Password=`. **Não há fallback em memória:** sem `VENDAS_SQL`, o servidor lança `InvalidOperationException` e não arranca.

3. **`Ferramentas/VistasTools.cs`** define as tools, uma por view SQL:
   - `clientes_consultar` — dados gerais (`NomeCliente`, `Zona`, `Vendedor`, `TipoCliente`, `Actividade`, `Distrito`), view `[dbo].[ViewMCP_cliente]`.
   - `faturacao_consultar` — condições de faturação (`NomeCliente`, `Pagamento`, `Cobranca`, `Expedicao`, `SituacaoFinanceira`, `EscalaoPlafond`, `EscalaoVolumeVendas`), view `[dbo].[ViewMCP_cliente_faturacao]`. Não devolve valores faturados.

   Parâmetros: `coluna` (opcional, mostrar/filtrar/contar), `valor` (filtro exato), `contar=true` (agrupa com percentagens) e `limite` (1–100, por omissão 50). Devolvem CSV. Erros de SQL nunca chegam ao modelo: a tool devolve "Não foi possivel consultar os dados neste momento" e o detalhe fica no log (`stderr`).

## Configuração da ligação

Requisitos: .NET SDK 10; Node.js (`npx`) para o MCP Inspector; Ollama para o `Vendas.Agente`.

```powershell
dotnet build Vendas.slnx
```

**1. Password encriptada (DPAPI, só Windows).** Só o teu utilizador Windows, nesta máquina, a consegue desencriptar. Correr no teu terminal (pede a password sem a mostrar):

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Vendas" | Out-Null
Read-Host "Password SQL" -AsSecureString | ConvertFrom-SecureString | Set-Content "$env:LOCALAPPDATA\Vendas\sql.pwd"
```

Quando a password mudar, basta repetir o comando.

**2. `VENDAS_SQL` sem password**, no `env` do host (ver abaixo) ou de forma persistente:

```powershell
setx VENDAS_SQL "Server=tcp:<servidor>,<porta>;Database=<bd>;User Id=<login>;Encrypt=True;TrustServerCertificate=True"
```

(depois de `setx`, é preciso abrir um terminal/processo novo.)

O login SQL deve ter só permissões de leitura sobre as duas views.

## Usar com o Claude Desktop

O Claude Desktop instalado pela Microsoft Store / MSIX **não lê** `%APPDATA%\Claude\claude_desktop_config.json`. O ficheiro certo é:

```text
%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude_desktop_config.json
```

(Settings → Developer → Edit Config abre sempre o ficheiro certo.) Acrescentar `mcpServers` ao nível de topo, sem apagar as outras chaves:

```json
{
  "mcpServers": {
    "vendas": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\...\\projeto_Mcp\\src\\Vendas.Servidor\\Vendas.Servidor.csproj", "--no-build"],
      "env": { "VENDAS_SQL": "Server=tcp:<servidor>,<porta>;Database=<bd>;User Id=<login>;Encrypt=True;TrustServerCertificate=True" }
    }
  }
}
```

Em alternativa, usar a versão publicada: `dotnet publish src/Vendas.Servidor -c Release -o C:\mcp\vendas` e `"command": "C:\\mcp\\vendas\\Vendas.Servidor.exe"` sem `args`.

Notas:

- **Editar o config só com a app fechada** (ícone na bandeja → **Quit**; fechar a janela não chega). Com a app aberta, ela regrava o ficheiro e apaga a alteração.
- Com `--no-build`, é preciso `dotnet build` depois de alterar o código; com o `.exe` publicado, é preciso publicar de novo (com a app fechada, senão o `.exe` está bloqueado).
- Numa conversa nova, perguntar em linguagem natural, ex.: *"Quantos clientes temos por distrito?"*, *"Lista os clientes da zona Lisboa"*, *"Que percentagem de clientes há em cada escalão de plafond?"*. O modelo escolhe a tool; abrir o bloco da chamada mostra os parâmetros e o CSV devolvido.
- As regras de `PoliticasSeguranca.cs` **não** se aplicam no Claude Desktop. Para as usar, criar um Project e colar o texto nas instruções do projeto.
- Logs: `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\Claude\logs\` (`main.log` e `mcp-server-vendas.log`).

## Usar com o Claude Code (terminal)

Alternativa à app, sem interface gráfica. Registar o servidor uma vez (âmbito `user`, fica em `~/.claude.json` e disponível em qualquer pasta):

```powershell
claude mcp add vendas --scope user -e "VENDAS_SQL=Server=tcp:<servidor>,<porta>;Database=<bd>;User Id=<login>;Encrypt=True;TrustServerCertificate=True" -- dotnet run --project "C:\...\projeto_Mcp\src\Vendas.Servidor" --no-build
claude mcp get vendas      # deve mostrar "Connected"
```

- Abrir uma sessão **nova** com `claude` (as tools só aparecem em sessões novas) e perguntar em linguagem natural. Na primeira chamada pede autorização para `mcp__vendas__clientes_consultar` / `mcp__vendas__faturacao_consultar`.
- Dentro da sessão, `/mcp` mostra o estado do servidor e as tools.
- Remover: `claude mcp remove vendas -s user`.

## Onde pode correr

Qualquer host (Claude Desktop, Claude Code, Vendas.Agente, Inspector) tem de correr **neste PC, na sessão Windows do utilizador**:

- o transporte é **stdio** — o host lança o servidor como processo filho na mesma máquina;
- o `sql.pwd` está protegido com DPAPI da conta Windows — só um processo desse utilizador, nesse PC, o desencripta;
- o SQL Server está na rede interna.

Para usar a partir de outro PC ou do telemóvel seria preciso mudar para transporte HTTP (`ModelContextProtocol.AspNetCore`), alojar o servidor como serviço com autenticação própria e, para um conector do claude.ai, expô-lo à internet — decisão de IT/segurança, fora do âmbito atual.

## Usar com o Vendas.Agente (Ollama)

Host de consola local, sem cloud. Precisa do Ollama a correr em `http://localhost:11434` com o modelo `qwen2.5` (`ollama pull qwen2.5`). O agente lança o servidor com `dotnet run` e passa-lhe o `VENDAS_SQL` do seu próprio ambiente; a password vem do `sql.pwd`.

```powershell
dotnet run --project src/Vendas.Agente
```

Mostra as tools disponíveis, depois lê perguntas do teclado (Enter vazio termina) e indica que tools usou em cada resposta.

## Testar com o MCP Inspector

Criar um `inspector.json` local (está no `.gitignore`) com o mesmo bloco `mcpServers` acima, mas `"args": ["run", "--project", "src\\Vendas.Servidor"]`, e:

```powershell
npx @modelcontextprotocol/inspector --config inspector.json --server vendas
```

No Inspector: "List Tools" e invocar cada tool para validar a resposta.

> Nota: como a ligação é por stdio, cada sessão do cliente corresponde a um processo do servidor. Se alterares o código, é preciso recompilar **e** reiniciar a ligação no cliente.

## Resolução de problemas

| Sintoma | Causa provável |
| --- | --- |
| O conector não aparece no Claude Desktop | Config editado no ficheiro errado (`%APPDATA%` em vez do da pasta `Packages`), ou a app estava aberta e regravou o ficheiro. `main.log` com `no stdio servers connected` confirma. |
| Tool devolve "Não foi possivel consultar os dados neste momento" | Ver `mcp-server-vendas.log`. `Login failed ... 18456` → password errada/ausente: recriar o `sql.pwd`. |
| Servidor não arranca: `VENDAS_SQL não definida` | Falta o `env` no config do host, ou o terminal foi aberto antes do `setx`. |
| `dotnet build` falha com `MSB3027`/`MSB3021` ("... is locked by") | Há processos `Vendas.Servidor.exe` antigos (Inspector, Claude Desktop). Terminá-los e recompilar (os PIDs vêm na mensagem de erro). |

```powershell
Stop-Process -Id <PID1>,<PID2> -Force
Get-Process -Name Vendas.Servidor -ErrorAction SilentlyContinue   # confirma que não sobrou nenhum
dotnet build src/Vendas.Servidor
```

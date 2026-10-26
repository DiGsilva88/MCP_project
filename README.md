# Vendas.Servidor — Servidor MCP de Vendas

Servidor [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) escrito em C# (.NET 10), que expõe ferramentas ("tools") para consultar dados de clientes e de faturação a um cliente/agente de IA (ex.: Claude Desktop, MCP Inspector, ou o `Vendas.Agente` deste repositório com Ollama).

## O que é o MCP, neste projeto

O MCP é um protocolo baseado em JSON-RPC 2.0 que permite a um cliente de IA descobrir e invocar "ferramentas" expostas por um servidor. Este servidor comunica por **stdio** (entrada/saída standard do processo) — não abre nenhuma porta de rede. O cliente (o "host") lança o executável, troca mensagens JSON-RPC linha a linha, e pode chamar as tools registadas.

## Estrutura do projeto

```text
projeto_Mcp/
├── Vendas.slnx                      Solution do .NET
├── inspector.json                   Config local do MCP Inspector (gitignored)
├── .github/workflows/build.yml      CI: restore, build (--warnaserror) e testes unitários
├── src/
│   ├── Vendas.Servidor/             Servidor MCP
│   │   ├── Program.cs               Ponto de entrada: host, DI, ligação ao SQL e servidor MCP
│   │   ├── Vendas.Servidor.csproj
│   │   ├── Ferramentas/
│   │   │   ├── VistasTools.cs       Tools consultar (ficha + condições de faturação) e consultar_sensivel
│   │   │   ├── VendasTools.cs       Tool consultar_vendas (faturação real por período)
│   │   │   └── Respostas.cs         Partilhado: limites, erro neutro, CSV, execução com log, "valores existentes"
│   │   ├── Modelos/
│   │   │   ├── Campo.cs             enum Campo (colunas permitidas nas views de clientes)
│   │   │   ├── CampoSensivel.cs     enum CampoSensivel (NIF, contactos, volume, plafond)
│   │   │   ├── Campos.cs            normalização de valores ("sem dados")
│   │   │   ├── Dimensoes.cs         enums DimensaoVenda (Cliente, Artigo, Familia, Marca, Mes, Localidade,
│   │   │   │                        Distrito, Zona, Vendedor, Actividade) e OrdenarVenda (Valor, Quantidade)
│   │   │   ├── Periodo.cs           enum Periodo + cálculo das datas [desde, ate[ (período, mês/ano ou datas)
│   │   │   ├── ContagemCliente.cs, PaginaClientes.cs, ValorAgrupado.cs   records de resultado
│   │   │   └── Dados/
│   │   │       ├── IVendasRepo.cs       Contrato do repositório
│   │   │       ├── RepoSql.cs           Implementação em SQL Server (única — sem fallback em memória)
│   │   │       ├── ConsultaClientes.cs  SQL das tools de clientes; só aqui se traduz Campo -> coluna SQL
│   │   │       └── ConsultaVendas.cs    SQL da tool de vendas; só aqui se traduz DimensaoVenda -> coluna SQL
│   │   └── sql/                     Scripts das views (schema mcp) e permissões, a correr por ordem (00–06)
│   └── Vendas.Agente/               Host local: Ollama (qwen3:4b-instruct) + servidor MCP, em consola ou browser
│       ├── Program.cs               Lança o servidor, liga o modelo às tools, modos consola, menu e web
│       ├── PoliticasSeguranca.cs    Regras de segurança (prompt de sistema), validação de argumentos e respostas
│       ├── PaginaChat.cs            Lê a página do chat embebida no executável
│       └── Pagina/
│           └── pagina-chat.html     Página do modo web (chat no browser), embebida como recurso no build
└── tests/
    ├── Vendas.Servidor.Tests/       Testes das tools (repo falso), do SQL gerado, períodos e integração com a BD
    └── Vendas.Agente.Tests/         Testes das políticas de segurança
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

3. **Tools.** `consultar` e `consultar_sensivel` devolvem CSV (ponto decimal, valores com vírgula entre aspas); `consultar_vendas` devolve texto pronto a mostrar, com números em formato pt-PT (`38 087,07`). Todas dizem quantas linhas vieram e quantas existem (`# Mostrados N de M` / `Mostro N de M`). Erros de SQL nunca chegam ao modelo: a tool devolve "Não foi possivel consultar os dados neste momento" e o detalhe fica no log (`stderr`). O modelo nunca escreve SQL: escolhe valores de enums que o servidor traduz para colunas (lista branca) e os textos vão sempre como parâmetros.

   | Tool | Views | Para quê |
   | --- | --- | --- |
   | `consultar` | `ViewMCP_cliente` + `ViewMCP_cliente_faturacao` (juntas por `ClienteID`) | Listar ou contar clientes pela ficha (`Zona`, `Localidade`, `Vendedor`, `TipoCliente`, `Actividade`, `Distrito`) ou condições de faturação (`Pagamento`, `Cobranca`, `Expedicao`, `SituacaoFinanceira`, `EscalaoPlafond`, `EscalaoVolumeVendas`). |
   | `consultar_sensivel` | + `ViewMCP_cliente_sensivel` | NIF, email, telefone, morada, código postal, volume de vendas declarado e plafond. Máx. 20 linhas e 5 páginas; cada acesso fica registado no log (auditoria, sem os valores). |
   | `consultar_vendas` | `ViewMCP_vendas` (+ `ViewMCP_cliente` para o nome) | Faturação real num período: valor líquido sem IVA, notas de crédito descontadas. |

   **`consultar`** — `coluna` (mostrar/filtrar/contar), `valor` (filtro), `contar=true` (nº de clientes por valor, com percentagens), `cruzarCom`/`valorCruzado` (filtra também por outra coluna, de qualquer das views — ex.: contar por `Zona` só dos clientes com `Pagamento` = "30 dias"), `nomeComecaPor`, `limite` (1–100, por omissão 10) e `pagina`. Os filtros ignoram acentos e maiúsculas; quando um valor não existe, a tool devolve logo os valores existentes (até 30).

   **`consultar_sensivel`** — `dado` (obrigatório), `coluna`/`valor` (filtro), `maiores=true` (top N por `VolumeVendas` ou `Plafond`), `mostrarTambem` (outra coluna na mesma linha), `nomeComecaPor`, `limite`, `pagina`. Pode ser desligada sem recompilar com `VENDAS_SENSIVEL=0`: sai da lista de tools e o modelo deixa de a ver.

   **`consultar_vendas`** — ranking de faturação (valor líquido sem IVA, notas de crédito descontadas):
   - `agrupar`: `Cliente` (melhores clientes, com o ID junto ao nome), `Artigo`, `Familia`, `Marca` (o que mais se vende), `Mes` (atividade mês a mês, do mais recente para o mais antigo) ou, pela ficha do cliente, `Localidade`, `Distrito`, `Zona`, `Vendedor`, `Actividade` (onde/quem vende mais);
   - período, por esta prioridade:
     - `dataInicio`/`dataFim` (`dd-MM-aaaa`, data final incluída) — semana, trimestre, semestre;
     - `mes` (1–12) e/ou `ano` (2000–ano atual) — mês sem ano = a ocorrência mais recente; ano sem mês = ano inteiro;
     - `periodo`: `Ultimos7Dias`, `Ultimos30Dias`, `Ultimos90Dias`, `Ultimos12Meses`, `AnoAtual` (por omissão), `AnoAnterior`, `MesAtual`, `MesAnterior` — o servidor calcula as datas;
   - `cliente` (opcional): ID ou início do nome; limita tudo às vendas desse cliente (ex.: `agrupar=Artigo, cliente=1003` = o que esse cliente compra). Se o nome servir a vários, devolve até 5 com o ID;
   - `coluna`/`valor` (opcional): só clientes com esse valor na ficha (ex.: `agrupar=Cliente, coluna=Localidade, valor=Seixal`);
   - `ordenar`: `Valor` (por omissão) ou `Quantidade` (unidades; não se aplica a `agrupar=Cliente`);
   - `semVendas=true`: clientes **sem** compras no período, com a data da última compra (não se combina com `cliente`);
   - `limite` (1–100).

   A resposta traz um título com período, datas, total e nº de documentos, o valor médio por documento, a lista numerada (valor e % do total, ou unidades) e o rodapé `Mostro N de M`. Avisa (`Atenção: …`) quando a última venda do período é mais de 7 dias antes do fim, ou quando o período vem vazio (diz até onde vão os dados).

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

O login SQL (`mcpserver`) deve ter só `SELECT` sobre as views do schema `mcp`. Os scripts em `src/Vendas.Servidor/sql/` criam as views e dão as permissões, por ordem: `01`–`02` views de clientes, `03` permissões, `04` verificação, `05` view sensível (com `GRANT` próprio, para poder ser retirado à parte) e `06` view de vendas.

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
- Numa conversa nova, perguntar em linguagem natural, ex.: *"Quantos clientes temos por distrito?"*, *"Lista os clientes da zona Lisboa"*, *"Quais os 5 melhores clientes este ano?"*, *"Que artigos compra o cliente 1003?"*. O modelo escolhe a tool; abrir o bloco da chamada mostra os parâmetros e o CSV devolvido.
- As regras de `PoliticasSeguranca.cs` **não** se aplicam no Claude Desktop. Para as usar, criar um Project e colar o texto nas instruções do projeto.
- Logs: `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\Claude\logs\` (`main.log` e `mcp-server-vendas.log`).

## Usar com o Claude Code (terminal)

Alternativa à app, sem interface gráfica. Registar o servidor uma vez (âmbito `user`, fica em `~/.claude.json` e disponível em qualquer pasta):

```powershell
claude mcp add vendas --scope user -e "VENDAS_SQL=Server=tcp:<servidor>,<porta>;Database=<bd>;User Id=<login>;Encrypt=True;TrustServerCertificate=True" -- dotnet run --project "C:\...\projeto_Mcp\src\Vendas.Servidor" --no-build
claude mcp get vendas      # deve mostrar "Connected"
```

- Abrir uma sessão **nova** com `claude` (as tools só aparecem em sessões novas) e perguntar em linguagem natural. Na primeira chamada de cada tool pede autorização (`mcp__vendas__consultar`, `mcp__vendas__consultar_vendas`, `mcp__vendas__consultar_sensivel`).
- Dentro da sessão, `/mcp` mostra o estado do servidor e as tools.
- Remover: `claude mcp remove vendas -s user`.

## Onde pode correr

Qualquer host (Claude Desktop, Claude Code, Vendas.Agente, Inspector) tem de correr **neste PC, na sessão Windows do utilizador**:

- o transporte é **stdio** — o host lança o servidor como processo filho na mesma máquina;
- o `sql.pwd` está protegido com DPAPI da conta Windows — só um processo desse utilizador, nesse PC, o desencripta;
- o SQL Server está na rede interna.

Para usar a partir de outro PC ou do telemóvel seria preciso mudar para transporte HTTP (`ModelContextProtocol.AspNetCore`), alojar o servidor como serviço com autenticação própria e, para um conector do claude.ai, expô-lo à internet — decisão de IT/segurança, fora do âmbito atual.

## Usar com o Vendas.Agente (Ollama)

Host de consola local, sem cloud. Precisa do Ollama a correr em `http://localhost:11434` com o modelo `qwen3:4b-instruct` (`ollama pull qwen3:4b-instruct`; outro modelo: variável `OLLAMA_MODELO`). O agente lança a dll compilada do servidor (`Vendas.Servidor.dll`; um `ProjectReference` garante que é compilada com o agente, por isso basta o comando abaixo) e passa-lhe o `VENDAS_SQL` do seu próprio ambiente; a password vem do `sql.pwd`.

```powershell
dotnet run --project src/Vendas.Agente
```

Mostra as tools disponíveis e pergunta o modo:

1. **Perguntas livres** — lê perguntas do teclado (Enter vazio termina) e o modelo escolhe a tool. Cada chamada mostra a tool, os argumentos, o tempo e o início do resultado (`[consultar_vendas(agrupar=Cliente, …) 120 ms] …`; em `consultar_sensivel` só o tamanho, para os dados pessoais não ficarem no ecrã). No fim: tempo até ao 1.º token, total e tokens lidos.
2. **Menu fixo** — sem modelo: escolhas numeradas que chamam a tool `consultar` diretamente.
3. **Chat no browser** — o mesmo fluxo do modo 1 numa página local em `http://localhost:5080` (abre sozinha). Também arranca direto com `dotnet run --project src/Vendas.Agente -- --web`. A resposta aparece à medida que é gerada; as chamadas às tools e os erros técnicos só aparecem na consola. Só aceita pedidos de `localhost` (POST de outra origem → 403), perguntas de 1 a 500 caracteres e um pedido de cada vez.

- Modelo por omissão `qwen3:4b-instruct` (o 7b é pesado demais para CPU); `OLLAMA_MODELO` muda-o. Fica carregado em memória (`keep_alive = -1m`), `Temperature = 0` e janela fixa `NumCtx = 8192` — um prompt maior faria o Ollama cortar o início, que são as regras de segurança.
- O modelo é aquecido em paralelo com a escolha do modo.
- Sem histórico: cada pergunta é só regras + pergunta (mais rápido em CPU; perde "mostra mais").
- Os argumentos do modelo são validados contra o esquema da tool antes da chamada: um valor fora do enum (ex.: `agrupar=Localidad`) volta logo ao modelo com os valores possíveis, sem ir ao servidor.
- Uma resposta com números sem nenhuma chamada a tool é considerada inventada: é descartada e o modelo tenta uma vez mais. Máx. 5 chamadas a tools e 12 000 caracteres de resultados por pergunta.
- As regras do prompt (`PoliticasSeguranca.cs`) mandam recusar perguntas fora dos dados de clientes e faturação.

## Testar com o MCP Inspector

Criar um `inspector.json` local (está no `.gitignore`) com o mesmo bloco `mcpServers` acima, mas `"args": ["run", "--project", "src\\Vendas.Servidor"]`, e:

```powershell
npx @modelcontextprotocol/inspector --config inspector.json --server vendas
```

No Inspector: "List Tools" e invocar cada tool para validar a resposta.

> Nota: como a ligação é por stdio, cada sessão do cliente corresponde a um processo do servidor. Se alterares o código, é preciso recompilar **e** reiniciar a ligação no cliente.

## Testes

Os mesmos passos da CI (`.github/workflows/build.yml`), a correr antes de cada commit:

```powershell
dotnet restore Vendas.slnx
dotnet build Vendas.slnx --no-restore --configuration Release --warnaserror
dotnet test Vendas.slnx --no-restore --configuration Release --filter "Categoria!=Integração"
```

Os testes unitários usam um repositório falso (`FakeVendasRepo`) e verificam o SQL gerado sem ir à base de dados. Os de integração (`RepoSqlIntegracaoTests`) falam com o SQL Server e só correm com a variável `VENDAS_SQL_TESTES` definida (sem ela aparecem como saltados):

```powershell
dotnet test Vendas.slnx --filter "Categoria=Integração"
```

## Resolução de problemas

| Sintoma | Causa provável |
| --- | --- |
| O conector não aparece no Claude Desktop | Config editado no ficheiro errado (`%APPDATA%` em vez do da pasta `Packages`), ou a app estava aberta e regravou o ficheiro. `main.log` com `no stdio servers connected` confirma. |
| Tool devolve "Não foi possivel consultar os dados neste momento" | Ver `mcp-server-vendas.log`. `Login failed ... 18456` → password errada/ausente: recriar o `sql.pwd`. |
| Servidor não arranca: `VENDAS_SQL não definida` | Falta o `env` no config do host, ou o terminal foi aberto antes do `setx`. |
| O agente diz "O Ollama não respondeu ou devolveu um erro" com `model '…' not found` | O modelo não está instalado. `ollama list` mostra os que existem; `ollama pull qwen3:4b-instruct` ou definir `OLLAMA_MODELO` com um deles. |
| O agente não liga ao Ollama (`Connection refused`) | O Ollama não está a correr: abrir a app Ollama ou `ollama serve`. Confirmar em `http://localhost:11434/api/tags`. |
| `consultar_sensivel` não aparece na lista de tools | `VENDAS_SENSIVEL=0` está definida no ambiente do servidor. |
| O agente diz `Não encontrei o servidor compilado` | O servidor ainda não foi compilado na mesma configuração do agente. `dotnet build Vendas.slnx` (ou `dotnet run --project src/Vendas.Agente`, que compila os dois). |
| O chat web não abre (`HttpListenerException`) | A porta 5080 está ocupada por outra instância do agente: fechá-la e arrancar de novo. |
| `dotnet build` falha com `MSB3027`/`MSB3021` ("... is locked by") | Há processos `Vendas.Servidor.exe` antigos (Inspector, Claude Desktop). Terminá-los e recompilar (os PIDs vêm na mensagem de erro). |

```powershell
Stop-Process -Id <PID1>,<PID2> -Force
Get-Process -Name Vendas.Servidor -ErrorAction SilentlyContinue   # confirma que não sobrou nenhum
dotnet build src/Vendas.Servidor
```

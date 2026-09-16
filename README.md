# Vendas.Servidor — Servidor MCP de Vendas

Servidor [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) escrito em C# (.NET 10), que expõe ferramentas ("tools") para consultar dados de vendas e clientes a um cliente/agente de IA (ex.: Claude Desktop, VS Code, MCP Inspector).

## O que é o MCP, neste projeto

O MCP é um protocolo baseado em JSON-RPC 2.0 que permite a um cliente de IA descobrir e invocar "ferramentas" expostas por um servidor. Este servidor comunica por **stdio** (entrada/saída standard do processo) — não abre nenhuma porta de rede. O cliente lança o executável, troca mensagens JSON-RPC linha a linha, e pode chamar as tools registadas.

## Estrutura do projeto

```
projeto_Mcp/
├── Vendas.slnx                     Solution do .NET
├── inspector.json                  Config local do MCP Inspector (gitignored, tem a connection string)
└── src/Vendas.Servidor/
    ├── Program.cs                  Ponto de entrada: configura o host, DI e o servidor MCP
    ├── Vendas.Servidor.csproj      Dependências do projeto
    ├── Ferramentas/                Classes com as tools MCP expostas
    │   ├── ClientesTools.cs         Tools clientes_nomes e clientes_por
    │   └── VendasTools.cs           Tools de vendas (atualmente desativadas, ver abaixo)
    └── Modelos/                    Modelos de dados e acesso a dados
        ├── Vendas.cs                (record Venda)
        ├── VendasPorCliente.cs      (record VendaPorCliente)
        ├── VendasPorProduto.cs      (record VendaPorProduto)
        ├── ClientesInativos.cs      (record ClienteInativo)
        ├── ContagemCliente.cs       (record ContagemCliente, enum DimensaoCliente, Dimensoes)
        └── Dados/
            ├── IVendasRepo.cs       Contrato do repositório
            ├── RepoMemoria.cs       Implementação em memória (dados fictícios)
            └── RepoSql.cs           Implementação em SQL Server
```

## Como funciona

1. **`Program.cs`** cria um `Host` genérico do .NET (`Host.CreateApplicationBuilder`), configura logging para a consola de erro (`stderr`, obrigatório — o `stdout` fica reservado para o protocolo MCP) e liga o servidor MCP:
   ```csharp
   builder.Services
       .AddMcpServer()
       .WithStdioServerTransport()
       .WithToolsFromAssembly();
   ```
   `WithToolsFromAssembly()` percorre o assembly à procura de classes marcadas com `[McpServerToolType]` e regista automaticamente os métodos marcados com `[McpServerTool]` como tools MCP — não é preciso registar cada tool manualmente.

2. **Qual repositório é usado (`IVendasRepo`) depende da variável de ambiente `VENDAS_SQL`**, lida uma única vez no arranque do processo:
   ```csharp
   var cs = Environment.GetEnvironmentVariable("VENDAS_SQL");
   if (string.IsNullOrWhiteSpace(cs))
       builder.Services.AddSingleton<IVendasRepo, RepoMemoria>();   // sem VENDAS_SQL
   else
       builder.Services.AddSingleton<IVendasRepo>(_ => new RepoSql(cs)); // com VENDAS_SQL
   ```
   Isto é decidido **apenas no arranque** — não há reconexão nem verificação em tempo real. Se o processo for lançado sem `VENDAS_SQL` no ambiente (ex.: `dotnet run` direto, ou um cliente/config diferente do que define a variável), o servidor cai sempre para `RepoMemoria`, mesmo que já tenha corrido com SQL Server antes. Confirma no stderr qual foi usado:
   ```
   [Vendas.Servidor] VENDAS_SQL não definida — a usar RepoMemoria (dados em memória).
   [Vendas.Servidor] VENDAS_SQL definida — a usar RepoSql (SQL Server).
   ```

3. **As classes em `Ferramentas/`** definem as tools. Atualmente só `ClientesTools` está ativa (marcada com `[McpServerToolType]`):
   - `ClientesTools.NomesAsync()` — tool `clientes_nomes`, devolve os nomes de todos os clientes com vendas, em ordem alfabética. **Só implementado em `RepoMemoria`** — em `RepoSql` lança `NotImplementedException` (ainda não há view/query para isto no SQL Server).
   - `ClientesTools.ClientesPorAsync(agrupar, limite)` — tool `clientes_por`, devolve CSV com a contagem de clientes agrupados por `DimensaoCliente` (`Zona`, `Vendendor`, `TipoCliente`, `Actividade`, `Distrito`). **Só implementado em `RepoSql`** (consulta a view `[dbo].[ViewMCP_cliente]`) — em `RepoMemoria` devolve sempre lista vazia.
   - `ClientesTools.InativosAsync(dias)` — tool `clientes_inativos`, está comentada no código (por implementar/ativar).

   `VendasTools` existe mas não tem `[McpServerToolType]` nem nenhum método ativo — todos os métodos (`vendas_top_clientes`, `vendas_top_produtos`) estão comentados, por isso não aparecem como tools no cliente MCP neste momento.

   > Nota: como `clientes_nomes` só funciona em memória e `clientes_por` só funciona em SQL Server, o modo de arranque (com ou sem `VENDAS_SQL`) determina qual das duas tools responde com dados e qual devolve erro/lista vazia.

4. **`Modelos/Dados/IVendasRepo.cs`** define o contrato de acesso a dados (`ObterTopClientesAsync`, `ObterTopProdutosAsync`, `ObterInativosAsync`, `ObterNomesClientesAsync`, `ContarClientesAsync`). Há duas implementações:
   - **`RepoMemoria`** — mantém uma lista de `Venda` gerada em memória (dados fictícios, ~24 meses), perdida quando o processo termina.
   - **`RepoSql`** — liga a um SQL Server real via `Microsoft.Data.SqlClient`, usando a connection string recebida em `VENDAS_SQL`. Só `ContarClientesAsync` está implementado; os restantes métodos lançam `NotImplementedException` até existirem views equivalentes no SQL Server.

## Modelos de dados

- **`Venda`** — uma venda individual.
- **`VendaPorCliente`** / **`VendaPorProduto`** — agregações de vendas por cliente/produto.
- **`ClienteInativo`** — cliente e há quantos dias não compra.
- **`ContagemCliente`** — contagem de clientes por dimensão (`Valor`, `Clientes`, `Total`, `Grupos`), devolvida pela tool `clientes_por`.

## Como correr e testar

Requisitos: .NET SDK 10, e Node.js (`npx`) se quiseres usar o MCP Inspector.

```powershell
# Compilar
dotnet build Vendas.slnx
```

### Modo memória (sem SQL Server)

Não é preciso nenhuma variável de ambiente — é o modo por omissão.

```powershell
dotnet run --project src/Vendas.Servidor

# ou com o MCP Inspector
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
$env:PORT=6280; npx @modelcontextprotocol/inspector "dotnet run --project src/Vendas.Servidor --no-build"
```

Neste modo, `clientes_nomes` devolve dados; `clientes_por` devolve sempre "Não há clientes...".

### Modo SQL Server

É preciso a variável `VENDAS_SQL` **no mesmo processo que lança o servidor**. A forma mais simples é usar o `inspector.json` (ficheiro local, no `.gitignore` — não é partilhado nem commitado porque tem a connection string):

```json
{
  "mcpServers": {
    "vendas": {
      "command": "dotnet",
      "args": ["run", "--project", "src\\Vendas.Servidor"],
      "env": { "VENDAS_SQL": "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;" }
    }
  }
}
```

```powershell
# Testar com o MCP Inspector usando este config
npx @modelcontextprotocol/inspector --config inspector.json --server vendas
```

Se preferires não depender do `inspector.json` em cada arranque (ex.: outro cliente MCP que não o lê), define `VENDAS_SQL` de forma persistente para o teu utilizador Windows:

```powershell
setx VENDAS_SQL "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;"
```
(depois de `setx`, é preciso abrir um terminal/processo novo para a variável ficar disponível.)

Neste modo, `clientes_por` devolve dados reais da view `[dbo].[ViewMCP_cliente]`; `clientes_nomes` devolve erro (`NotImplementedException`).

No Inspector: liga ao servidor, usa "List Tools" para ver as tools disponíveis (atualmente `clientes_nomes` e `clientes_por`), e invoca cada uma para validar a resposta.

> Nota: como a ligação é por stdio, cada sessão do cliente corresponde a um processo do servidor. Se alterares o código, é preciso recompilar **e** reiniciar a ligação no cliente (o processo antigo continua a correr com o código anterior até ser terminado).

> Nota: se o `dotnet build` falhar com `MSB3027`/`MSB3021` ("Could not copy ... Vendas.Servidor.exe ... is locked by"), é porque ainda há um ou mais processos `Vendas.Servidor.exe` de uma sessão anterior a correr (ex.: ligações antigas do MCP Inspector/cliente) e a bloquear o executável. Termina-os e volta a compilar:
> ```powershell
> Stop-Process -Id <PID1>,<PID2> -Force
> Get-Process -Name Vendas.Servidor -ErrorAction SilentlyContinue   # confirma que não sobrou nenhum
> dotnet build src/Vendas.Servidor
> ```
> Os PIDs aparecem na própria mensagem de erro do build ("The file is locked by: ...").

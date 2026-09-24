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
            └── RepoSql.cs           Implementação em SQL Server (única — sem fallback em memória)
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

2. **`IVendasRepo` liga sempre ao SQL Server via `RepoSql`**, usando a connection string da variável de ambiente `VENDAS_SQL`, lida uma única vez no arranque do processo:
   ```csharp
   var ligacao = Environment.GetEnvironmentVariable("VENDAS_SQL")
       ?? throw new InvalidOperationException(
           "Vendas_Sql não definida. Defina a ligação ao SQL antes de arrancar o servidor");
   builder.Services.AddSingleton<IVendasRepo>(_ => new RepoSql(ligacao));
   ```
   **Não há fallback em memória.** Se o processo for lançado sem `VENDAS_SQL` no ambiente, o servidor lança `InvalidOperationException` e não chega a arrancar o host MCP.

3. **`Ferramentas/VistasTools.cs`** define as tools ativas (marcada com `[McpServerToolType]`), uma por view SQL:
   - `ClientesAsync` — tool `clientes_consultar`, dados gerais dos clientes (`NomeCliente`, `Zona`, `Vendedor`, `TipoCliente`, `Actividade`, `Distrito`).
   - `FaturacaoAsync` — tool `faturacao_consultar`, condições de faturação (`NomeCliente`, `Pagamento`, `Cobranca`, `Expedicao`, `SituacaoFinanceira`, `EscalaoPlafond`, `EscalaoVolumeVendas`).

   Ambas aceitam uma coluna opcional a mostrar/filtrar/contar, um `valor` exato para filtrar, `contar=true` para agrupar com percentagens, e `limite` (1–100, por omissão 50). Devolvem CSV; erros de SQL nunca são expostos ao modelo — a tool devolve sempre uma mensagem genérica e regista o detalhe no log.

4. **`Modelos/Dados/IVendasRepo.cs`** define o contrato de acesso a dados: `ContarAsync` (contagem de clientes agrupados por `Campo`, com filtros) e `ListarAsync` (linhas de clientes com as colunas pedidas). A única implementação é **`RepoSql`**, que consulta `[dbo].[ViewMCP_cliente]` e `[dbo].[ViewMCP_cliente_faturacao]` via `Microsoft.Data.SqlClient`, usando a connection string recebida em `VENDAS_SQL`.

## Modelos de dados

- **`ContagemCliente`** — contagem de clientes por valor de um `Campo` (`Valor`, `Clientes`, `Total`, `Grupos`), devolvida quando `clientes_consultar`/`faturacao_consultar` são chamadas com `contar=true`.
- **`PaginaClientes`** — linhas de clientes devolvidas por `ListarAsync`, com o total de resultados (antes do `limite`).

## Como correr e testar

Requisitos: .NET SDK 10, e Node.js (`npx`) se quiseres usar o MCP Inspector.

```powershell
# Compilar
dotnet build Vendas.slnx
```

### Arrancar o servidor

É preciso a variável `VENDAS_SQL` **no mesmo processo que lança o servidor** — não há modo sem SQL Server; sem ela, o servidor lança `InvalidOperationException` no arranque e não chega a expor as tools. A forma mais simples é usar o `inspector.json` (ficheiro local, no `.gitignore` — não é partilhado nem commitado porque tem a connection string):

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

No Inspector: liga ao servidor, usa "List Tools" para ver as tools disponíveis (atualmente `clientes_consultar` e `faturacao_consultar`), e invoca cada uma para validar a resposta.

> Nota: como a ligação é por stdio, cada sessão do cliente corresponde a um processo do servidor. Se alterares o código, é preciso recompilar **e** reiniciar a ligação no cliente (o processo antigo continua a correr com o código anterior até ser terminado).

> Nota: se o `dotnet build` falhar com `MSB3027`/`MSB3021` ("Could not copy ... Vendas.Servidor.exe ... is locked by"), é porque ainda há um ou mais processos `Vendas.Servidor.exe` de uma sessão anterior a correr (ex.: ligações antigas do MCP Inspector/cliente) e a bloquear o executável. Termina-os e volta a compilar:
> ```powershell
> Stop-Process -Id <PID1>,<PID2> -Force
> Get-Process -Name Vendas.Servidor -ErrorAction SilentlyContinue   # confirma que não sobrou nenhum
> dotnet build src/Vendas.Servidor
> ```
> Os PIDs aparecem na própria mensagem de erro do build ("The file is locked by: ...").

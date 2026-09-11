# Vendas.Servidor — Servidor MCP de Vendas

Servidor [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) escrito em C# (.NET 10), que expõe ferramentas ("tools") para consultar dados de vendas e clientes a um cliente/agente de IA (ex.: Claude Desktop, VS Code, MCP Inspector).

## O que é o MCP, neste projeto

O MCP é um protocolo baseado em JSON-RPC 2.0 que permite a um cliente de IA descobrir e invocar "ferramentas" expostas por um servidor. Este servidor comunica por **stdio** (entrada/saída standard do processo) — não abre nenhuma porta de rede. O cliente lança o executável, troca mensagens JSON-RPC linha a linha, e pode chamar as tools registadas.

## Estrutura do projeto

```
projeto_Mcp/
├── Vendas.slnx                     Solution do .NET
└── src/Vendas.Servidor/
    ├── Program.cs                  Ponto de entrada: configura o host, DI e o servidor MCP
    ├── Vendas.Servidor.csproj      Dependências do projeto
    ├── Ferramentas/                Classes com as tools MCP expostas
    │   ├── SaudacaoTools.cs
    │   ├── VendasTools.cs
    │   └── ClientesTools.cs
    └── Modelos/                    Modelos de dados e acesso a dados
        ├── Vendas.cs                (record Venda)
        ├── VendasPorCliente.cs      (record VendaPorCliente)
        └── Dados/
            ├── IVendasRepo.cs       Contrato do repositório
            └── RepoMemoria.cs       Implementação em memória (usada atualmente)
```

## Como funciona

1. **`Program.cs`** cria um `Host` genérico do .NET (`Host.CreateApplicationBuilder`), configura logging para a consola de erro (`stderr`, obrigatório — o `stdout` fica reservado para o protocolo MCP), regista `IVendasRepo → RepoMemoria` no contentor de DI e liga o servidor MCP:
   ```csharp
   builder.Services.AddSingleton<IVendasRepo, RepoMemoria>();

   builder.Services
       .AddMcpServer()
       .WithStdioServerTransport()
       .WithToolsFromAssembly();
   ```
   `WithToolsFromAssembly()` percorre o assembly à procura de classes marcadas com `[McpServerToolType]` e regista automaticamente os métodos marcados com `[McpServerTool]` como tools MCP — não é preciso registar cada tool manualmente.

2. **As classes em `Ferramentas/`** definem as tools. Cada método público anotado vira uma tool disponível para o cliente, com nome, descrição e schema de parâmetros gerados a partir dos atributos `[McpServerTool]`/`[Description]` e da assinatura do método:
   - `SaudacaoTools.Saudacao(nome)` — tool `Saudacao`, sem dependências, devolve uma saudação simples. Serve para confirmar que a ligação ao servidor está a funcionar.
   - `ClientesTools.ListarClientesAsync(filtro?)` — tool `listar-clientes`, devolve as vendas agregadas por cliente (nome, nº de vendas, total vendido), com filtro opcional por nome.
   - `VendasTools.ListarVendasAsync(filtro?)` — tool `listar-vendas-detalhadas`, devolve a lista de vendas individuais (produto, quantidade, valor, data), com filtro opcional por cliente.

   > Anteriormente `VendasTools` e `ClientesTools` tinham o mesmo método duplicado (ambos chamavam `ObterTopClientesAsync`), pelo que as duas tools deviam sempre resultados iguais. `VendasTools` foi corrigido para usar `ListarVendasAsync` e devolver dados diferentes (detalhe em vez de agregado).

   Ambas as tools de vendas/clientes recebem `IVendasRepo` por injeção de dependência no construtor — é por isso que o registo no `Program.cs` é essencial; sem ele, o servidor MCP falha ao tentar ativar estas classes quando uma tool é chamada.

3. **`Modelos/Dados/IVendasRepo.cs`** define o contrato de acesso a dados: `ObterTopClientesAsync` (agregação por cliente) e `ListarVendasAsync` (lista de vendas individuais). `RepoMemoria` é a única implementação, registada no DI: mantém uma lista de `Venda` em memória (10 registos de exemplo) e serve ambos os métodos. A antiga implementação `VendasRepo` (com apenas 3 registos) foi removida por ser uma duplicação não utilizada.

4. **Persistência**: apesar de o projeto referenciar `Dapper` e `Microsoft.Data.SqlClient` no `.csproj`, não há nenhuma ligação a base de dados implementada — toda a informação é mantida em memória e perde-se quando o processo termina. Estas dependências existem para trabalho futuro (ligação a SQL Server).

## Modelos de dados

- **`Venda`** — `(int Id, string Cliente, string Produto, decimal Valor, int Quantidade, DateTime DataVenda)`: uma venda individual.
- **`VendaPorCliente`** — `(string Cliente, int NumeroVendas, decimal TotalVendido)`: agregação de vendas por cliente, devolvida pelas tools.

## Como correr e testar

Requisitos: .NET SDK 10, e Node.js (`npx`) se quiseres usar o MCP Inspector.

```powershell
# Compilar
dotnet build Vendas.slnx

# Correr o servidor diretamente (fica à espera de mensagens MCP via stdio)
dotnet run --project src/Vendas.Servidor

# Testar com o MCP Inspector (interface web para listar e invocar tools)
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
$env:PORT=6280; npx @modelcontextprotocol/inspector "dotnet run --project src/Vendas.Servidor --no-build"
```

No Inspector: liga ao servidor, usa "List Tools" para ver `Saudacao`, `listar-clientes` e `listar-vendas-detalhadas`, e invoca cada uma para validar a resposta.

> Nota: como a ligação é por stdio, cada sessão do cliente corresponde a um processo do servidor. Se alterares o código, é preciso recompilar **e** reiniciar a ligação no cliente (o processo antigo continua a correr com o código anterior até ser terminado).

> Nota: se o `dotnet build` falhar com `MSB3027`/`MSB3021` ("Could not copy ... Vendas.Servidor.exe ... is locked by"), é porque ainda há um ou mais processos `Vendas.Servidor.exe` de uma sessão anterior a correr (ex.: ligações antigas do MCP Inspector/cliente) e a bloquear o executável. Termina-os e volta a compilar:
> ```powershell
> Stop-Process -Id <PID1>,<PID2> -Force
> Get-Process -Name Vendas.Servidor -ErrorAction SilentlyContinue   # confirma que não sobrou nenhum
> dotnet build src/Vendas.Servidor
> ```
> Os PIDs aparecem na própria mensagem de erro do build ("The file is locked by: ...").

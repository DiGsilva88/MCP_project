# mcp-vendas

Servidor MCP em C# que responde a perguntas de negócio sobre vendas, lê de uma
vista de SQL Server com um utilizador só de leitura.

Construído por níveis — ver `guia-mcp-vendas.docx`. Cada nível funciona por si.

## Estrutura

```
mcp-vendas/
├── sql/                         # vista, utilizador, permissões (correr 1x, como db_owner)
├── src/Vendas.Servidor/         # o servidor MCP (stdio)
├── src/Vendas.Agente/           # agente de consola: cliente MCP + LLM
└── tests/Vendas.Testes/         # xUnit, sem BD e sem rede
```

## Pré-requisitos

| | |
|---|---|
| .NET SDK 10 | `dotnet --version` |
| Node ≥ 22.19 | só para o MCP Inspector — `node --version` |
| SQL Server | a partir do Nível 3 |
| `ANTHROPIC_API_KEY` | só para o agente (Nível 4) |

## Arrancar

```bash
dotnet build
```

### Configurar a ligação à base de dados

A password nunca entra no repositório. Em desenvolvimento:

```bash
dotnet user-secrets set "ConnectionStrings:Vendas" "Server=localhost;Database=Vendas;User Id=mcp_leitor;Password=***;TrustServerCertificate=True;Encrypt=True" --project src/Vendas.Servidor
```

Quando o servidor é lançado por outro programa (Claude Desktop, Inspector), os user
secrets **não são lidos** — esse processo corre como `Production`. Nesse caso passa-se
a variável de ambiente `ConnectionStrings__Vendas` (dois underscores; o .NET converte
para `:`).

### Testar o servidor sozinho

Sempre pelo executável compilado, nunca por `dotnet run` — os flags do `dotnet`
entram em conflito com os do Inspector:

```bash
# browser (abrir o URL COM token que o terminal imprime)
npx @modelcontextprotocol/inspector src/Vendas.Servidor/bin/Debug/net10.0/Vendas.Servidor.exe

# linha de comandos: flags do Inspector ANTES do caminho
npx @modelcontextprotocol/inspector --cli --method tools/list \
    src/Vendas.Servidor/bin/Debug/net10.0/Vendas.Servidor.exe
```

### O agente

```bash
export ANTHROPIC_API_KEY="sk-ant-..."      # PowerShell: $env:ANTHROPIC_API_KEY="..."
dotnet run --project src/Vendas.Agente
```

### Testes

```bash
dotnet test
```

Não precisam de base de dados nem de rede — usam um repositório falso.

## Ferramentas expostas

| Ferramenta | Responde a | Argumentos |
|---|---|---|
| `saudacao` | «o servidor está vivo?» | `nome` |
| `vendas_top_clientes` | «quem são os maiores clientes?» | `limite` (1–50, def. 10) |
| `clientes_inativos` | «que clientes deixaram de comprar?» | `dias` (1–3650, def. 90), `limite` (1–50, def. 20) |
| `vendas_resumo` | métrica × dimensão × período | chaves de `config/metricas.json` |
| `vendas_chaves` | as chaves válidas da anterior | — |

## Segurança — as cinco barreiras

Por ordem de **quanto dependem de o código estar certo**:

1. **Colunas fora da vista.** `NIF`, `Email`, `Telefone`, `CustoInterno`, `Margem` não
   existem em `vwVendas`. Não depende de código nenhum.
2. **`mcp_leitor`** sem permissões por omissão + `db_denydatawriter` + **um único**
   `GRANT SELECT` na vista. Nunca `DENY SELECT ON SCHEMA::dbo` — o `DENY` vence o
   `GRANT` e tira o acesso à própria vista.
3. **Ferramentas estreitas**, ao nível da pergunta de negócio. Nunca `executar_sql(string)`.
   Consultas parametrizadas; agregação em SQL.
4. **`Math.Clamp` em todos os argumentos numéricos.** O valor do modelo é sugestão; o
   travão é do servidor.
5. **Saneamento de erros.** O detalhe vai para `stderr`; para o modelo vai uma frase
   neutra. Nada com `Password=`, `User Id=` ou `Server=` sai para o contexto.

Mais: verificação de arranque que confronta as colunas reais da vista com uma lista de
termos proibidos — se a vista expuser um NIF, o servidor não arranca. E os dados que
voltam da base de dados são tratados como **entrada não confiável** (injeção de prompt
indireta).

## Configuração sem recompilar

`src/Vendas.Servidor/config/metricas.json` define métricas, dimensões e períodos. O
modelo escolhe **chaves**, nunca escreve SQL. Alterar o ficheiro não exige recompilar —
exige reiniciar. Precisa de `CopyToOutputDirectory` no `.csproj`, senão o ficheiro nunca
chega ao `bin/`.

## Regras da casa

- Em stdio, `stdout` é o canal do protocolo: **nunca** `Console.WriteLine` no servidor.
  Logs para `stderr` (`LogToStandardErrorThreshold`).
- Nenhum valor vindo do modelo entra no texto de uma consulta SQL — só como parâmetro.
- Ferramenta nova com dependência nova: duas alterações (o construtor **e** o `Program.cs`).
- O `ChatOptions.Tools` do agente monta-se uma vez por sessão; alterá-lo a meio invalida
  o prompt caching e custa mais do que poupa.

## Problemas frequentes

| Sintoma | Causa |
|---|---|
| No Inspector não aparecem separadores | Não se ligou no ecrã **Servers**, ou lançou-se com `dotnet run` |
| `method is required` no `--cli` | Flags do Inspector têm de vir **antes** do caminho do servidor |
| `Falta a connection string 'Vendas'` | User secrets não carregam fora de `Development` → usar `env` |
| `The SELECT permission was denied` | Falta o `GRANT`, ou existe um `DENY` no esquema |
| `metricas.json` «não existe» | Falta `CopyToOutputDirectory` no `.csproj` |
| Ficam processos `dotnet` a correr | Falta `await using` no `McpClient` |

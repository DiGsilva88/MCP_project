-- 07 · VIEW mcp.ViewMCP_resp_aberto_cli (responsabilidades em aberto por cliente). Correr por blocos, depois de 01.
-- A dbo.VGTS_RespAbertoCli é do ERP: NÃO usar ALTER SCHEMA ... TRANSFER, o ERP deixava de a encontrar em dbo.
-- Em vez disso, uma view em mcp lê a original (como a 06 faz com a AnaVendArt).
-- O que a view contém (verificado em 17/11/2026, ver 08b): só clientes (TipoTerceiro C, todos com ficha), sem saldos negativos.
--   Aplicacao VND (área 10): F faturas, V faturas-recibo e N notas de crédito. Aplicacao GTS (área 0): R recibos (3 linhas, por confirmar).
--   RemanMoedaAdop é o saldo por pagar COM IVA (bate com o total do documento nas vendas). As notas de crédito (N) vêm com o saldo POSITIVO:
--   o sinal perdeu-se e a consulta do painel (ConsultaVendas.EmAberto) troca-o, para não as somar às faturas.
USE [IAVSGIX];
GO

-- 1. Onde está a origem e quem é o dono (tem de ser dbo, como o dono do schema mcp, senão a cadeia de propriedade quebra).
SELECT SCHEMA_NAME(o.schema_id) AS schema_nome, o.name AS objeto,
       USER_NAME(COALESCE(o.principal_id, s.principal_id)) AS dono
FROM   sys.objects AS o
JOIN   sys.schemas AS s ON s.schema_id = o.schema_id
WHERE  o.name = 'VGTS_RespAbertoCli';

SELECT s.name AS schema_nome, USER_NAME(s.principal_id) AS dono_schema
FROM   sys.schemas AS s
WHERE  s.name IN ('mcp', 'dbo');

-- 2. Quem usa a origem (confirma que é do ERP e não pode sair de dbo).
SELECT OBJECT_SCHEMA_NAME(d.referencing_id) AS schema_nome, OBJECT_NAME(d.referencing_id) AS usado_por
FROM   sys.sql_expression_dependencies AS d
WHERE  d.referenced_id = OBJECT_ID('dbo.VGTS_RespAbertoCli');

-- 3. Colunas da origem: escolher só as que o MCP precisa e trocar o r.* abaixo por elas.
SELECT name, TYPE_NAME(user_type_id) AS tipo
FROM   sys.columns
WHERE  object_id = OBJECT_ID('dbo.VGTS_RespAbertoCli')
ORDER BY column_id;
GO

-- 4. A view MCP.
-- Só as colunas que o MCP precisa (ver passo 3); o resto da origem fica escondido.
CREATE OR ALTER VIEW mcp.ViewMCP_resp_aberto_cli AS
SELECT r."EntidadeID",r.DataEmissao, r.DataVencimento, r.TipoDocID, r.DocID, r.Ano, r.Aplicacao, r.AreaID,
       r.TipoTerceiro, r.DescricaoDoc, r.Contravalor, r.RemanMoedaAdop
FROM   dbo.VGTS_RespAbertoCli AS r;
GO

-- 5. Mesmo nº de linhas nas duas (2 números iguais).
SELECT (SELECT COUNT(*) FROM mcp.ViewMCP_resp_aberto_cli) AS linhas_mcp,
       (SELECT COUNT(*) FROM dbo.VGTS_RespAbertoCli)       AS linhas_origem;
GO

-- 6. Acesso do mcpserver só à view MCP.
GRANT SELECT ON OBJECT::mcp.ViewMCP_resp_aberto_cli TO mcpserver;
GO

-- 7. Testes como mcpserver. O REVERT tem de correr sempre.
EXECUTE AS USER = 'mcpserver';
SELECT TOP 5 * FROM mcp.ViewMCP_resp_aberto_cli;   -- tem de dar linhas
SELECT TOP 1 * FROM dbo.VGTS_RespAbertoCli;        -- a do ERP continua escondida: erro 229
REVERT;
GO

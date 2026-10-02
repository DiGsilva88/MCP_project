-- 04 · VERIFICAÇÃO (só leitura). Correr depois de 01–03 e 05 para confirmar que tudo ficou bem.
-- Correr por blocos (selecionar e executar), ligado à IAVSGIX.
USE [IAVSGIX];
GO

-- Tempo de CPU/decorrido de cada consulta (separador Mensagens). Serve para decidir se vale a pena
-- otimizar as views: só se uma consulta demorar bem mais de ~200 ms.
SET STATISTICS TIME ON;

-- ── ViewMCP_cliente ─────────────────────────────────────────────────────
-- Uma linha por cliente? (Linhas = Clientes)
SELECT COUNT(*)                    AS Linhas,
       COUNT(DISTINCT ClienteID)   AS Clientes,
       COUNT(DISTINCT NomeCliente) AS Nomes
FROM mcp.ViewMCP_cliente;

SELECT TOP 10 NomeCliente, Zona, Vendedor, TipoCliente, Actividade, Localidade, Distrito
FROM   mcp.ViewMCP_cliente
ORDER BY NomeCliente;

-- Quanto está por preencher
SELECT COUNT(*) AS clientes,
  SUM(CASE WHEN Zona        = '(sem zona)'       THEN 1 ELSE 0 END) AS sem_zona,
  SUM(CASE WHEN Vendedor    = '(sem vendedor)'   THEN 1 ELSE 0 END) AS sem_vendedor,
  SUM(CASE WHEN TipoCliente = '(sem tipo)'       THEN 1 ELSE 0 END) AS sem_tipo,
  SUM(CASE WHEN Actividade  = '(sem actividade)' THEN 1 ELSE 0 END) AS sem_actividade,
  SUM(CASE WHEN Localidade  = '(sem localidade)' THEN 1 ELSE 0 END) AS sem_localidade,
  SUM(CASE WHEN Distrito    = '(sem distrito)'   THEN 1 ELSE 0 END) AS sem_distrito
FROM mcp.ViewMCP_cliente;

SELECT Zona, COUNT(*) AS clientes
FROM   mcp.ViewMCP_cliente
GROUP BY Zona
ORDER BY clientes DESC;

SELECT MAX(LEN(Actividade)) AS len, MAX(DATALENGTH(Actividade)) AS bytes
FROM mcp.ViewMCP_cliente;

-- Nomes com caracteres estranhos (espaços/bytes invisíveis)
SELECT ClienteID, NomeCliente, DATALENGTH(NomeCliente) AS bytes,
       CAST(NomeCliente AS varbinary(200)) AS hex
FROM mcp.ViewMCP_cliente
WHERE NomeCliente LIKE 'ANTONIO MANUEL CASTRO%';

-- ── ViewMCP_cliente_faturacao ───────────────────────────────────────────
-- Mesmo nº de clientes que a view da ficha?
SELECT (SELECT COUNT(*) FROM mcp.ViewMCP_cliente)           AS ficha,
       (SELECT COUNT(*) FROM mcp.ViewMCP_cliente_faturacao) AS faturacao;

-- Valores reais de Pagamento (o filtro do MCP aceita o início: "30 dias" → "30 Dias Fim do Mês")
SELECT Pagamento, COUNT(*) AS clientes
FROM   mcp.ViewMCP_cliente_faturacao
GROUP BY Pagamento
ORDER BY clientes DESC;

-- Cruzamento das duas views, como faz o servidor (ex.: Setúbal + 30 dias)
DECLARE @limite int = 10;
SELECT TOP (@limite) c.NomeCliente, c.Distrito, f.Pagamento, COUNT(*) OVER () AS Total
FROM   mcp.ViewMCP_cliente AS c
JOIN   mcp.ViewMCP_cliente_faturacao AS f ON f.ClienteID = c.ClienteID
WHERE  c.Distrito  COLLATE Latin1_General_CI_AI = 'Setubal'
  AND  f.Pagamento COLLATE Latin1_General_CI_AI LIKE '30 dias %'
ORDER BY c.NomeCliente;

-- ── ViewMCP_cliente_sensivel (05) ───────────────────────────────────────
-- Mesmo nº de clientes que a ficha? Top 3 por volume (valores reais).
SELECT (SELECT COUNT(*) FROM mcp.ViewMCP_cliente)          AS ficha,
       (SELECT COUNT(*) FROM mcp.ViewMCP_cliente_sensivel) AS sensivel;

SELECT TOP 3 NomeCliente, VolumeVendas, Plafond
FROM   mcp.ViewMCP_cliente_sensivel
ORDER BY VolumeVendas DESC, NomeCliente;

-- ── Permissões do mcpserver ─────────────────────────────────────────────
-- Papéis: só deve aparecer db_denydatawriter.
SELECT dp.name, r.name AS papel
FROM   sys.database_principals dp
LEFT JOIN sys.database_role_members m ON m.member_principal_id = dp.principal_id
LEFT JOIN sys.database_principals  r ON r.principal_id = m.role_principal_id
WHERE  dp.name = 'mcpserver';

-- Como mcpserver: lê as 3 views (se falhar, falta o GRANT em 03/05). O REVERT tem de correr sempre:
-- se um SELECT falhar, execute só "REVERT;".
EXECUTE AS USER = 'mcpserver';
SELECT COUNT(*) AS Fichas    FROM mcp.ViewMCP_cliente;
SELECT COUNT(*) AS Faturacao FROM mcp.ViewMCP_cliente_faturacao;
SELECT COUNT(*) AS Sensivel  FROM mcp.ViewMCP_cliente_sensivel;
REVERT;

SET STATISTICS TIME OFF;
GO

-- 04 · VERIFICAÇÃO (só leitura). Correr depois de 01–03 para confirmar que tudo ficou bem.
USE [IAVSGIX];
GO

-- ── ViewMCP_cliente ─────────────────────────────────────────────────────
-- Uma linha por cliente? (Linhas = Clientes)
SELECT COUNT(*)                    AS Linhas,
       COUNT(DISTINCT ClienteID)   AS Clientes,
       COUNT(DISTINCT NomeCliente) AS Nomes
FROM dbo.ViewMCP_cliente;

SELECT TOP 10 NomeCliente, Zona, Vendedor, TipoCliente, Actividade, Distrito
FROM   dbo.ViewMCP_cliente
ORDER BY NomeCliente;

-- Quanto está por preencher
SELECT COUNT(*) AS clientes,
  SUM(CASE WHEN Zona        = '(sem zona)'       THEN 1 ELSE 0 END) AS sem_zona,
  SUM(CASE WHEN Vendedor    = '(sem vendedor)'   THEN 1 ELSE 0 END) AS sem_vendedor,
  SUM(CASE WHEN TipoCliente = '(sem tipo)'       THEN 1 ELSE 0 END) AS sem_tipo,
  SUM(CASE WHEN Actividade  = '(sem actividade)' THEN 1 ELSE 0 END) AS sem_actividade,
  SUM(CASE WHEN Distrito    = '(sem distrito)'   THEN 1 ELSE 0 END) AS sem_distrito
FROM dbo.ViewMCP_cliente;

SELECT Zona, COUNT(*) AS clientes
FROM   dbo.ViewMCP_cliente
GROUP BY Zona
ORDER BY clientes DESC;

SELECT MAX(LEN(Actividade)) AS len, MAX(DATALENGTH(Actividade)) AS bytes
FROM dbo.ViewMCP_cliente;

-- Nomes com caracteres estranhos (espaços/bytes invisíveis)
SELECT ClienteID, NomeCliente, DATALENGTH(NomeCliente) AS bytes,
       CAST(NomeCliente AS varbinary(200)) AS hex
FROM dbo.ViewMCP_cliente
WHERE NomeCliente LIKE 'ANTONIO MANUEL CASTRO%';

-- ── ViewMCP_cliente_faturacao ───────────────────────────────────────────
-- Mesmo nº de clientes que a view da ficha?
SELECT (SELECT COUNT(*) FROM dbo.ViewMCP_cliente)           AS ficha,
       (SELECT COUNT(*) FROM dbo.ViewMCP_cliente_faturacao) AS faturacao;

-- Valores reais de Pagamento (o filtro do MCP aceita o início: "30 dias" → "30 Dias Fim do Mês")
SELECT Pagamento, COUNT(*) AS clientes
FROM   dbo.ViewMCP_cliente_faturacao
GROUP BY Pagamento
ORDER BY clientes DESC;

-- Cruzamento das duas views, como faz o servidor (ex.: Setúbal + 30 dias)
DECLARE @limite int = 10;
SELECT TOP (@limite) c.NomeCliente, c.Distrito, f.Pagamento, COUNT(*) OVER () AS Total
FROM   dbo.ViewMCP_cliente AS c
JOIN   dbo.ViewMCP_cliente_faturacao AS f ON f.ClienteID = c.ClienteID
WHERE  c.Distrito  COLLATE Latin1_General_CI_AI = 'Setubal'
  AND  f.Pagamento COLLATE Latin1_General_CI_AI LIKE '30 dias %'
ORDER BY c.NomeCliente;

-- ── Permissões (correr como mcpserver) ──────────────────────────────────
SELECT permission_name FROM fn_my_permissions('dbo.ViewMCP_cliente', 'OBJECT');
SELECT permission_name FROM fn_my_permissions('dbo.ViewMCP_cliente_faturacao', 'OBJECT');
GO

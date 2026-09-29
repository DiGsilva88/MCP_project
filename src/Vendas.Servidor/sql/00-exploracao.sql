-- 00 · EXPLORAÇÃO (só leitura, não altera nada). Correr por blocos, à vontade.
USE [IAVSGIX];
GO

-- ── Ligação ─────────────────────────────────────────────────────────────
SELECT @@VERSION AS Versao, DB_NAME() AS BaseDados, CONNECTIONPROPERTY('local_tcp_port') AS Porta;
SELECT USER_NAME() AS utilizador, DB_NAME() AS base;

-- A minha conta pode ver e gerir logins? (1 = sim, 0 = não)
SELECT IS_SRVROLEMEMBER('sysadmin')                     AS SouAdmin,
       HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY LOGIN') AS PossoGerirLogins;

-- ── Origem: dbo.VCliente ────────────────────────────────────────────────
-- Colunas e tipos
SELECT STRING_AGG(QUOTENAME(COLUMN_NAME), ', ')
           WITHIN GROUP (ORDER BY ORDINAL_POSITION) AS colunas
FROM   INFORMATION_SCHEMA.COLUMNS
WHERE  TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'VCliente';

SELECT DATA_TYPE, NUMERIC_PRECISION
FROM   INFORMATION_SCHEMA.COLUMNS
WHERE  TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'VCliente' AND COLUMN_NAME = 'VolumeVendas';

-- Colunas candidatas para a view de faturação
SELECT c.name AS coluna, t.name AS tipo, c.max_length
FROM   sys.columns AS c
JOIN   sys.types   AS t ON t.user_type_id = c.user_type_id
WHERE  c.object_id = OBJECT_ID('dbo.VCliente')
  AND (   c.name LIKE '%Plafond%'  OR c.name LIKE '%Credito%'
       OR c.name LIKE '%Pag%'      OR c.name LIKE '%Cobr%'
       OR c.name LIKE '%SitFin%'   OR c.name LIKE '%Tolerancia%'
       OR c.name LIKE '%Prazo%'    OR c.name LIKE '%Venc%')
ORDER BY c.name;

-- Clientes repetidos na origem: as linhas diferem nas colunas que usamos?
SELECT c.ClienteID, c.NomeCliente, c.Pagamento, c.Cobranca, c.SitFinanceira,
       c.Plafond, c.Zona, c.Vendedor
FROM   dbo.VCliente AS c
WHERE  c.ClienteID IN (SELECT ClienteID FROM dbo.VCliente
                       GROUP BY ClienteID HAVING COUNT(*) > 1)
ORDER BY c.ClienteID;

-- Quartis do Plafond (base dos escalões em 02-view-faturacao.sql)
SELECT DISTINCT
  COUNT(*) OVER ()                                           AS clientes,
  PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY Plafond) OVER () AS p25,
  PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY Plafond) OVER () AS mediana,
  PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY Plafond) OVER () AS p75
FROM dbo.VCliente
WHERE Plafond > 0;


--mostrar as colunas

SELECT COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'VCliente';

--criar dados limpos

SELECT * 
FROM dbo.[VCliente]
WHERE [ClienteID] IS NOT NULL 
  AND [NomeCliente] IS NOT NULL 
  AND [ContribuinteID] IS NOT NULL;



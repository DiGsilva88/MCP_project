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

  --ver se é a coluna ou junção no nome do artigo que está a dar problemas 

  SELECT COUNT(*) AS linhas,
       SUM(CASE WHEN a.ArtigoID IS NULL THEN 1 ELSE 0 END)                         AS sem_artigo_no_catalogo,
       SUM(CASE WHEN NULLIF(LTRIM(RTRIM(a.Artigo)), '') IS NULL THEN 1 ELSE 0 END)        AS artigo_vazio,
       SUM(CASE WHEN NULLIF(LTRIM(RTRIM(a.DescrResumida)), '') IS NULL THEN 1 ELSE 0 END) AS descr_resumida_vazia
FROM dbo.AnaVendArt AS v
LEFT JOIN dbo.FArtigo AS a ON a.ArtigoID = v.ArtigoID
                          AND a.TipoArtigo = v.TipoArtigo;

--ver se a coluna TipoDocID é chave primária em alguma tabela
SELECT c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
  ON k.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
 AND k.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA
JOIN INFORMATION_SCHEMA.COLUMNS c
  ON c.TABLE_SCHEMA = tc.TABLE_SCHEMA
 AND c.TABLE_NAME = tc.TABLE_NAME
WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND k.COLUMN_NAME = 'TipoDocID'
ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;

SELECT OBJECT_SCHEMA_NAME(m.object_id) AS Esquema,
       OBJECT_NAME(m.object_id) AS Objeto,
       o.type_desc
FROM sys.sql_modules m
JOIN sys.objects o ON o.object_id = m.object_id
WHERE m.definition LIKE '%tipo%doc%';

SELECT c.TABLE_SCHEMA, c.TABLE_NAME,
       c.COLUMN_NAME AS ColunaTipo,
       d.COLUMN_NAME AS ColunaDescricao
FROM INFORMATION_SCHEMA.COLUMNS c
JOIN INFORMATION_SCHEMA.COLUMNS d
  ON d.TABLE_SCHEMA = c.TABLE_SCHEMA
 AND d.TABLE_NAME = c.TABLE_NAME
WHERE (c.COLUMN_NAME LIKE '%tipo%doc%' OR c.COLUMN_NAME LIKE '%tpdoc%')
  AND (d.COLUMN_NAME LIKE '%desc%' OR d.COLUMN_NAME LIKE '%nome%' OR d.COLUMN_NAME LIKE '%design%')
ORDER BY c.TABLE_NAME;

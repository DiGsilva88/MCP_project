USE [IAVSGIX];
GO

SELECT DATA_TYPE, NUMERIC_PRECISION
FROM   INFORMATION_SCHEMA.COLUMNS
WHERE  TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'VCliente' AND COLUMN_NAME = 'VolumeVendas';


GO
CREATE OR ALTER VIEW [dbo].[ViewMCP_cliente] AS
SELECT ClienteID, NomeCliente, Zona, Vendedor, TipoCliente, Actividade, Distrito
FROM (
    SELECT [ClienteID],
           LTRIM(RTRIM([NomeCliente])) AS NomeCliente,
           COALESCE(NULLIF(LTRIM(RTRIM([Zona])),''),        '(sem zona)')       AS Zona,
           COALESCE(NULLIF(LTRIM(RTRIM([Vendedor])),''),    '(sem vendedor)')   AS Vendedor,
           COALESCE(NULLIF(LTRIM(RTRIM([TipoCliente])),''), '(sem tipo)')       AS TipoCliente,
           COALESCE(NULLIF(LTRIM(RTRIM([Actividade])),''),  '(sem actividade)') AS Actividade,
           COALESCE(NULLIF(LTRIM(RTRIM([Distrito])),''),    '(sem distrito)')   AS Distrito,
           ROW_NUMBER() OVER (PARTITION BY [ClienteID]
                              ORDER BY [NomeCliente], [Vendedor]) AS rn
    FROM [dbo].[VCliente]
) AS x
WHERE rn = 1;
GO

SELECT MAX(LEN(Actividade)) AS len, MAX(DATALENGTH(Actividade)) AS bytes
FROM dbo.ViewMCP_cliente;


SELECT COUNT(*)                    AS Linhas,
       COUNT(DISTINCT ClienteID)   AS Clientes,
       COUNT(DISTINCT NomeCliente) AS Nomes
FROM dbo.ViewMCP_cliente;

SELECT ClienteID, NomeCliente, DATALENGTH(NomeCliente) AS bytes,
       CAST(NomeCliente AS varbinary(200)) AS hex
FROM dbo.ViewMCP_cliente
WHERE NomeCliente LIKE 'ANTONIO MANUEL CASTRO%';


USE [IAVSGIX];

SELECT c.name AS coluna, t.name AS tipo, c.max_length
FROM   sys.columns AS c
JOIN   sys.types   AS t ON t.user_type_id = c.user_type_id
WHERE  c.object_id = OBJECT_ID('dbo.VCliente')
  AND (   c.name LIKE '%Plafond%'  OR c.name LIKE '%Credito%'
       OR c.name LIKE '%Pag%'      OR c.name LIKE '%Cobr%'
       OR c.name LIKE '%SitFin%'   OR c.name LIKE '%Tolerancia%'
       OR c.name LIKE '%Prazo%'    OR c.name LIKE '%Venc%')
ORDER BY c.name;

SELECT STRING_AGG(QUOTENAME(COLUMN_NAME), ', ')
           WITHIN GROUP (ORDER BY ORDINAL_POSITION) AS colunas
FROM   INFORMATION_SCHEMA.COLUMNS
WHERE  TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'VCliente';


USE [IAVSGIX];
GO

SELECT name, is_disabled, is_policy_checked,
       LOGINPROPERTY(name, 'IsLocked') AS Bloqueado,
       create_date
FROM   sys.sql_logins
WHERE  name = 'mcp_leitor';

-- A minha conta pode ver e gerir logins? (1 = sim, 0 = não)

SELECT IS_SRVROLEMEMBER('sysadmin')                 AS SouAdmin,
       HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY LOGIN') AS PossoGerirLogins;

-- Existe o user mcp_leitor dentro da IAVSGIX?
-- (sendo db_owner, esta lista vê-a sempre toda)

USE IAVSGIX;
SELECT name, create_date
FROM   sys.database_principals
WHERE  name = 'mcp_leitor';







-- ============  O USER  ============
USE IAVSGIX;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'mcp_leitor')
    CREATE USER mcp_leitor FOR LOGIN mcp_leitor WITH DEFAULT_SCHEMA = dbo;
GO


-- ============ 4C. AS PERMISSÕES (a gaveta) ============
ALTER ROLE db_denydatawriter ADD MEMBER mcp_leitor;           -- nunca consegue escrever
GRANT SELECT ON OBJECT::dbo.ViewMCP_cliente TO mcp_leitor;    -- lê só esta view
-- Quando a view de faturação existir, acrescenta-se:
-- GRANT SELECT ON OBJECT::dbo.ViewMCP_cliente_faturacao TO mcp_leitor;
GO

GRANT SELECT ON OBJECT::[dbo].[ViewMCP_cliente_faturacao] TO mcp_leitor;
GO
GRANT SELECT ON OBJECT::[dbo].[ViewMCP_cliente] TO mcp_leitor;
GO


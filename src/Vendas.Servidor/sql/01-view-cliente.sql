-- 01 · VIEW DA FICHA DO CLIENTE. Só o CREATE; verificações em 04-verificacao.sql.
USE [IAVSGIX];
GO

-- Schema próprio das views MCP (as tabelas/views de origem continuam em dbo).
-- AUTHORIZATION dbo: o dono do schema tem de ser o mesmo de dbo.VCliente, senão a cadeia de
-- propriedade quebra e o mcpserver leva "SELECT permission was denied on VCliente".
IF SCHEMA_ID('mcp') IS NULL EXEC('CREATE SCHEMA [mcp] AUTHORIZATION dbo');
ELSE ALTER AUTHORIZATION ON SCHEMA::mcp TO dbo;
GO

-- Migração: remove as views antigas em dbo (o GRANT ao mcpserver cai com elas; 03 e 05 voltam a dá-lo).
DROP VIEW IF EXISTS [dbo].[ViewMCP_cliente_sensivel], [dbo].[ViewMCP_cliente_faturacao], [dbo].[ViewMCP_cliente];
GO

CREATE OR ALTER VIEW [mcp].[ViewMCP_cliente] AS
SELECT ClienteID, NomeCliente, Zona, Vendedor, TipoCliente, Actividade, Localidade, Distrito, ContribuinteID, Email
FROM (
    SELECT [ClienteID],
           LTRIM(RTRIM([NomeCliente])) AS NomeCliente,
           COALESCE(NULLIF(LTRIM(RTRIM([Zona])),''),        '(sem zona)')       AS Zona,
           COALESCE(NULLIF(LTRIM(RTRIM([Vendedor])),''),    '(sem vendedor)')   AS Vendedor,
           COALESCE(NULLIF(LTRIM(RTRIM([TipoCliente])),''), '(sem tipo)')       AS TipoCliente,
           COALESCE(NULLIF(LTRIM(RTRIM([Actividade])),''),  '(sem actividade)') AS Actividade,
           COALESCE(NULLIF(LTRIM(RTRIM([Localidade])),''),  '(sem localidade)') AS Localidade,
           COALESCE(NULLIF(LTRIM(RTRIM([Distrito])),''),    '(sem distrito)')   AS Distrito,
           -- Mascarar o Telefone (Corrigido o duplicado e a vírgula)
           COALESCE(
               NULLIF(
                   CASE
                       WHEN [Telefone] IS NOT NULL AND LEN(CAST([Telefone] AS VARCHAR(50))) >= 9
                       THEN '*******' + RIGHT(CAST([Telefone] AS VARCHAR(50)), 3)
                       ELSE ''
                   END, ''
               ), '(Sem telefone)'
           ) AS Telefone,
           -- Mascarar o NIF
           COALESCE(
               NULLIF(
                   CASE 
                    WHEN [ContribuinteID] IS NOT NULL 
                 AND LEN(LTRIM(RTRIM(CAST([ContribuinteID] AS VARCHAR(50))))) >= 9
            THEN 'XXXXXX' + RIGHT(LTRIM(RTRIM(CAST([ContribuinteID] AS VARCHAR(50)))), 3)
                       ELSE ''  
                   END, ''
               ), '(Sem NIF)'
           ) AS ContribuinteID,
           --Mascarar o Email (Corrigido para usar '@' e evitar erros)
           COALESCE(
               NULLIF(
                   CASE 
                       WHEN [Email] LIKE '%@%' 
                       THEN LEFT([Email], 2) + '**' + SUBSTRING([Email], CHARINDEX('@', [Email]), LEN([Email]))
                       ELSE '' 
                   END, ''
               ), '(sem e-mail)'
           ) AS Email,
           ROW_NUMBER() OVER (PARTITION BY [ClienteID]
                              ORDER BY [NomeCliente], [Vendedor]) AS rn
    FROM [dbo].[VCliente]
) AS x
WHERE rn = 1;
GO

ALTER AUTHORIZATION ON SCHEMA::mcp TO dbo;
SELECT s.name, USER_NAME(s.principal_id) AS dono FROM sys.schemas s WHERE s.name = 'mcp';

SELECT s.name, USER_NAME(s.principal_id) AS dono FROM sys.schemas s WHERE s.name IN ('dbo','mcp');.
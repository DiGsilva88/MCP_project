-- 05 · VIEW DOS DADOS SENSÍVEIS (NIF, contactos, morada), SEM máscara. Fica separada das views 01/02
-- de propósito: o GRANT é próprio e pode ser dado/retirado sem mexer no resto. Correr depois de 01.
USE [IAVSGIX];
GO

CREATE OR ALTER VIEW [dbo].[ViewMCP_cliente_sensivel] AS
SELECT ClienteID, NomeCliente, ContribuinteID, Email, Telefone, Endereco, PostalID, VolumeVendas, Plafond
FROM (
    SELECT [ClienteID],
           LTRIM(RTRIM([NomeCliente])) AS NomeCliente,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST([ContribuinteID] AS varchar(50)))),''), '(sem NIF)')      AS ContribuinteID,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST([Email]          AS varchar(100)))),''), '(sem e-mail)')  AS Email,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST([Telefone]       AS varchar(50)))),''),  '(sem telefone)') AS Telefone,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST([Endereco]       AS varchar(200)))),''), '(sem morada)')  AS Endereco,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST([PostalID]       AS varchar(20)))),''),  '(sem código postal)') AS PostalID,
           -- valores reais (em 02 só saem como escalão). NULL fica 0: permite ordenar para rankings
           COALESCE([VolumeVendas], 0) AS VolumeVendas,
           COALESCE([Plafond], 0)      AS Plafond,
           -- mesma regra de 01: uma linha por cliente
           ROW_NUMBER() OVER (PARTITION BY [ClienteID] ORDER BY [NomeCliente], [Vendedor]) AS rn
    FROM [dbo].[VCliente]
) AS x
WHERE rn = 1;
GO


-- O mcpserver continua só de leitura (db_denydatawriter, ver 03): este é o único acesso novo.
GRANT SELECT ON OBJECT::dbo.ViewMCP_cliente_sensivel TO mcpserver;
GO



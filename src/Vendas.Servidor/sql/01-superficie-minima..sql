USE [IAVSGIX];
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
GRANT SELECT ON OBJECT::[dbo].[ViewMCP_cliente] TO mcp_leitor;
GO
-- Superfície de leitura do MCP sobre a ficha de clientes.
-- Correr numa base de dados nova para recriar tudo o que o servidor precisa.
USE [IAVSGIX];
GO

CREATE OR ALTER VIEW [dbo].[ViewMCP_cliente] AS
SELECT DISTINCT
    [ClienteID],
    LTRIM(RTRIM(CAST([NomeCliente] AS varchar(70)))) AS [NomeCliente],
    ISNULL(NULLIF(LTRIM(RTRIM(CAST([Zona]        AS varchar(20)))),''), '(sem zona)')       AS [Zona],
    ISNULL(NULLIF(LTRIM(RTRIM(CAST([Vendedor]    AS varchar(30)))),''), '(sem vendedor)')   AS [Vendedor],
    ISNULL(NULLIF(LTRIM(RTRIM(CAST([TipoCliente] AS varchar(1)))),''),  '(sem tipo)')       AS [TipoCliente],
    ISNULL(NULLIF(LTRIM(RTRIM(CAST([Actividade]  AS varchar(50)))),''), '(sem actividade)') AS [Actividade],
    ISNULL(NULLIF(LTRIM(RTRIM(CAST([Distrito]    AS varchar(50)))),''), '(sem distrito)')   AS [Distrito]
FROM [dbo].[VCliente];
GO

GRANT SELECT ON OBJECT::[dbo].[ViewMCP_cliente] TO mcp_leitor;
GO
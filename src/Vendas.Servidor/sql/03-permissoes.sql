-- 03 · PERMISSÕES DO LOGIN mcpserver (só leitura, só nas views MCP). Correr depois de 01 e 02.
-- O GRANT da view sensível (05) está no próprio 05.
USE [IAVSGIX];
GO

-- Estado atual do user (antes)
SELECT dp.name AS UserNaBase, dp.authentication_type_desc, SUSER_SNAME(dp.sid) AS Login,
       r.name AS Papel
FROM   sys.database_principals AS dp
LEFT JOIN sys.database_role_members AS m ON m.member_principal_id = dp.principal_id
LEFT JOIN sys.database_principals  AS r ON r.principal_id = m.role_principal_id
WHERE  dp.name = 'mcpserver';
GO

-- IS_ROLEMEMBER evita o erro "x não é membro de y", que abortava o batch
-- e deixava os GRANT seguintes por executar.
IF IS_ROLEMEMBER('db_datareader', 'mcpserver') = 1
    ALTER ROLE db_datareader DROP MEMBER mcpserver;      -- deixa de ler tudo
IF IS_ROLEMEMBER('db_denydatawriter', 'mcpserver') = 0
    ALTER ROLE db_denydatawriter ADD MEMBER mcpserver;   -- nunca escreve

GRANT SELECT ON OBJECT::mcp.ViewMCP_cliente           TO mcpserver;
GRANT SELECT ON OBJECT::mcp.ViewMCP_cliente_faturacao TO mcpserver;
GO

-- Confirmar: authentication_type_desc tem de dizer INSTANCE
SELECT name, authentication_type_desc
FROM   sys.database_principals
WHERE  name = 'mcpserver';
GO

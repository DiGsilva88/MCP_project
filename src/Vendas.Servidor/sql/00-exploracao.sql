USE [IAVSGIX];
GO

SELECT @@VERSION AS Versao, DB_NAME() AS BaseDados;
GO



-- Quanto está por preencher na view de clientes 

SELECT COUNT(*) AS clientes, 
  SUM(CASE WHEN Zona        = '(sem zona)'       THEN 1 ELSE 0 END) AS sem_zona, 
  SUM(CASE WHEN Vendedor    = '(sem vendedor)'   THEN 1 ELSE 0 END) AS sem_vendedor, 
  SUM(CASE WHEN TipoCliente = '(sem tipo)'       THEN 1 ELSE 0 END) AS sem_tipo, 
  SUM(CASE WHEN Actividade  = '(sem actividade)' THEN 1 ELSE 0 END) AS sem_actividade, 
  SUM(CASE WHEN Distrito    = '(sem distrito)'   THEN 1 ELSE 0 END) AS sem_distrito 

FROM dbo.ViewMCP_cliente; 

  

-- Clientes repetidos na origem: as linhas diferem nas colunas que usamos? 

SELECT c.ClienteID, c.NomeCliente, c.Pagamento, c.Cobranca, c.SitFinanceira, 
       c.Plafond, c.Zona, c.Vendedor 
FROM dbo.VCliente AS c 
WHERE c.ClienteID IN (SELECT ClienteID FROM dbo.VCliente 
                      GROUP BY ClienteID HAVING COUNT(*) > 1) 
ORDER BY c.ClienteID;


SELECT name, type_desc, is_disabled, default_database_name
FROM sys.server_principals
WHERE name = 'mcp_leitor'; -- o login (servidor)
USE [IAVSGIX];
SELECT name, type_desc
FROM sys.database_principals
WHERE name = 'mcp_leitor'; -- o utilizador (base de dados)
-- As permissões que tem: devem aparecer só as 2 views, com SELECT / GRANT.
SELECT OBJECT_NAME(p.major_id) AS objeto, p.permission_name, p.state_desc
FROM sys.database_permissions AS p
WHERE USER_NAME(p.grantee_principal_id) = 'mcp_leitor';




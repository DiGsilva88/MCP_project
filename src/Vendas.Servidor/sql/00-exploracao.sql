USE [IAVSGIX];
GO

SELECT @@VERSION AS Versao, DB_NAME() AS BaseDados;
GO

SELECT CONNECTIONPROPERTY('local_tcp_port') AS Porta;

-- Quanto está por preencher na view de clientes 

SELECT COUNT(*) AS clientes, 
  SUM(CASE WHEN Zona        = '(sem zona)'       THEN 1 ELSE 0 END) AS sem_zona, 
  SUM(CASE WHEN Vendedor    = '(sem vendedor)'   THEN 1 ELSE 0 END) AS sem_vendedor, 
  SUM(CASE WHEN TipoCliente = '(sem tipo)'       THEN 1 ELSE 0 END) AS sem_tipo, 
  SUM(CASE WHEN Actividade  = '(sem actividade)' THEN 1 ELSE 0 END) AS sem_actividade, 
  SUM(CASE WHEN Distrito    = '(sem distrito)'   THEN 1 ELSE 0 END) AS sem_distrito 

FROM dbo.ViewMCP_cliente; 

SELECT CONNECTIONPROPERTY('local_tcp_port') AS Porta;

-- Clientes repetidos na origem: as linhas diferem nas colunas que usamos? 

SELECT c.ClienteID, c.NomeCliente, c.Pagamento, c.Cobranca, c.SitFinanceira, 
       c.Plafond, c.Zona, c.Vendedor 
FROM dbo.VCliente AS c 
WHERE c.ClienteID IN (SELECT ClienteID FROM dbo.VCliente 
                      GROUP BY ClienteID HAVING COUNT(*) > 1) 
ORDER BY c.ClienteID;


SELECT Zona, COUNT(*) AS clientes
FROM   dbo.ViewMCP_cliente
GROUP  BY Zona
ORDER  BY clientes DESC;

SELECT USER_NAME() AS login, USER_NAME() AS utilizador, DB_NAME() AS base;

SELECT permission_name
FROM fn_my_permissions('dbo.ViewMCP_cliente', 'OBJECT')



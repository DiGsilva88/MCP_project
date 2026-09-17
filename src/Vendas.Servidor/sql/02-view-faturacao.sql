USE [IAVSGIX];
GO

CREATE OR ALTER VIEW [dbo].[ViewMCP_cliente_faturacao] AS
SELECT ClienteID, NomeCliente, Pagamento, Cobranca, Expedicao, SitFinanceira, EscalaoPlafond
FROM (
    SELECT [ClienteID],
           LTRIM(RTRIM([NomeCliente])) AS NomeCliente,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST([Pagamento]     AS varchar(100)))),''), '(sem pagamento)') AS Pagamento,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST([Cobranca]      AS varchar(100)))),''), '(sem cobrança)')  AS Cobranca,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST([Expedicao]     AS varchar(100)))),''), '(sem expedição)') AS Expedicao,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST([SitFinanceira] AS varchar(100)))),''), '(sem situação)')  AS SitFinanceira,
           CASE WHEN [Plafond] IS NULL OR [Plafond] = 0 THEN '0 - sem plafond'
                WHEN [Plafond] <=  1000 THEN '1 - até 1.000'
                WHEN [Plafond] <=  5000 THEN '2 - 1.001 a 5.000'
                WHEN [Plafond] <= 20000 THEN '3 - 5.001 a 20.000'
                ELSE                         '4 - mais de 20.000' END AS EscalaoPlafond,
           ROW_NUMBER() OVER (PARTITION BY [ClienteID] ORDER BY [NomeCliente]) AS rn
    FROM [dbo].[VCliente]
) AS x
WHERE rn = 1;
GO


SELECT COUNT(*) AS linhas, COUNT(DISTINCT ClienteID) AS clientes
FROM dbo.ViewMCP_cliente_faturacao;          -- os dois números têm de ser iguais

SELECT EscalaoPlafond, COUNT(*) AS clientes
FROM dbo.ViewMCP_cliente_faturacao
GROUP BY EscalaoPlafond ORDER BY EscalaoPlafond;


SELECT DISTINCT 
  COUNT(*) OVER () AS clientes, 
  PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY Plafond) OVER () AS p25, 
  PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY Plafond) OVER () AS mediana, 
  PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY Plafond) OVER () AS p75 
FROM dbo.VCliente 
WHERE Plafond > 0; 

GO
SELECT TOP (21) [Pagamento] AS Valor, COUNT(*) AS Clientes,
       SUM(COUNT(*)) OVER () AS Total, COUNT(*) OVER () AS Grupos
FROM   dbo.ViewMCP_cliente_faturacao
GROUP BY [Pagamento]
ORDER BY Clientes DESC, Valor;

SELECT COUNT(*) AS linhas, COUNT(DISTINCT ClienteID) AS clientes 

FROM dbo.ViewMCP_cliente_faturacao;                         -- iguais 

  

SELECT COUNT(DISTINCT Pagamento) AS pagamentos, COUNT(DISTINCT Cobranca) AS cobrancas, 
COUNT(DISTINCT SitFinanceira) AS situacoes, COUNT(DISTINCT EscalaoPlafond) AS escaloes 
FROM dbo.ViewMCP_cliente_faturacao;                         -- uma coluna com 1 valor não serve para agrupar 

GRANT SELECT ON OBJECT::[dbo].[ViewMCP_cliente_faturacao] TO mcp_leitor;
GO
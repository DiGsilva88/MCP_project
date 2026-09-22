USE [IAVSGIX];
GO

CREATE OR ALTER VIEW [dbo].[ViewMCP_cliente_faturacao] AS
SELECT ClienteID, NomeCliente, Pagamento, Cobranca, Expedicao, SitFinanceira,
       EscalaoPlafond, EscalaoVolumeVendas
FROM (
    SELECT c.[ClienteID],
           LTRIM(RTRIM(c.[NomeCliente])) AS NomeCliente,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Pagamento]     AS varchar(100)))),''), '(sem pagamento)') AS Pagamento,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Cobranca]      AS varchar(100)))),''), '(sem cobrança)')  AS Cobranca,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Expedicao]     AS varchar(100)))),''), '(sem expedição)') AS Expedicao,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[SitFinanceira] AS varchar(100)))),''), '(sem situação)')  AS SitFinanceira,
           CASE WHEN COALESCE(c.[Plafond], 0) = 0 THEN '0 - sem plafond'
                WHEN c.[Plafond] <=  1000 THEN '1 - até 1.000'
                WHEN c.[Plafond] <=  5000 THEN '2 - 1.001 a 5.000'
                WHEN c.[Plafond] <= 20000 THEN '3 - 5.001 a 20.000'
                ELSE                           '4 - mais de 20.000' END AS EscalaoPlafond,

           -- valor declarado na ficha do cliente; não é faturação real
           CASE WHEN COALESCE(c.[VolumeVendas], 0) = 0 THEN '0 - sem volume declarado'
                WHEN c.[VolumeVendas] <=   50000 THEN '1 - até 50 mil'
                WHEN c.[VolumeVendas] <=  250000 THEN '2 - 50 mil a 250 mil'
                WHEN c.[VolumeVendas] <= 1000000 THEN '3 - 250 mil a 1 milhão'
                ELSE                                  '4 - mais de 1 milhão' END AS EscalaoVolumeVendas,
           ROW_NUMBER() OVER (PARTITION BY c.[ClienteID]
                              ORDER BY c.[TerceiroID]) AS rn
    FROM [dbo].[VCliente] AS c
) AS x
WHERE rn = 1;
GO

--SQL (reutiliza o Coluna() do switch, nunca texto do modelo):
DECLARE @limite int= 10,
        @valor varchar(100) = '30 dias Fim do Mês';

SELECT TOP (@limite) NomeCliente, [Pagamento] AS Valor, COUNT(*) OVER () AS Total
FROM dbo.ViewMCP_cliente_faturacao
WHERE [Pagamento] = @valor
ORDER BY NomeCliente;

GRANT SELECT ON OBJECT::[dbo].[ViewMCP_cliente_faturacao] TO mcp_leitor;
GO


SELECT DISTINCT
  COUNT(*)OVER () AS clientes, 
  PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY Plafond) OVER () AS p25, 
  PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY Plafond) OVER () AS mediana, 
  PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY Plafond) OVER () AS p75 
FROM dbo.VCliente
WHERE Plafond > 0



SELECT COUNT(*) FROM dbo.ViewMCP_cliente_faturacao;


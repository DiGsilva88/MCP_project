USE [IAVSGIX];
GO

CREATE OR ALTER VIEW [dbo].[ViewMCP_cliente_faturacao] AS
SELECT 
    ClienteID, NomeCliente, TipoCliente, FormaJuridica, Actividade, Cae, Pais, Zona, Vendedor, 
    Pagamento, Cobranca, Expedicao, SitFinanceira, EscalaoPlafond, EscalaoVolumeVendas
FROM (
    SELECT c.[ClienteID],
           LTRIM(RTRIM(c.[NomeCliente])) AS NomeCliente,
           -- Campos Comerciais e de Perfil
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[TipoCliente]   AS varchar(100)))),''), '(sem tipo)')       AS TipoCliente,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[FormaJuridica] AS varchar(100)))),''), '(sem forma jur.)')  AS FormaJuridica,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Actividade]    AS varchar(100)))),''), '(sem actividade)') AS Actividade,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Cae]           AS varchar(100)))),''), '(sem CAE)')        AS Cae, 
           -- Campos Geográficos
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Pais]          AS varchar(100)))),''), '(sem país)')       AS Pais,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Zona]          AS varchar(100)))),''), '(sem zona)')       AS Zona,
           -- Campos de Faturação e Operações
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Vendedor]      AS varchar(100)))),''), '(sem vendedor)')   AS Vendedor,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Pagamento]     AS varchar(100)))),''), '(sem pagamento)')  AS Pagamento,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Cobranca]      AS varchar(100)))),''), '(sem cobrança)')  AS Cobranca,
           COALESCE(NULLIF(LTRIM(RTRIM(CAST(c.[Expedicao]     AS varchar(100)))),''), '(sem expedição)') AS Expedicao,
           -- Proteção da SitFinanceira (Mascara textos descritivos longos)
           COALESCE(
               NULLIF(
                   CASE 
                       WHEN c.[SitFinanceira] IS NOT NULL AND LEN(LTRIM(RTRIM(CAST(c.[SitFinanceira] AS varchar(100))))) > 5
                       THEN LEFT(LTRIM(RTRIM(CAST(c.[SitFinanceira] AS varchar(100)))), 4) + '***'
                       ELSE LTRIM(RTRIM(CAST(c.[SitFinanceira] AS varchar(100))))
                   END, ''
               ), '(sem situação)'
           ) AS SitFinanceira,
           -- Escalões Financeiros Protegidos
           CASE WHEN COALESCE(c.[Plafond], 0) = 0 THEN '0 - sem plafond'
                WHEN c.[Plafond] <=  1000 THEN '1 - até 1.000'
                WHEN c.[Plafond] <=  5000 THEN '2 - 1.001 a 5.000'
                WHEN c.[Plafond] <= 20000 THEN '3 - 5.001 a 20.000'
                ELSE                           '4 - mais de 20.000' END AS EscalaoPlafond,
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

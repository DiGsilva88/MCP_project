USE [IAVSGIX];
GO

SELECT @@VERSION AS Versao, DB_NAME() AS BaseDados;
GO

-- 1) Qualidade de dados na origem: quantos valores preenchidos há em cada coluna
SELECT COUNT(*)                            AS Linhas,
       COUNT(NULLIF(TRIM(NomeCliente),'')) AS NomeCliente,
       COUNT(NULLIF(TRIM(Zona),''))        AS Zona,
       COUNT(ZonaID)                       AS ZonaID,
       COUNT(NULLIF(TRIM(Vendedor),''))    AS Vendedor,
       COUNT(VendedorID)                   AS VendedorID,
       COUNT(NULLIF(TRIM(TipoCliente),'')) AS TipoCliente,
       COUNT(NULLIF(TRIM(Actividade),''))  AS Actividade,
       COUNT(NULLIF(TRIM(Distrito),''))    AS Distrito,
       COUNT(ConcelhoID)                   AS ConcelhoID,
       COUNT(GrupoContab)                  AS GrupoContab
FROM dbo.VCliente;



GO
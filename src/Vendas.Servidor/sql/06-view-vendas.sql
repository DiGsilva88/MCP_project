

--schema já existente mcp vai usar a view AnaVendart

--confirma que o schema mcp existe e quem é o dono do schema
-- dono da AnaVendArt e das tabelas que ela lê (normalmente dbo).
SELECT s.name                    AS schema_nome,
       USER_NAME(s.principal_id) AS dono_schema
FROM   sys.schemas AS s
WHERE  s.name IN ('mcp','dbo');

--quem é o dono da tabela de origem AnaVendart

SELECT o.name AS objecto,
         USER_NAME(COALESCE(o.principal_id, s.principal_id)) AS dono
FROM   sys.objects AS o
       JOIN sys.schemas AS s ON o.schema_id = s.schema_id
WHERE  o.name = 'AnaVendArt'
;


SELECT o.name                                               AS objeto,
       USER_NAME(COALESCE(o.principal_id, s.principal_id))  AS dono
FROM   sys.objects AS o
JOIN   sys.schemas AS s ON s.schema_id = o.schema_id
WHERE  o.object_id IN (OBJECT_ID('dbo.AnaVendArt'), OBJECT_ID('dbo.FArtigo'));

ALTER AUTHORIZATION ON SCHEMA::mcp TO dbo; -- altera o dono do schema mcp para dbo



-- 1. A coluna com o nome do artigo na FArtigo (ex.: Descricao, Nome) antes de criar a view AnaVendArt, para que a view funcione corretamente, deve-se garantir que a coluna com o nome do artigo 
--na tabela FArtigo exista e esteja corretamente definida. Isso é importante porque a view AnaVendArt depende dessa coluna para exibir informações sobre os artigos vendidos.

SELECT name, TYPE_NAME(system_type_id) AS tipo, max_length
FROM   sys.columns
WHERE  object_id = OBJECT_ID('dbo.FArtigo')
AND (name LIKE '%Desc%' OR name LIKE '%Nome%');

-- A tabela das familias e as suas colunas
SELECT OBJECT_NAME(c.object_id) AS tabela,
c.name AS coluna,
TYPE_NAME(c.user_type_id) AS tipo
FROM   sys.columns AS c
JOIN   sys.tables AS t ON c.object_id = t.object_id
WHERE  t.name LIKE '%Familia%'
ORDER BY tabela, c.column_id;


-- a tabela com descrição dos artigos (FArtigo) e as suas colunas
SELECT name, TYPE_NAME(user_type_id) AS tipo, max_length
FROM   sys.columns
WHERE  object_id = OBJECT_ID('dbo.FArtigo')
  AND (name LIKE '%Desc%' OR name LIKE '%Nome%');

GO

--confirmar se as notas de crédito vem com sinal

SELECT TipoDocID,
       MIN(Quantidade) AS MinQtd, MAX(Quantidade) AS MaxQtd,
       MIN(VlrLiquido) AS MinValor, MAX(VlrLiquido) AS MaxValor
FROM dbo.AnaVendArt
GROUP BY TipoDocID;

--ver se existem artigos duplicados
SELECT ArtigoID, TipoArtigo, COUNT(*) AS N
FROM dbo.ATG_Marca
GROUP BY ArtigoID, TipoArtigo
HAVING COUNT(*) > 1;

SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE '%Marca%';


SELECT TABLE_NAME, COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME LIKE '%Marca%';
EXEC sp_help 'dbo.ATG_Marca';

--criar a view mcp.Vendas, que vai ler a AnaVendArt e a FArtigo, e a ATG_Familia para obter o nome da familia do artigo
CREATE OR ALTER VIEW mcp.ViewMCP_vendas AS
SELECT
    -- Documento "FT 1/2026/123 o Doc Id repete se entre anos e secções, 
    -- por isso é necessário acrescentar o ano e a secção para tornar o DocumentoID único"
    CAST(CONCAT(RTRIM(v.TipoDocID),' ', v.SecVendaID,'/', 
    YEAR(v.DataDoc), '/', v.DocID) AS nvarchar(80)) AS DocumentoID,
    CAST(RTRIM(v.TipoDocID) as nvarchar(20)) AS TipoDocID,
    CAST(v.DataDoc AS date) AS DataDoc,
    -- Cliente: a mesma chave da ViewMcp_clientes, para permitir a junção entre as duas views
    v.ClienteID AS ClienteID,
    -- Artigo: o código vem da AnaVendArt, o nome vem da Fartigo.
    CAST(RTRIM(v.ArtigoID) AS nvarchar(50)) AS ArtigoID,
    COALESCE(NULLIF(LTRIM(RTRIM(a.DescrResumida)),''), '(sem descrição)') AS Artigo,
    COALESCE(NULLIF(LTRIM(RTRIM(f.Descricao)),''), '(sem familia)') AS Familia,
    -- Medidas: o sinal já vem da AnaVendArt(multiplicador ActAnaVend).
    CAST(v.Quantidade AS decimal(18,2)) AS Quantidade,
    CAST(v.VlrLiquido AS decimal(18,2)) AS ValorLiquido
FROM dbo.AnaVendArt AS v
-- A chave da Fartigo é o ArtigoID, que é a mesma chave usada na AnaVendArt. 
-- A junção é feita com base nessa chave para obter o nome do artigo correspondente ArtigoId+TipoArtigo
-- um artigo apagado do catalogo não faz desaparecer a venda
LEFT JOIN dbo.FArtigo AS a ON a.ArtigoID = v.ArtigoID 
LEFT JOIN dbo.ATG_Familia AS f ON a.FamiliaID = f.FamiliaID; 
GO




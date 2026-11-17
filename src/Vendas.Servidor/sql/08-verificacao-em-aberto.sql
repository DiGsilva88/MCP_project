-- 08 · VERIFICAÇÃO dos valores de "Faturas por pagar" (briefing do cliente, alerta "Fatura em atraso" e a tool consultar_em_aberto).
-- SÓ LÊ (SELECT): não altera nada. Correr no SSMS com um login de leitura, bloco a bloco, e guardar os resultados.
-- A consulta do painel (ConsultaVendas.EmAberto) soma RemanMoedaAdop de TODAS as linhas de mcp.ViewMCP_resp_aberto_cli:
-- não filtra por aplicação, área, tipo de terceiro nem tipo de documento, e "documentos" são linhas da view.
-- Cada bloco confirma (ou afasta) uma dúvida; a conclusão de cada um está no comentário.
USE [IAVSGIX];
GO

-- 1. O que a view contém. Se houver mais do que faturas de clientes (outras aplicações, fornecedores, recibos, adiantamentos),
--    o total do painel inclui-os. A view de vendas só usa Aplicacao = 'VND' (ver 06): comparar com o que aparece aqui.
SELECT Aplicacao, AreaID, TipoTerceiro, RTRIM(TipoDocID) AS TipoDocID,
       COUNT(*) AS linhas, SUM(CAST(COALESCE(RemanMoedaAdop, 0) AS decimal(18,2))) AS saldo
FROM   mcp.ViewMCP_resp_aberto_cli
GROUP BY Aplicacao, AreaID, TipoTerceiro, RTRIM(TipoDocID)
ORDER BY linhas DESC;

-- 2. Saldos negativos (notas de crédito, adiantamentos, recibos por abater). Entram no total e no "vencido" e baixam-nos;
--    a contagem de "documentos" também os conta.
SELECT RTRIM(TipoDocID) AS TipoDocID, COUNT(*) AS linhas, SUM(CAST(RemanMoedaAdop AS decimal(18,2))) AS saldo
FROM   mcp.ViewMCP_resp_aberto_cli
WHERE  RemanMoedaAdop < 0
GROUP BY RTRIM(TipoDocID)
ORDER BY saldo;

-- 3. Linhas contra documentos. Se "linhas" > "documentos", há documentos com mais de uma linha (prestações, vários vencimentos)
--    e o painel conta cada linha como um documento.
SELECT COUNT(*) AS linhas,
       COUNT(DISTINCT CONCAT(RTRIM(TipoDocID), '|', Ano, '|', DocID, '|', EntidadeID)) AS documentos
FROM   mcp.ViewMCP_resp_aberto_cli;

SELECT TOP (20) RTRIM(TipoDocID) AS TipoDocID, Ano, DocID, EntidadeID, COUNT(*) AS linhas
FROM   mcp.ViewMCP_resp_aberto_cli
GROUP BY RTRIM(TipoDocID), Ano, DocID, EntidadeID
HAVING COUNT(*) > 1
ORDER BY linhas DESC;

-- 4. Entidades que não são clientes da ficha. A consulta compara EntidadeID com ClienteID sem olhar a TipoTerceiro: um fornecedor
--    com o mesmo código de um cliente seria somado ao cliente. "sem_ficha_de_cliente" > 0 em TipoTerceiro que não seja cliente = confirmado.
SELECT r.TipoTerceiro, COUNT(*) AS linhas, COUNT(DISTINCT r.EntidadeID) AS entidades,
       SUM(CASE WHEN c.ClienteID IS NULL THEN 1 ELSE 0 END) AS sem_ficha_de_cliente
FROM   mcp.ViewMCP_resp_aberto_cli AS r
LEFT JOIN mcp.ViewMCP_cliente AS c ON CAST(c.ClienteID AS nvarchar(50)) = CAST(r.EntidadeID AS nvarchar(50))
GROUP BY r.TipoTerceiro;

-- 5. Que coluna é o valor por pagar. O painel usa RemanMoedaAdop (remanescente). Se for muitas vezes diferente de Contravalor,
--    há pagamentos parciais ou moeda estrangeira: o valor certo é o remanescente, mas convém confirmar com o ERP.
SELECT SUM(CASE WHEN RemanMoedaAdop = Contravalor THEN 1 ELSE 0 END)  AS iguais,
       SUM(CASE WHEN RemanMoedaAdop <> Contravalor THEN 1 ELSE 0 END) AS diferentes,
       SUM(CASE WHEN RemanMoedaAdop IS NULL THEN 1 ELSE 0 END)        AS sem_remanescente,
       SUM(CASE WHEN DataVencimento IS NULL THEN 1 ELSE 0 END)        AS sem_vencimento
FROM   mcp.ViewMCP_resp_aberto_cli;

SELECT c.name AS coluna, TYPE_NAME(c.user_type_id) AS tipo
FROM   sys.columns AS c
WHERE  c.object_id = OBJECT_ID('mcp.ViewMCP_resp_aberto_cli')
ORDER BY c.column_id;


-- 6. Com ou sem IVA. O painel diz "valores sem IVA" nos outros ecrãs; um saldo por pagar costuma ser com IVA.
--    Faturas ainda sem pagamentos (remanescente = valor da fatura): compara com o líquido e o líquido + IVA nas vendas, no mesmo dia e cliente.
--    "com_iva" igual ao saldo = o por pagar vem com IVA.
SELECT TOP (10) r.EntidadeID, RTRIM(r.TipoDocID) AS TipoDocID, r.DataEmissao,
       CAST(r.RemanMoedaAdop AS decimal(18,2)) AS saldo_por_pagar, v.liquido, v.liquido + v.iva AS com_iva
FROM   mcp.ViewMCP_resp_aberto_cli AS r
CROSS APPLY (SELECT SUM(x.[ValorLiquido]) AS liquido, SUM(x.[ValorIva]) AS iva
             FROM   mcp.ViewMCP_vendas AS x
             WHERE  CAST(x.[ClienteID] AS nvarchar(50)) = CAST(r.EntidadeID AS nvarchar(50))
               AND  x.[DataDoc] = r.DataEmissao AND RTRIM(x.[TipoDocID]) = RTRIM(r.TipoDocID)) AS v
WHERE  r.RemanMoedaAdop = r.Contravalor AND v.liquido IS NOT NULL
ORDER BY r.DataEmissao DESC;

-- 7. Para comparar com o ERP: os 5 clientes com mais saldo. Abrir a conta corrente de cada um no ERP e ver se o total e o vencido batem.
SELECT TOP (5) EntidadeID, COUNT(*) AS linhas,
       SUM(CAST(RemanMoedaAdop AS decimal(18,2))) AS saldo_total,
       SUM(CASE WHEN DataVencimento < CAST(GETDATE() AS date) THEN CAST(RemanMoedaAdop AS decimal(18,2)) ELSE 0 END) AS vencido
FROM   mcp.ViewMCP_resp_aberto_cli
GROUP BY EntidadeID
ORDER BY saldo_total DESC;


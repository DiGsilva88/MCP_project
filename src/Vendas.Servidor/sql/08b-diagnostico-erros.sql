-- Active: 1789469768837@@192.168.90.11SQL22@1433@IAVSGIX
-- 08b - DIAGNOSTICO das faturas por pagar. So le (SELECT). Sem variaveis, sem alias com o nome de uma coluna, sem CAST.
-- Corre o ficheiro inteiro, ou uma consulta de cada vez, ligado a base IAVSGIX. Cola-me o resultado de cada uma.
-- Ja sabemos: soma de todas as linhas = 316 617,09; 3331 linhas = 3331 documentos distintos; 2 combinacoes de Aplicacao/AreaID/TipoTerceiro.

-- 1. O que sao as 2 combinacoes, e quantos saldos negativos (creditos por abater) cada uma tem
SELECT Aplicacao, AreaID, TipoTerceiro, COUNT(*) AS linhas, SUM(RemanMoedaAdop) AS saldo,
       SUM(CASE WHEN RemanMoedaAdop < 0 THEN 1 ELSE 0 END) AS negativas,
       SUM(CASE WHEN RemanMoedaAdop < 0 THEN RemanMoedaAdop ELSE 0 END) AS saldo_negativo
FROM   mcp.ViewMCP_resp_aberto_cli
GROUP BY Aplicacao, AreaID, TipoTerceiro;

-- 2. Que documentos sao, por combinacao
SELECT Aplicacao, TipoTerceiro, TipoDocID, COUNT(*) AS linhas, SUM(RemanMoedaAdop) AS saldo
FROM   mcp.ViewMCP_resp_aberto_cli
GROUP BY Aplicacao, TipoTerceiro, TipoDocID
ORDER BY linhas DESC;

-- 3. Terceiros sem ficha de cliente (um fornecedor com o mesmo codigo de um cliente seria somado ao cliente)
SELECT r.TipoTerceiro, COUNT(*) AS linhas, SUM(CASE WHEN c.ClienteID IS NULL THEN 1 ELSE 0 END) AS sem_ficha
FROM   mcp.ViewMCP_resp_aberto_cli r
LEFT JOIN mcp.ViewMCP_cliente c ON CAST(c.ClienteID AS nvarchar(50)) = CAST(r.EntidadeID AS nvarchar(50))
GROUP BY r.TipoTerceiro;

-- Depois: no ERP, ve o total de contas a receber de clientes (todos) e compara com a soma da view: 316 617,09.

-- 4. Sinal das notas de credito (N). Na view de vendas valem negativo; aqui o saldo vem positivo e nao ha nenhum negativo.
--    Se "liquido" (vendas) for negativo e o saldo por pagar positivo, o sinal perdeu-se: a nota soma-se em vez de abater.
SELECT TOP (10) r.TipoDocID, r.DataEmissao, r.DataVencimento, r.RemanMoedaAdop AS saldo_por_pagar, r.Contravalor, v.liquido, v.iva
FROM   mcp.ViewMCP_resp_aberto_cli r
CROSS APPLY (SELECT SUM(x.ValorLiquido) AS liquido, SUM(x.ValorIva) AS iva
             FROM   mcp.ViewMCP_vendas x
             WHERE  CAST(x.ClienteID AS nvarchar(50)) = CAST(r.EntidadeID AS nvarchar(50))
               AND  x.DataDoc = r.DataEmissao AND x.TipoDocID = r.TipoDocID) v
WHERE  r.TipoDocID = 'N' AND v.liquido IS NOT NULL
ORDER BY r.DataEmissao DESC;

-- 5. O mesmo para as faturas (F): o saldo por pagar bate com o liquido ou com liquido + IVA? (faturas sem pagamentos: saldo = Contravalor)
SELECT TOP (10) r.TipoDocID, r.DataEmissao, r.RemanMoedaAdop AS saldo_por_pagar, v.liquido, v.iva, v.liquido + v.iva AS com_iva
FROM   mcp.ViewMCP_resp_aberto_cli r
CROSS APPLY (SELECT SUM(x.ValorLiquido) AS liquido, SUM(x.ValorIva) AS iva
             FROM   mcp.ViewMCP_vendas x
             WHERE  CAST(x.ClienteID AS nvarchar(50)) = CAST(r.EntidadeID AS nvarchar(50))
               AND  x.DataDoc = r.DataEmissao AND x.TipoDocID = r.TipoDocID) v
WHERE  r.TipoDocID = 'F' AND r.RemanMoedaAdop = r.Contravalor AND v.liquido IS NOT NULL
ORDER BY r.DataEmissao DESC;

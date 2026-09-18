using Vendas.Servidor.Modelos;

namespace Vendas.Servidor.Tests.Modelos;

public class DimensoesTests
{
    [Theory]
    [InlineData(DimensaoCliente.Zona, "Zona")]
    [InlineData(DimensaoCliente.Vendedor, "Vendedor")]
    [InlineData(DimensaoCliente.TipoCliente, "TipoCliente")]
    [InlineData(DimensaoCliente.Actividade, "Actividade")]
    [InlineData(DimensaoCliente.Distrito, "Distrito")]
    public void Coluna_DimensaoCliente_DevolveNomeDaColunaNaView(DimensaoCliente dimensao, string colunaEsperada)
    {
        Assert.Equal(colunaEsperada, Dimensoes.Coluna(dimensao));
    }

    [Theory]
    [InlineData(DimensaoCliente.Zona, "zona")]
    [InlineData(DimensaoCliente.Vendedor, "vendedor")]
    [InlineData(DimensaoCliente.TipoCliente, "tipo_cliente")]
    [InlineData(DimensaoCliente.Actividade, "actividade")]
    [InlineData(DimensaoCliente.Distrito, "distrito")]
    public void Cabecalho_DimensaoCliente_DevolveNomeParaOCsv(DimensaoCliente dimensao, string cabecalhoEsperado)
    {
        Assert.Equal(cabecalhoEsperado, Dimensoes.Cabecalho(dimensao));
    }

    [Theory]
    [InlineData(DimensaoFaturacao.Pagamento, "Pagamento")]
    [InlineData(DimensaoFaturacao.Cobranca, "Cobranca")]
    [InlineData(DimensaoFaturacao.SituacaoFinanceira, "SitFinanceira")]
    [InlineData(DimensaoFaturacao.EscalaoPlafond, "EscalaoPlafond")]
    [InlineData(DimensaoFaturacao.EscalaoVolumeVendas, "EscalaoVolumeVendas")]
    public void Coluna_DimensaoFaturacao_DevolveNomeDaColunaNaView(DimensaoFaturacao dimensao, string colunaEsperada)
    {
        Assert.Equal(colunaEsperada, Dimensoes.Coluna(dimensao));
    }

    [Theory]
    [InlineData(DimensaoFaturacao.Pagamento, "condicao_pagamento")]
    [InlineData(DimensaoFaturacao.Cobranca, "cobranca")]
    [InlineData(DimensaoFaturacao.SituacaoFinanceira, "situacao_financeira")]
    [InlineData(DimensaoFaturacao.EscalaoPlafond, "escalao_plafond")]
    [InlineData(DimensaoFaturacao.EscalaoVolumeVendas, "escalao_volume_declarado")]
    public void Cabecalho_DimensaoFaturacao_DevolveNomeParaOCsv(DimensaoFaturacao dimensao, string cabecalhoEsperado)
    {
        Assert.Equal(cabecalhoEsperado, Dimensoes.Cabecalho(dimensao));
    }

    [Fact]
    public void Coluna_DimensaoClienteInvalida_LancaArgumentOutOfRangeException()
    {
        var invalida = (DimensaoCliente)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => Dimensoes.Coluna(invalida));
    }

    [Fact]
    public void Cabecalho_DimensaoFaturacaoInvalida_LancaArgumentOutOfRangeException()
    {
        var invalida = (DimensaoFaturacao)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => Dimensoes.Cabecalho(invalida));
    }
}

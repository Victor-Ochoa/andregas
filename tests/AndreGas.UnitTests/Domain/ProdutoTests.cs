using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;

namespace AndreGas.UnitTests.Domain;

public class ProdutoTests
{
    private static Produto CriarProduto(decimal precoVenda = 100m, decimal precoCusto = 60m, decimal precoGasDoPovo = 80m, int estoqueMinimo = 5) =>
        new("Botijão de Gás 13kg", TipoProduto.GasBotijao13, precoVenda, precoCusto, precoGasDoPovo, estoqueMinimo);

    [Fact]
    public void Construtor_DeveIniciarComEstoqueZerado()
    {
        var produto = CriarProduto();

        Assert.Equal(0, produto.QuantidadeEstoque);
    }

    [Fact]
    public void PrecoParaFormaPagamento_DeveRetornarPrecoGasDoPovo_QuandoFormaGasDoPovo()
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);

        var preco = produto.PrecoParaFormaPagamento(FormaPagamento.GasDoPovo);

        Assert.Equal(80m, preco);
    }

    [Theory]
    [InlineData(FormaPagamento.Pix)]
    [InlineData(FormaPagamento.Debito)]
    [InlineData(FormaPagamento.Credito)]
    [InlineData(FormaPagamento.Dinheiro)]
    [InlineData(FormaPagamento.Fiado)]
    public void PrecoParaFormaPagamento_DeveRetornarPrecoVenda_QuandoFormaNaoForGasDoPovo(FormaPagamento formaPagamento)
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);

        var preco = produto.PrecoParaFormaPagamento(formaPagamento);

        Assert.Equal(100m, preco);
    }

    [Fact]
    public void RegistrarEntrada_DeveAumentarEstoque()
    {
        var produto = CriarProduto();

        produto.RegistrarEntrada(10);

        Assert.Equal(10, produto.QuantidadeEstoque);
    }

    [Fact]
    public void RegistrarSaida_DeveDiminuirEstoque()
    {
        var produto = CriarProduto();
        produto.RegistrarEntrada(10);

        produto.RegistrarSaida(4);

        Assert.Equal(6, produto.QuantidadeEstoque);
    }

    [Fact]
    public void RegistrarSaida_DeveLancarExcecao_QuandoEstoqueInsuficiente()
    {
        var produto = CriarProduto();
        produto.RegistrarEntrada(5);

        Assert.Throws<InvalidOperationException>(() => produto.RegistrarSaida(6));
    }

    [Fact]
    public void EstoqueBaixo_DeveSerVerdadeiro_QuandoEstoqueMenorOuIgualAoMinimo()
    {
        var produto = CriarProduto(estoqueMinimo: 5);
        produto.RegistrarEntrada(5);

        Assert.True(produto.EstoqueBaixo);
    }

    [Fact]
    public void EstoqueBaixo_DeveSerFalso_QuandoEstoqueMaiorQueOMinimo()
    {
        var produto = CriarProduto(estoqueMinimo: 5);
        produto.RegistrarEntrada(10);

        Assert.False(produto.EstoqueBaixo);
    }

    [Fact]
    public void AjustarEstoque_DeveDefinirQuantidadeExata()
    {
        var produto = CriarProduto();
        produto.RegistrarEntrada(10);

        produto.AjustarEstoque(3);

        Assert.Equal(3, produto.QuantidadeEstoque);
    }
}

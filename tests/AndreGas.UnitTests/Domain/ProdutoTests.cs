using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;

namespace AndreGas.UnitTests.Domain;

public class ProdutoTests
{
    private static Produto CriarProduto(decimal precoVenda = 100m, decimal precoCusto = 60m, decimal precoGasDoPovo = 80m, int estoqueMinimo = 5) =>
        new("Botijão de Gás 13kg", TipoProduto.Gas, precoVenda, precoCusto, precoGasDoPovo, estoqueMinimo);

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

    [Fact]
    public void Construtor_DeveIniciarComAtivoVerdadeiro()
    {
        var produto = CriarProduto();

        Assert.True(produto.Ativo);
    }

    [Fact]
    public void Desativar_DeveMarcarProdutoComoInativo()
    {
        var produto = CriarProduto();

        produto.Desativar();

        Assert.False(produto.Ativo);
    }

    [Fact]
    public void Ativar_DeveMarcarProdutoComoAtivo()
    {
        var produto = CriarProduto();
        produto.Desativar();

        produto.Ativar();

        Assert.True(produto.Ativo);
    }

    // ===== Vasilhame =====

    [Fact]
    public void Construtor_DeveIniciarComControleDeVasilhameDesligado()
    {
        var produto = CriarProduto();

        Assert.False(produto.ControlaVasilhame);
        Assert.Equal(0, produto.QuantidadeVasilhames);
        Assert.Equal(0, produto.QuantidadeVasilhamesEmprestados);
    }

    [Fact]
    public void Construtor_ComControlaVasilhame_DeveLigarAFlag()
    {
        var produto = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m, controlaVasilhame: true);

        Assert.True(produto.ControlaVasilhame);
    }

    [Fact]
    public void RegistrarEntradaVasilhame_deve_somar_aos_vazios_disponiveis()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);

        p.RegistrarEntradaVasilhame(3);

        Assert.Equal(3, p.QuantidadeVasilhames);
        Assert.Equal(3, p.TotalVasilhames);
    }

    [Fact]
    public void RegistrarEntradaVasilhame_DeveLancarExcecao_QuandoQuantidadeMenorOuIgualAZero()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarEntradaVasilhame(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarEntradaVasilhame(-1));
    }

    [Fact]
    public void RegistrarSaidaVasilhame_DeveDiminuirOsDisponiveis()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);
        p.RegistrarEntradaVasilhame(5);

        p.RegistrarSaidaVasilhame(2);

        Assert.Equal(3, p.QuantidadeVasilhames);
    }

    [Fact]
    public void RegistrarSaidaVasilhame_DeveLancarExcecao_QuandoQuantidadeMenorOuIgualAZero()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarSaidaVasilhame(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarSaidaVasilhame(-2));
    }

    [Fact]
    public void RegistrarSaidaVasilhame_DeveLancarExcecao_QuandoAcimaDoDisponivel()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);
        p.RegistrarEntradaVasilhame(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarSaidaVasilhame(4));
    }

    [Fact]
    public void RegistrarEmprestimoVasilhame_DeveSomarAosEmprestados()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);

        p.RegistrarEmprestimoVasilhame(2);

        Assert.Equal(2, p.QuantidadeVasilhamesEmprestados);
        Assert.Equal(2, p.TotalVasilhames);
    }

    [Fact]
    public void RegistrarEmprestimoVasilhame_DeveLancarExcecao_QuandoQuantidadeMenorOuIgualAZero()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarEmprestimoVasilhame(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarEmprestimoVasilhame(-1));
    }

    [Fact]
    public void RegistrarDevolucaoVasilhame_DeveDiminuirOsEmprestados()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);
        p.RegistrarEmprestimoVasilhame(3);

        p.RegistrarDevolucaoVasilhame(2);

        Assert.Equal(1, p.QuantidadeVasilhamesEmprestados);
    }

    [Fact]
    public void RegistrarDevolucaoVasilhame_DeveLancarExcecao_QuandoQuantidadeMenorOuIgualAZero()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarDevolucaoVasilhame(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarDevolucaoVasilhame(-1));
    }

    [Fact]
    public void RegistrarDevolucaoVasilhame_DeveLancarExcecao_QuandoDevolucaoExcederEmprestados()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);
        p.RegistrarEmprestimoVasilhame(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => p.RegistrarDevolucaoVasilhame(3));
    }

    [Fact]
    public void TotalVasilhames_DeveSomarDisponiveisEEmprestados()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);
        p.RegistrarEntradaVasilhame(4);
        p.RegistrarEmprestimoVasilhame(2);

        Assert.Equal(6, p.TotalVasilhames);
    }

    [Fact]
    public void AtualizarDados_DevePersistirFlagDeControleDeVasilhame()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);

        p.AtualizarDados("Botijão 13kg", TipoProduto.Gas, 130m, 90m, 110m, 10, controlaVasilhame: true);

        Assert.Equal("Botijão 13kg", p.Nome);
        Assert.Equal(130m, p.PrecoVenda);
        Assert.True(p.ControlaVasilhame);
    }

    [Fact]
    public void AtualizarDados_ComFlagFalsa_DeveDesligarControleDeVasilhame()
    {
        var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m, controlaVasilhame: true);

        p.AtualizarDados("Botijão", TipoProduto.Gas, 120m, 80m, 100m, 5);

        Assert.False(p.ControlaVasilhame);
    }
}
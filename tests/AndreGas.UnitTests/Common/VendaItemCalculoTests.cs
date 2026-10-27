using AndreGas.Domain.Enums;
using AndreGas.Web.Common;

namespace AndreGas.UnitTests.Common;

/// <summary>
/// Testa os cálculos compartilhados de prévia de venda (Nova Venda / Editar Venda). O helper
/// deve espelhar EXATAMENTE a regra que o RegistrarVendaCommandHandler persiste — divergir aqui
/// mostraria ao operador um total diferente do gravado.
/// </summary>
public sealed class VendaItemCalculoTests
{
    private sealed record ProdutoPrecoStub(Guid Id, decimal PrecoVenda, decimal PrecoGasDoPovo) : IProdutoPreco;

    private static ProdutoPrecoStub CriarProduto(decimal precoVenda, decimal precoGasDoPovo) =>
        new(Guid.NewGuid(), precoVenda, precoGasDoPovo);

    [Fact]
    public void PrecoPadraoPara_GasDoPovo_RetornaPrecoSubsidiado()
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);

        var preco = VendaItemCalculo.PrecoPadraoPara(produto.Id, [produto], FormaPagamento.GasDoPovo);

        Assert.Equal(80m, preco);
    }

    [Fact]
    public void PrecoPadraoPara_DemaisFormas_RetornaPrecoVenda()
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);

        var preco = VendaItemCalculo.PrecoPadraoPara(produto.Id, [produto], FormaPagamento.Dinheiro);

        Assert.Equal(100m, preco);
    }

    [Fact]
    public void PrecoPadraoPara_ProdutoInexistente_RetornaZero()
    {
        var preco = VendaItemCalculo.PrecoPadraoPara(Guid.NewGuid(), [], FormaPagamento.Pix);

        Assert.Equal(0m, preco);
    }

    [Fact]
    public void PrecoSugeridoPara_PersonalizadoExiste_RetornaPersonalizado()
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);
        var precos = new Dictionary<Guid, decimal> { [produto.Id] = 95m };

        var preco = VendaItemCalculo.PrecoSugeridoPara(produto.Id, [produto], precos, FormaPagamento.Dinheiro);

        Assert.Equal(95m, preco);
    }

    [Fact]
    public void PrecoSugeridoPara_SemPersonalizado_GasDoPovo_RetornaSubsidiado()
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);

        var preco = VendaItemCalculo.PrecoSugeridoPara(produto.Id, [produto], new Dictionary<Guid, decimal>(), FormaPagamento.GasDoPovo);

        Assert.Equal(80m, preco);
    }

    [Fact]
    public void CalcularValorTotalPreview_Normal_UsaPrecoUnitarioDoItem()
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);
        var itens = new[] { (produto.Id, PrecoUnitario: 95m, Quantidade: 2) };

        var total = VendaItemCalculo.CalcularValorTotalPreview(itens, [produto], FormaPagamento.Dinheiro, desconto: 0m, valorEntrega: 0m);

        Assert.Equal(190m, total); // 95 × 2 (preço editado/personalizado vale)
    }

    [Fact]
    public void CalcularValorTotalPreview_GasDoPovo_UsaPrecoSubsidiadoDoProduto()
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);
        var itens = new[] { (produto.Id, PrecoUnitario: 95m, Quantidade: 2) };

        var total = VendaItemCalculo.CalcularValorTotalPreview(itens, [produto], FormaPagamento.GasDoPovo, desconto: 0m, valorEntrega: 0m);

        // O preço subsidiado (80) vale, não o editado (95).
        Assert.Equal(160m, total);
    }

    [Fact]
    public void CalcularValorTotalPreview_AplicaDescontoEEntrega()
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);
        var itens = new[] { (produto.Id, PrecoUnitario: 100m, Quantidade: 2) };

        var total = VendaItemCalculo.CalcularValorTotalPreview(itens, [produto], FormaPagamento.Dinheiro, desconto: 30m, valorEntrega: 15m);

        Assert.Equal(185m, total); // 200 − 30 + 15
    }

    [Fact]
    public void CalcularValorTotalPreview_DescontoMaiorQueBruto_NaoFicaNegativo()
    {
        var produto = CriarProduto(precoVenda: 100m, precoGasDoPovo: 80m);
        var itens = new[] { (produto.Id, PrecoUnitario: 100m, Quantidade: 1) };

        var total = VendaItemCalculo.CalcularValorTotalPreview(itens, [produto], FormaPagamento.Dinheiro, desconto: 200m, valorEntrega: 0m);

        Assert.Equal(0m, total);
    }
}
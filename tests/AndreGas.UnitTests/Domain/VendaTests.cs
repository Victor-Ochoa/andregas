using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;

namespace AndreGas.UnitTests.Domain;

public class VendaTests
{
    [Fact]
    public void Construtor_DeveIniciarComDescontoZeradoESemItens()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);

        Assert.Equal(0m, venda.Desconto);
        Assert.Empty(venda.Itens);
        Assert.Equal(0m, venda.ValorBruto);
        Assert.Equal(0m, venda.ValorTotal);
    }

    [Theory]
    [InlineData(FormaPagamento.Pix)]
    [InlineData(FormaPagamento.Debito)]
    [InlineData(FormaPagamento.Credito)]
    [InlineData(FormaPagamento.Dinheiro)]
    [InlineData(FormaPagamento.GasDoPovo)]
    public void Status_DeveSerPago_QuandoFormaPagamentoNaoForFiado(FormaPagamento formaPagamento)
    {
        var venda = new Venda(Guid.NewGuid(), formaPagamento);

        Assert.Equal(VendaStatus.Pago, venda.Status);
    }

    [Fact]
    public void Status_DeveSerFiadoAberto_QuandoFormaPagamentoForFiado()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Fiado);

        Assert.Equal(VendaStatus.FiadoAberto, venda.Status);
    }

    [Fact]
    public void AdicionarItem_DeveAcumularValorBrutoELucroBruto()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);

        // Botijão: vende a 100, custa 60 -> lucro bruto 40 por unidade.
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 2, precoUnitario: 100m, precoCustoUnitario: 60m);
        // Água: vende a 10, custa 6 -> lucro bruto 4 por unidade.
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 3, precoUnitario: 10m, precoCustoUnitario: 6m);

        Assert.Equal(230m, venda.ValorBruto); // 2*100 + 3*10
        Assert.Equal(230m, venda.ValorTotal); // sem desconto
        Assert.Equal(92m, venda.LucroTotal);  // 2*40 + 3*4
    }

    [Fact]
    public void AplicarDesconto_DeveAbaterDoValorTotal()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 60m);

        venda.AplicarDesconto(10m);

        Assert.Equal(100m, venda.ValorBruto);
        Assert.Equal(90m, venda.ValorTotal);
    }

    [Fact]
    public void AplicarDesconto_DeveRatearProporcionalmenteEntreItensReduzindoLucro()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);
        // Item A: valor 300 (75% do total), lucro bruto 120.
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 3, precoUnitario: 100m, precoCustoUnitario: 60m);
        // Item B: valor 100 (25% do total), lucro bruto 40.
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 60m);

        venda.AplicarDesconto(40m); // 40 * 75% = 30 para A, 40 * 25% = 10 para B

        var itens = venda.Itens.ToList();
        Assert.Equal(120m, itens[0].LucroBruto);
        Assert.Equal(30m, itens[0].DescontoRateado);
        Assert.Equal(90m, itens[0].Lucro);

        Assert.Equal(40m, itens[1].LucroBruto);
        Assert.Equal(10m, itens[1].DescontoRateado);
        Assert.Equal(30m, itens[1].Lucro);

        Assert.Equal(360m, venda.ValorTotal); // 400 - 40
        Assert.Equal(120m, venda.LucroTotal); // 90 + 30
    }

    [Fact]
    public void AplicarDesconto_ComRateioNaoExato_UltimoItemAbsorveResiduoDeArredondamento()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);
        // Três itens de valor igual (33.33...% cada) para forçar arredondamento.
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 50m);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 50m);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 50m);

        venda.AplicarDesconto(10m);

        var itens = venda.Itens.ToList();
        var somaDescontoRateado = itens.Sum(i => i.DescontoRateado);

        // O total rateado deve bater exatamente com o desconto aplicado, sem perder centavos.
        Assert.Equal(10m, somaDescontoRateado);
        Assert.Equal(290m, venda.ValorTotal);
        Assert.Equal(140m, venda.LucroTotal); // 150 de lucro bruto - 10 de desconto
    }

    [Fact]
    public void AplicarDesconto_DeveRecalcularRateio_QuandoNovoItemAdicionadoDepois()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 60m);
        venda.AplicarDesconto(20m);

        // Adicionar um segundo item deve recalcular o rateio do desconto já aplicado.
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 60m);

        var itens = venda.Itens.ToList();
        Assert.Equal(10m, itens[0].DescontoRateado);
        Assert.Equal(10m, itens[1].DescontoRateado);
        Assert.Equal(180m, venda.ValorTotal); // 200 - 20
    }

    [Fact]
    public void AplicarDesconto_DeveLancarExcecao_QuandoDescontoNegativo()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 60m);

        Assert.Throws<ArgumentOutOfRangeException>(() => venda.AplicarDesconto(-1m));
    }

    [Fact]
    public void AplicarDesconto_DeveLancarExcecao_QuandoDescontoMaiorQueValorBruto()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 60m);

        Assert.Throws<ArgumentOutOfRangeException>(() => venda.AplicarDesconto(101m));
    }

    [Fact]
    public void AdicionarItem_DeveLancarExcecao_QuandoQuantidadeNaoPositiva()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);

        Assert.Throws<ArgumentOutOfRangeException>(() => venda.AdicionarItem(Guid.NewGuid(), 0, 100m, 60m));
    }

    [Fact]
    public void Construtor_DeveIniciarComValorEntregaZerado()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);

        Assert.Equal(0m, venda.ValorEntrega);
    }

    [Fact]
    public void DefinirValorEntrega_DeveSomarAoValorTotalSemAlterarLucro()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 2, precoUnitario: 100m, precoCustoUnitario: 60m);

        venda.DefinirValorEntrega(15m);

        Assert.Equal(200m, venda.ValorBruto);
        Assert.Equal(215m, venda.ValorTotal); // 200 + 15
        Assert.Equal(80m, venda.LucroTotal);  // lucro não muda (entrega não é item)
    }

    [Fact]
    public void DefinirValorEntrega_DeveLancarExcecao_QuandoNegativo()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);

        Assert.Throws<ArgumentOutOfRangeException>(() => venda.DefinirValorEntrega(-1m));
    }

    [Fact]
    public void ValorTotal_ComDescontoEEntrega_DeveAplicarAmbos()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 2, precoUnitario: 100m, precoCustoUnitario: 60m);

        venda.AplicarDesconto(20m);
        venda.DefinirValorEntrega(10m);

        Assert.Equal(200m, venda.ValorBruto);
        Assert.Equal(190m, venda.ValorTotal); // 200 - 20 + 10
        Assert.Equal(60m, venda.LucroTotal);  // 80 - 20 (entrega não entra)
    }

    [Fact]
    public void DefinirVendedor_DeveRegistrarVendedorId()
    {
        var venda = new Venda(Guid.NewGuid(), FormaPagamento.Dinheiro);
        var vendedorId = Guid.NewGuid();

        venda.DefinirVendedor(vendedorId);

        Assert.Equal(vendedorId, venda.VendedorId);
    }
}
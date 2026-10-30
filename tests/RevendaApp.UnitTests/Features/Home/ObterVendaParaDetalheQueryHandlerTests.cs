using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Home;

namespace RevendaApp.UnitTests.Features.Home;

public class ObterVendaParaDetalheQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarNull_QuandoVendaNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new ObterVendaParaDetalheQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterVendaParaDetalheQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_DeveRetornarDadosDaVendaEItensComProduto()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(produto.Id, 2, 100m, 60m);
        venda.AplicarDesconto(20m);
        venda.DefinirValorEntrega(15m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterVendaParaDetalheQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterVendaParaDetalheQuery(venda.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(cliente.Id, result!.ClienteId);
        Assert.Equal("Maria Souza", result.ClienteNome);
        Assert.Equal("11988887777", result.ClienteTelefone);
        Assert.Equal(FormaPagamento.Dinheiro, result.FormaPagamento);
        Assert.Equal(VendaStatus.Pago, result.Status);
        Assert.Equal(20m, result.Desconto);
        Assert.Equal(15m, result.ValorEntrega);
        Assert.Equal(195m, result.ValorTotal);      // 200 - 20 + 15
        Assert.Equal(60m, result.LucroTotal);       // 2*(100-60) - 20 rateado

        var item = Assert.Single(result!.Itens);
        Assert.Equal(produto.Id, item.ProdutoId);
        Assert.Equal("Botijão 13kg", item.ProdutoNome);
        Assert.Equal(2, item.Quantidade);
        Assert.Equal(100m, item.PrecoUnitario);
        Assert.False(item.EhEmprestimo);
    }

    [Fact]
    public async Task Handle_DeveRetornarNull_QuandoVendaFoiExcluida()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), 1, 100m, 60m);
        venda.Excluir();
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterVendaParaDetalheQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterVendaParaDetalheQuery(venda.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_DeveMarcarItemEhEmprestimo()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(produto.Id, 1, 100m, 60m, ehEmprestimo: true);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterVendaParaDetalheQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterVendaParaDetalheQuery(venda.Id), CancellationToken.None);

        Assert.NotNull(result);
        var item = Assert.Single(result!.Itens);
        Assert.True(item.EhEmprestimo);
    }
}
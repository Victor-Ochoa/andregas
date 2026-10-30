using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Admin.Vendas;

namespace RevendaApp.UnitTests.Features.Admin.Vendas;

/// <summary>
/// Cobre a reversão de venda em cenários auto-contidos, incluindo o **buraco** do produto
/// removido do catálogo: a reversão deve ser no-op segura (não lançar, não quebrar FK, não
/// estornar estoque inexistente) quando um item aponta para um produto que não existe mais.
/// </summary>
public class ReversaoVendaHelperTests
{
    [Fact]
    public async Task ReverterAsync_ProdutoRemovidoDoCatalogo_DeveNoOpSemLancar()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        // Venda de 2 com produto que NÃO existe no catálogo (nunca foi cadastrado/excluído).
        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), 2, 100m, 60m);
        db.Vendas.Add(venda);
        await db.SaveChangesAsync();

        // Reverter (ex.: na exclusão da venda) não pode lançar nem criar registros órfãos.
        await ReversaoVendaHelper.ReverterAsync(db, venda, CancellationToken.None);
        await db.SaveChangesAsync();

        // No-op: NÃO há movimentação de estorno criada (não existe estoque a devolver/FK).
        Assert.Empty(db.MovimentacoesEstoque);
        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task ReverterAsync_ProdutoExistente_DeveEstornarEstoqueECriarMovimentacaoDeEntrada()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        produto.RegistrarEntrada(20);
        produto.RegistrarSaida(2); // vendeu 2
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(produto.Id, 2, 100m, 60m);
        db.Vendas.Add(venda);
        await db.SaveChangesAsync();

        Assert.Equal(18, produto.QuantidadeEstoque);

        await ReversaoVendaHelper.ReverterAsync(db, venda, CancellationToken.None);
        await db.SaveChangesAsync();

        // Estorno: estoque volta para 20; 1 movimentação de entrada criada.
        Assert.Equal(20, (await db.Produtos.FindAsync(produto.Id))!.QuantidadeEstoque);
        var mov = Assert.Single(db.MovimentacoesEstoque);
        Assert.Equal(TipoMovimentacaoEstoque.Entrada, mov.Tipo);
        Assert.Equal(2, mov.Quantidade);
    }

    [Fact]
    public async Task ReverterAsync_VendaFiado_DeveAbaterSaldoDevedor()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        produto.RegistrarEntrada(10);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);

        var venda = new Venda(cliente.Id, FormaPagamento.Fiado);
        venda.AdicionarItem(produto.Id, 1, 100m, 60m);
        venda.AplicarDesconto(10m); // 90 fiado
        db.Vendas.Add(venda);
        cliente.AdicionarSaldoDevedor(90m);
        await db.SaveChangesAsync();

        await ReversaoVendaHelper.ReverterAsync(db, venda, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(0m, (await db.Clientes.FindAsync(cliente.Id))!.SaldoDevedor);
    }

    [Fact]
    public async Task ReverterAsync_VendaNaoFiado_DeveRemoverPagamentoVinculado()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), 1, 100m, 60m);
        db.Vendas.Add(venda);
        db.Pagamentos.Add(new Pagamento(cliente.Id, 100m, FormaPagamento.Dinheiro, "Venda", vendaId: venda.Id));
        await db.SaveChangesAsync();

        Assert.Single(db.Pagamentos);

        await ReversaoVendaHelper.ReverterAsync(db, venda, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Empty(db.Pagamentos);
    }
}
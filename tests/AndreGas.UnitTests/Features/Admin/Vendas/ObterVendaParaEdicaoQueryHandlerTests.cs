using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Admin.Vendas;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Features.Admin.Vendas;

public class ObterVendaParaEdicaoQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarNull_QuandoVendaNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new ObterVendaParaEdicaoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterVendaParaEdicaoQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_DeveRetornarNull_QuandoVendaExcluida()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), 1, 100m, 60m);
        venda.Excluir();
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterVendaParaEdicaoQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterVendaParaEdicaoQuery(venda.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_DeveRetornarDadosDaVenda_ComItemEProduto()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        cliente.AdicionarSaldoDevedor(50m);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);

        var venda = new Venda(cliente.Id, FormaPagamento.Fiado, dataHora: new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc));
        venda.AdicionarItem(produto.Id, 2, 100m, 60m);
        venda.AplicarDesconto(20m);
        venda.DefinirValorEntrega(15m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterVendaParaEdicaoQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterVendaParaEdicaoQuery(venda.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(cliente.Id, result!.ClienteId);
        Assert.Equal("Maria Souza", result.ClienteNome);
        Assert.Equal("11988887777", result.ClienteTelefone);
        Assert.Equal(FormaPagamento.Fiado, result.FormaPagamento);
        Assert.Equal(VendaStatus.FiadoAberto, result.Status);
        Assert.Equal(20m, result.Desconto);
        Assert.Equal(15m, result.ValorEntrega);
        Assert.Equal(50m, result.SaldoDevedor);
        Assert.Equal("2026-09-15T11:00:00", result.DataHora.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)); // UTC → Brasília (UTC−3)

        var item = Assert.Single(result!.Itens);
        Assert.Equal(produto.Id, item.ProdutoId);
        Assert.Equal("Botijão 13kg", item.ProdutoNome);
        Assert.Equal(2, item.Quantidade);
        Assert.Equal(100m, item.PrecoUnitario);
        Assert.False(item.EhEmprestimo);
    }

    // NOTA: o fallback `?? "Produto removido"` do handler (i.Produto?.Nome) é DEFENSIVO e
    // inalcançável via ORM: a FK ItemVenda→Produto é DeleteBehavior.Restrict e o
    // `Include(v => v.Itens).ThenInclude(i => i.Produto)` gera INNER JOIN — um item com produto
    // inexistente nem chega ao resultado. Nenhuma forma de reproduzir isso com o provider InMemory
    // (que mantém a integridade referencial da relação). O cenário análogo realmente possível —
    // produto removido na reversão de venda — está coberto em ReversaoVendaHelperTests.
}
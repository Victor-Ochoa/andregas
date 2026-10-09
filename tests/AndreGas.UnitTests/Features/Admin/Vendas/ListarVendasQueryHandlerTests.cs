using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Admin.Vendas;

namespace AndreGas.UnitTests.Features.Admin.Vendas;

public class ListarVendasQueryHandlerTests
{
    private static async Task<AndreGas.Infrastructure.AppDbContext> CriarContextoComVendasAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        produto.RegistrarEntrada(10);
        db.Produtos.Add(produto);

        // Venda ativa.
        var vendaAtiva = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        vendaAtiva.AdicionarItem(produto.Id, 2, precoUnitario: 100m, precoCustoUnitario: 60m);
        db.Vendas.Add(vendaAtiva);

        // Venda excluída.
        var vendaExcluida = new Venda(cliente.Id, FormaPagamento.Fiado);
        vendaExcluida.AdicionarItem(produto.Id, 1, precoUnitario: 100m, precoCustoUnitario: 60m);
        vendaExcluida.Excluir();
        db.Vendas.Add(vendaExcluida);

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Handle_DeveListarSomenteVendasNaoExcluidas()
    {
        using var db = await CriarContextoComVendasAsync();
        var handler = new ListarVendasQueryHandler(db);

        var resultado = await handler.Handle(new ListarVendasQuery(), CancellationToken.None);

        Assert.Single(resultado);
        Assert.Contains(resultado, v => v.FormaPagamento == FormaPagamento.Dinheiro);
    }

    [Fact]
    public async Task Handle_DeveOrdenarPelaDataMaisRecentePrimeiro()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        produto.RegistrarEntrada(10);
        db.Produtos.Add(produto);

        var vendaAntiga = new Venda(cliente.Id, FormaPagamento.Dinheiro, dataHora: DateTime.UtcNow.AddDays(-10));
        vendaAntiga.AdicionarItem(produto.Id, 1, 100m, 60m);
        var vendaRecente = new Venda(cliente.Id, FormaPagamento.Pix, dataHora: DateTime.UtcNow.AddDays(-1));
        vendaRecente.AdicionarItem(produto.Id, 2, 100m, 60m);

        db.Vendas.AddRange(vendaAntiga, vendaRecente);
        await db.SaveChangesAsync();

        var handler = new ListarVendasQueryHandler(db);
        var resultado = await handler.Handle(new ListarVendasQuery(), CancellationToken.None);

        Assert.Equal(2, resultado.Count);
        Assert.Equal(vendaRecente.Id, resultado[0].Id);
        Assert.Equal(vendaAntiga.Id, resultado[1].Id);
    }

    [Fact]
    public async Task Handle_DeveRetornarDadosDoItemEValorTotal()
    {
        using var db = await CriarContextoComVendasAsync();
        var handler = new ListarVendasQueryHandler(db);

        var resultado = await handler.Handle(new ListarVendasQuery(), CancellationToken.None);

        var item = Assert.Single(resultado);
        Assert.Equal("Maria Souza", item.ClienteNome);
        Assert.Equal(200m, item.ValorTotal);
        Assert.Equal(2, item.QuantidadeItens);
    }
}
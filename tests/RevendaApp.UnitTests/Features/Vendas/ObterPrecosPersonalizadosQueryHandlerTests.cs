using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Vendas.NovaVenda;

namespace RevendaApp.UnitTests.Features.Vendas;

public class ObterPrecosPersonalizadosQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarPrecosPersonalizadosDoCliente_MapeadosPorProdutoId()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        var cliente = new Cliente("Restaurante A", "11988887777", "Rua A, 1");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);
        db.PrecosPersonalizadosClientes.Add(new PrecoPersonalizadoCliente(cliente.Id, produto.Id, 90m));
        await db.SaveChangesAsync();

        var handler = new ObterPrecosPersonalizadosQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterPrecosPersonalizadosQuery(cliente.Id), CancellationToken.None);

        var preco = Assert.Single(result);
        Assert.Equal(produto.Id, preco.Key);
        Assert.Equal(90m, preco.Value);
    }

    [Fact]
    public async Task Handle_DeveIgnorarPrecosDeOutrosClientes()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        var cliente1 = new Cliente("Restaurante A", "11988887777", "Rua A, 1");
        var cliente2 = new Cliente("Restaurante B", "11977776666", "Rua B, 2");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        db.Clientes.AddRange(cliente1, cliente2);
        db.Produtos.Add(produto);
        db.PrecosPersonalizadosClientes.Add(new PrecoPersonalizadoCliente(cliente1.Id, produto.Id, 90m));
        db.PrecosPersonalizadosClientes.Add(new PrecoPersonalizadoCliente(cliente2.Id, produto.Id, 85m));
        await db.SaveChangesAsync();

        var handler = new ObterPrecosPersonalizadosQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterPrecosPersonalizadosQuery(cliente2.Id), CancellationToken.None);

        var preco = Assert.Single(result);
        Assert.Equal(85m, preco.Value);
    }

    [Fact]
    public async Task Handle_DeveRetornarVazio_QuandoClienteNaoTemAcordos()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        var cliente = new Cliente("Restaurante A", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new ObterPrecosPersonalizadosQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterPrecosPersonalizadosQuery(cliente.Id), CancellationToken.None);

        Assert.Empty(result);
    }
}
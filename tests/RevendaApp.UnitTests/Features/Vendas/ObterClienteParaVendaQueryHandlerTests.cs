using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.Infrastructure;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Vendas.NovaVenda;

namespace RevendaApp.UnitTests.Features.Vendas;

public class ObterClienteParaVendaQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarClienteComUltimaValorEntrega()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11999999999", "Rua A, 10");
        cliente.AdicionarSaldoDevedor(50m);
        db.Clientes.Add(cliente);

        // Venda com entrega (recente) e outra sem (mais antiga) — deve usar a mais recente.
        var vendaRecente = new Venda(cliente.Id, FormaPagamento.Dinheiro, DateTime.UtcNow.AddDays(-2));
        vendaRecente.AdicionarItem(Guid.NewGuid(), 1, 100m, 60m);
        vendaRecente.DefinirValorEntrega(10m);
        var vendaAntiga = new Venda(cliente.Id, FormaPagamento.Dinheiro, DateTime.UtcNow.AddDays(-30));
        vendaAntiga.AdicionarItem(Guid.NewGuid(), 1, 100m, 60m);
        vendaAntiga.DefinirValorEntrega(5m);
        db.Vendas.AddRange(vendaRecente, vendaAntiga);
        await db.SaveChangesAsync();

        var handler = new ObterClienteParaVendaQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterClienteParaVendaQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(cliente.Id, result!.Id);
        Assert.Equal("Maria Souza", result.Nome);
        Assert.Equal("Rua A, 10", result.Endereco);
        Assert.Equal(50m, result.SaldoDevedor);
        Assert.Equal(10m, result.UltimaValorEntrega);
    }

    [Fact]
    public async Task Handle_ClienteInexistente_DeveRetornarNulo()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        var handler = new ObterClienteParaVendaQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterClienteParaVendaQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
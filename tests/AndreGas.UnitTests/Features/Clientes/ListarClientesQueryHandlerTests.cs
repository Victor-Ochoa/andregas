using AndreGas.Domain.Entities;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Listar;

namespace AndreGas.UnitTests.Features.Clientes;

public class ListarClientesQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarClientesOrdenadosPorNome()
    {
        using var db = InMemoryDbContextFactory.Create();
        db.Clientes.AddRange(
            new Cliente("Zeca Alves", "11911111111", "Rua Z, 1"),
            new Cliente("Ana Paula", "11922222222", "Rua A, 2"));
        await db.SaveChangesAsync();

        var handler = new ListarClientesQueryHandler(db);

        var result = await handler.Handle(new ListarClientesQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Ana Paula", result[0].Nome);
        Assert.Equal("Zeca Alves", result[1].Nome);
    }

    [Fact]
    public async Task Handle_DeveRetornarListaVazia_QuandoNaoHaClientes()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new ListarClientesQueryHandler(db);

        var result = await handler.Handle(new ListarClientesQuery(), CancellationToken.None);

        Assert.Empty(result);
    }
}
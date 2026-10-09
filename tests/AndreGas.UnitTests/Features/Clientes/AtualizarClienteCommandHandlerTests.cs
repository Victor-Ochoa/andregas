using AndreGas.Domain.Entities;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Detalhe;

namespace AndreGas.UnitTests.Features.Clientes;

public class AtualizarClienteCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeveAtualizarDadosDoCliente()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new AtualizarClienteCommandHandler(db);

        var sucesso = await handler.Handle(new AtualizarClienteCommand(cliente.Id, "Maria S. Oliveira", "11999998888", "Rua B, 2"), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Clientes.FindAsync(cliente.Id);
        Assert.Equal("Maria S. Oliveira", atualizado!.Nome);
        Assert.Equal("11999998888", atualizado.Telefone);
        Assert.Equal("Rua B, 2", atualizado.Endereco);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalso_QuandoClienteNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new AtualizarClienteCommandHandler(db);

        var sucesso = await handler.Handle(new AtualizarClienteCommand(Guid.NewGuid(), "X", "11988887777", "Rua A"), CancellationToken.None);

        Assert.False(sucesso);
    }
}
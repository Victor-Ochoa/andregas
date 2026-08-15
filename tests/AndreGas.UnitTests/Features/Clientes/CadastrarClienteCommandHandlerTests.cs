using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Cadastrar;

namespace AndreGas.UnitTests.Features.Clientes;

public class CadastrarClienteCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeveCriarClienteComSaldoZerado()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new CadastrarClienteCommandHandler(db);

        var id = await handler.Handle(new CadastrarClienteCommand("Maria Souza", "(11) 98888-7777", "Rua A, 1"), CancellationToken.None);

        var cliente = await db.Clientes.FindAsync(id);
        Assert.NotNull(cliente);
        Assert.Equal("Maria Souza", cliente!.Nome);
        Assert.Equal("11988887777", cliente.Telefone);
        Assert.Equal("Rua A, 1", cliente.Endereco);
        Assert.Equal(0m, cliente.SaldoDevedor);
        Assert.True(cliente.Ativo);
    }
}

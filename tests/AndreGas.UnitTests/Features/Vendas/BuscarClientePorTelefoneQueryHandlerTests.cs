using AndreGas.Domain.Entities;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.UnitTests.Features.Vendas;

public class BuscarClientePorTelefoneQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarCliente_QuandoTelefoneCadastradoComFormatacaoDiferente()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        cliente.AdicionarSaldoDevedor(50m);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTelefoneQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTelefoneQuery("(11) 98888-7777"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Maria Souza", result!.Nome);
        Assert.Equal(50m, result.SaldoDevedor);
    }

    [Fact]
    public async Task Handle_DeveRetornarNull_QuandoTelefoneNaoCadastrado()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new BuscarClientePorTelefoneQueryHandler(db);

        var result = await handler.Handle(new BuscarClientePorTelefoneQuery("11900000000"), CancellationToken.None);

        Assert.Null(result);
    }
}

using AndreGas.Web.Features.Clientes.Cadastrar;
using AndreGas.Web.Features.Clientes.Detalhe;
using AndreGas.Web.Features.Clientes.Listar;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração dos handlers de Clientes contra um Postgres real (via
/// <see cref="DatabaseFixture"/>), cobrindo cadastro, listagem, detalhe e atualização.
/// </summary>
public class ClientesFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Reset completo (não só Clientes): o Postgres do AppHost usa volume persistente, então
        // outras classes de teste podem ter deixado dados (ex.: Vendas referenciando Clientes).
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CadastrarEListar_DeveExibirClienteRecemCadastrado()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarClienteCommandHandler(db);
        var listarHandler = new ListarClientesQueryHandler(db);

        await cadastrarHandler.Handle(new CadastrarClienteCommand("Maria Souza", "11988887777", "Rua A, 1"), CancellationToken.None);

        var clientes = await listarHandler.Handle(new ListarClientesQuery(), CancellationToken.None);

        var cliente = Assert.Single(clientes);
        Assert.Equal("Maria Souza", cliente.Nome);
        Assert.Equal("11988887777", cliente.Telefone);
        Assert.Equal(0m, cliente.SaldoDevedor);
    }

    [Fact]
    public async Task CadastrarClienteComTelefoneDuplicado_DeveSerRejeitadoPeloValidador()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarClienteCommandHandler(db);
        await cadastrarHandler.Handle(new CadastrarClienteCommand("Maria Souza", "11988887777", "Rua A, 1"), CancellationToken.None);

        var validator = new CadastrarClienteCommandValidator(db);
        var resultado = await validator.ValidateAsync(new CadastrarClienteCommand("Outra Pessoa", "11988887777", "Rua B, 2"));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public async Task ObterDetalhe_DeveRefletirAtualizacaoDeDados()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarClienteCommandHandler(db);
        var id = await cadastrarHandler.Handle(new CadastrarClienteCommand("Maria Souza", "11988887777", "Rua A, 1"), CancellationToken.None);

        var atualizarHandler = new AtualizarClienteCommandHandler(db);
        await atualizarHandler.Handle(new AtualizarClienteCommand(id, "Maria S. Oliveira", "11999998888", "Rua B, 2"), CancellationToken.None);

        var detalheHandler = new ObterClienteDetalheQueryHandler(db);
        var detalhe = await detalheHandler.Handle(new ObterClienteDetalheQuery(id), CancellationToken.None);

        Assert.NotNull(detalhe);
        Assert.Equal("Maria S. Oliveira", detalhe!.Nome);
        Assert.Equal("11999998888", detalhe.Telefone);
        Assert.Equal("Rua B, 2", detalhe.Endereco);
    }
}
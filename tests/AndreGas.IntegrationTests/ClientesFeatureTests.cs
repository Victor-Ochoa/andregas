using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Clientes.Cadastrar;
using AndreGas.Web.Features.Clientes.Detalhe;
using AndreGas.Web.Features.Clientes.Listar;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Movimentar;
using AndreGas.Web.Features.Vendas.NovaVenda;
using AndreGas.IntegrationTests.TestHelpers;

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
        var cadastrarHandler = new CadastrarClienteCommandHandler(db, TenantIntegracao.Contexto());
        var listarHandler = new ListarClientesQueryHandler(db, TenantIntegracao.Contexto());

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
        var cadastrarHandler = new CadastrarClienteCommandHandler(db, TenantIntegracao.Contexto());
        await cadastrarHandler.Handle(new CadastrarClienteCommand("Maria Souza", "11988887777", "Rua A, 1"), CancellationToken.None);

        var validator = new CadastrarClienteCommandValidator(db);
        var resultado = await validator.ValidateAsync(new CadastrarClienteCommand("Outra Pessoa", "11988887777", "Rua B, 2"));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public async Task ObterDetalhe_DeveRefletirAtualizacaoDeDados()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarClienteCommandHandler(db, TenantIntegracao.Contexto());
        var id = await cadastrarHandler.Handle(new CadastrarClienteCommand("Maria Souza", "11988887777", "Rua A, 1"), CancellationToken.None);

        var atualizarHandler = new AtualizarClienteCommandHandler(db, TenantIntegracao.Contexto());
        await atualizarHandler.Handle(new AtualizarClienteCommand(id, "Maria S. Oliveira", "11999998888", "Rua B, 2"), CancellationToken.None);

        var detalheHandler = new ObterClienteDetalheQueryHandler(db, TenantIntegracao.Contexto());
        var detalhe = await detalheHandler.Handle(new ObterClienteDetalheQuery(id), CancellationToken.None);

        Assert.NotNull(detalhe);
        Assert.Equal("Maria S. Oliveira", detalhe!.Nome);
        Assert.Equal("11999998888", detalhe.Telefone);
        Assert.Equal("Rua B, 2", detalhe.Endereco);
    }

    [Fact]
    public async Task ObterDetalhe_DeveExibirNomeRealDoProdutoComprado()
    {
        // Regressão: o handler agrupava ProdutosComprados por i.Produto?.Nome ?? "Produto removido".
        // No app real, o handler do detalhe roda num AppDbContext novo (por request), sem o produto
        // trackeado — sem .ThenInclude(i => i.Produto) a navegação fica null e todo produto aparece
        // como "Produto removido". Usa dois contexts separados para simular request de escrita e
        // request de leitura, como no app.
        Guid clienteId;
        using (var db = fixture.CreateDbContext())
        {
            var produtoId = await new CadastrarProdutoCommandHandler(db, TenantIntegracao.Contexto())
                .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
            await new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantIntegracao.Contexto())
                .Handle(new RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

            var vendaHandler = new RegistrarVendaCommandHandler(db, TenantIntegracao.Contexto());
            var venda = await vendaHandler.Handle(
                new RegistrarVendaCommand(null, "Maria Souza", "11988887777", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                    [new ItemVendaInput(produtoId, 2)]),
                CancellationToken.None);
            clienteId = venda.ClienteId;
        }

        // Context fresco (como a request do detalhe) — sem produto trackeado.
        using var dbLeitura = fixture.CreateDbContext();
        var detalheHandler = new ObterClienteDetalheQueryHandler(dbLeitura, TenantIntegracao.Contexto());
        var detalhe = await detalheHandler.Handle(new ObterClienteDetalheQuery(clienteId), CancellationToken.None);

        var produtoComprado = Assert.Single(detalhe!.ProdutosComprados);
        Assert.Equal("Botijão 13kg", produtoComprado.ProdutoNome);
        Assert.Equal(2, produtoComprado.QuantidadeTotal);
    }
}
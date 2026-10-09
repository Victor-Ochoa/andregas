using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Features.Admin.Vendas;

namespace AndreGas.UnitTests.Features.Admin.Vendas;

public class ExcluirVendaCommandHandlerTests
{
    private static async Task<(AndreGas.Infrastructure.AppDbContext Db, Cliente Cliente, Produto Produto, Venda Venda)>
        CriarVendaAsync(FormaPagamento forma = FormaPagamento.Dinheiro, int quantidade = 2, decimal valorEntrega = 0m)
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        produto.RegistrarEntrada(20);
        db.Produtos.Add(produto);

        var venda = new Venda(cliente.Id, forma);
        venda.AdicionarItem(produto.Id, quantidade, precoUnitario: 100m, precoCustoUnitario: 60m);
        venda.DefinirValorEntrega(valorEntrega);

        db.Vendas.Add(venda);
        produto.RegistrarSaida(quantidade);
        if (forma == FormaPagamento.Fiado)
        {
            cliente.AdicionarSaldoDevedor(venda.ValorTotal);
        }
        else
        {
            db.Pagamentos.Add(new Pagamento(cliente.Id, venda.ValorTotal, forma, "Venda", vendaId: venda.Id));
        }

        await db.SaveChangesAsync();
        return (db, cliente, produto, venda);
    }

    [Fact]
    public async Task Handle_DeveExcluirLogicamente_EstornandoEstoqueESaldoFiado()
    {
        var (db, cliente, produto, venda) = await CriarVendaAsync(forma: FormaPagamento.Fiado, quantidade: 2); // 200 fiado
        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        var resultado = await handler.Handle(new ExcluirVendaCommand(venda.Id), CancellationToken.None);

        Assert.True(resultado.Sucesso);

        // Estoque devolvido: 20 - 2 + 2 = 20.
        var produtoAtual = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(20, produtoAtual!.QuantidadeEstoque);

        // Saldo fiado abatido: 200 - 200 = 0.
        var clienteAtual = await db.Clientes.FindAsync(cliente.Id);
        Assert.Equal(0m, clienteAtual!.SaldoDevedor);

        // Venda permanece no banco, mas marcada como excluída.
        var vendaAtual = await db.Vendas.FindAsync(venda.Id);
        Assert.NotNull(vendaAtual!.ExcluidaEm);
    }

    [Fact]
    public async Task Handle_DeveRemoverPagamentoDaVendaNaoFiado()
    {
        var (db, _, _, venda) = await CriarVendaAsync(forma: FormaPagamento.Dinheiro, quantidade: 2); // 200, pagamento 200
        Assert.Single(db.Pagamentos);

        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));
        await handler.Handle(new ExcluirVendaCommand(venda.Id), CancellationToken.None);

        Assert.Empty(db.Pagamentos);
    }

    [Fact]
    public async Task Handle_DeveBloquearExclusao_QuandoSaldoFiadoNaoCobreValor()
    {
        var (db, cliente, _, venda) = await CriarVendaAsync(forma: FormaPagamento.Fiado, quantidade: 2); // 200 fiado
        // Simula saldo já reduzido (cliente pagou parte): saldo 50 < 200.
        cliente.RegistrarPagamento(150m);
        await db.SaveChangesAsync();

        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ExcluirVendaCommand(venda.Id), CancellationToken.None).AsTask());

        // Nada mudou: venda segue ativa.
        Assert.Null((await db.Vendas.FindAsync(venda.Id))!.ExcluidaEm);
    }

    [Fact]
    public async Task Handle_DeveBloquearExclusao_QuandoVendaJaExcluida()
    {
        var (db, _, _, venda) = await CriarVendaAsync();
        venda.Excluir();
        await db.SaveChangesAsync();

        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ExcluirVendaCommand(venda.Id), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoVendaNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ExcluirVendaCommand(Guid.NewGuid()), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoUsuarioNaoAdmin()
    {
        var (db, _, _, venda) = await CriarVendaAsync();
        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ExcluirVendaCommand(venda.Id), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_DeveEstornarEstoque_QuandoNaoFiado()
    {
        var (db, _, produto, venda) = await CriarVendaAsync(forma: FormaPagamento.Dinheiro, quantidade: 3); // 300
        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await handler.Handle(new ExcluirVendaCommand(venda.Id), CancellationToken.None);

        // Estoque: 20 - 3 + 3 = 20.
        var produtoAtual = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(20, produtoAtual!.QuantidadeEstoque);
    }
}
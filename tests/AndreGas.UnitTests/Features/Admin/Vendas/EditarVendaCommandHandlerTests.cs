using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Features.Admin.Vendas;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.UnitTests.Features.Admin.Vendas;

public class EditarVendaCommandHandlerTests
{
    private static async Task<(AndreGas.Infrastructure.AppDbContext Db, Cliente Cliente, Produto Produto, Venda Venda)>
        CriarVendaAsync(FormaPagamento forma = FormaPagamento.Dinheiro, decimal desconto = 0m, int quantidade = 2, decimal valorEntrega = 0m)
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        produto.RegistrarEntrada(20);
        db.Produtos.Add(produto);

        var venda = new Venda(cliente.Id, forma);
        venda.AdicionarItem(produto.Id, quantidade, precoUnitario: 100m, precoCustoUnitario: 60m);
        if (desconto > 0)
        {
            venda.AplicarDesconto(desconto);
        }

        venda.DefinirValorEntrega(valorEntrega);

        // Persiste a venda e seus efeitos (estoque + saldo) de forma realista:
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
    public async Task Handle_DeveEditarParaNovaQuantidade_AtualizandoEstoqueELucro()
    {
        var (db, cliente, produto, venda) = await CriarVendaAsync(forma: FormaPagamento.Dinheiro, quantidade: 2);
        var handler = new EditarVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        var resultado = await handler.Handle(
            new EditarVendaCommand(venda.Id, FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 3)]),
            CancellationToken.None);

        // Estoque: 20 - 2 (venda antiga) + 2 (reversão) - 3 (nova) = 17.
        var produtoAtual = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(17, produtoAtual!.QuantidadeEstoque);

        // Nova venda criada, antiga marcada excluída.
        var vendas = db.Vendas.ToList();
        Assert.Equal(2, vendas.Count);
        Assert.Single(vendas, v => v.ExcluidaEm == null); // só a nova ativa
        Assert.Single(vendas, v => v.ExcluidaEm != null); // a antiga excluída

        Assert.Equal(300m, resultado.ValorTotal);
    }

    [Fact]
    public async Task Handle_RegraFiado_DeveRecalcularSaldo_QuandoSaldoCobreValor()
    {
        // Fiado: cliente tem saldo devedor (aberto) que cobre a venda.
        var (db, cliente, produto, venda) = await CriarVendaAsync(forma: FormaPagamento.Fiado, quantidade: 2); // 200, cliente +200 saldo
        var handler = new EditarVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        // Novo total: 100 (1 item) -> o saldo deve recalcular: 200 - 200(antiga) + 100(nova) = 100.
        var resultado = await handler.Handle(
            new EditarVendaCommand(venda.Id, FormaPagamento.Fiado, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        Assert.Equal(100m, resultado.ValorTotal);
        Assert.Equal(100m, resultado.SaldoDevedor);

        var clienteAtual = await db.Clientes.FindAsync(cliente.Id);
        Assert.Equal(100m, clienteAtual!.SaldoDevedor);
    }

    [Fact]
    public async Task Handle_RegraFiado_DeveBloquear_QuandoSaldoNaoCobreValor()
    {
        // Fiado quitado: cliente tem saldo que não cobre (já pagou).
        var (db, _, produto, venda) = await CriarVendaAsync(forma: FormaPagamento.Fiado, quantidade: 2); // 200 fiado
        var cliente = await db.Clientes.FindAsync(db.Clientes.First().Id);

        // Simula cliente que já quitou a dívida (saldo menor que o valor).
        db.Attach(cliente!);
        cliente!.RegistrarPagamento(150m); // saldo 50 < 200
        await db.SaveChangesAsync();

        var handler = new EditarVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new EditarVendaCommand(venda.Id, FormaPagamento.Fiado, 0m,
                    [new ItemVendaInput(produto.Id, 1)]),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_DeveBloquearEdicao_QuandoVendaJaExcluida()
    {
        var (db, _, produto, venda) = await CriarVendaAsync();
        venda.Excluir();
        await db.SaveChangesAsync();

        var handler = new EditarVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new EditarVendaCommand(venda.Id, FormaPagamento.Dinheiro, 0m,
                    [new ItemVendaInput(produto.Id, 1)]),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoVendaNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new EditarVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new EditarVendaCommand(Guid.NewGuid(), FormaPagamento.Dinheiro, 0m,
                    [new ItemVendaInput(Guid.NewGuid(), 1)]),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoUsuarioNaoAdmin()
    {
        var (db, _, produto, venda) = await CriarVendaAsync();
        var handler = new EditarVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new EditarVendaCommand(venda.Id, FormaPagamento.Dinheiro, 0m,
                    [new ItemVendaInput(produto.Id, 1)]),
                CancellationToken.None).AsTask());
    }
}
using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Features.Admin.Vendas;
using AndreGas.Web.Features.Vendas.NovaVenda;

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
    public async Task Handle_ExcluirTroca_ComVasilhamesDisponiveisMenoresQueQtd_DeveClamparSemLancar()
    {
        // Hardening I1: uma venda de troca de 2 (válida), mas o depósito teve o vazio consumido por
        // trocas manuais intersticiais após a venda (Vasilhames caiu a 1). Ao excluir (ReverterAsync),
        // o disponível (1) < qtd revertido (2) — não pode lançar; reverte no máximo o existente.
        var (db, _, produto, venda) = await CriarVendaVasilhameAsync(quantidade: 2, vasilhames: 10);
        Assert.Equal(12, produto.QuantidadeVasilhames); // 10 + 2 da troca

        produto.RegistrarSaidaVasilhame(11); // intersticial
        await db.SaveChangesAsync();
        Assert.Equal(1, produto.QuantidadeVasilhames);

        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));
        var resultado = await handler.Handle(new ExcluirVendaCommand(venda.Id), CancellationToken.None);

        Assert.True(resultado.Sucesso);

        var produtoAtual = await db.Produtos.FindAsync(produto.Id);

        // Sem exceção. Vasilhames: 1 → clamp reverte −1 (disponível) → 0. Estoque estornado +2.
        Assert.Equal(0, produtoAtual!.QuantidadeVasilhames);
    }

    private static async Task<(AndreGas.Infrastructure.AppDbContext Db, Cliente Cliente, Produto Produto, Venda Venda)>
        CriarVendaVasilhameAsync(int quantidade = 2, int estoque = 20, int vasilhames = 10, bool troca = true)
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        produto.RegistrarEntrada(estoque);
        produto.RegistrarEntradaVasilhame(vasilhames);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        var registrarHandler = new RegistrarVendaCommandHandler(db);
        var resultado = await registrarHandler.Handle(
            new RegistrarVendaCommand(cliente.Id, null, null, null, FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, quantidade, EhEmprestimo: !troca)]),
            CancellationToken.None);

        var venda = await db.Vendas.FindAsync(resultado.VendaId);
        return (db, cliente, produto, venda!);
    }

    [Fact]
    public async Task Handle_ExcluirEmprestimo_ComContadorDessincronizadoDoBanco_DeveLancarEmVezDeSilencioso()
    {
        // Hardening I3: se o contador do produto indica empréstimos mas as linhas de EmprestimoVasilhame
        // foram removidas/desalinhadas (divergência contador↔rows), a reversão não pode terminar
        // silenciosa com "restante > 0" não desfeito — deve lançar InvalidOperationException.
        var (db, _, produto, venda) = await CriarVendaVasilhameAsync(troca: false, quantidade: 3);
        Assert.Equal(3, produto.QuantidadeVasilhamesEmprestados);
        Assert.Equal(1, db.EmprestimosVasilhames.Count()); // venda de 1 produto consolida em 1 linha (Saldo 3)

        // Simula divergência: as linhas somem (restaurando só via código externo que esqueceu o
        // contador). O contador segue 3, linhas 0.
        db.EmprestimosVasilhames.RemoveRange(db.EmprestimosVasilhames);
        await db.SaveChangesAsync();
        Assert.Equal(3, (await db.Produtos.FindAsync(produto.Id))!.QuantidadeVasilhamesEmprestados);

        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(new ExcluirVendaCommand(venda.Id), CancellationToken.None));
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
    public async Task Handle_ExcluirVendaDeEmprestimo_DeveReverterEmprestadosEEstoqueERemoverLinhas()
    {
        // Venda de empréstimo de 2 via RegistrarVendaCommandHandler (caminho real): Estoque 18,
        // Vasilhames 10, Emprestados 2, 1 linha de EmprestimoVasilhame aberta.
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        produto.RegistrarEntrada(20);
        produto.RegistrarEntradaVasilhame(10);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        var registrarHandler = new RegistrarVendaCommandHandler(db);
        var resultado = await registrarHandler.Handle(
            new RegistrarVendaCommand(cliente.Id, null, null, null, FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 2, EhEmprestimo: true)]),
            CancellationToken.None);
        Assert.Equal(18, (await db.Produtos.FindAsync(produto.Id))!.QuantidadeEstoque);
        Assert.Equal(2, (await db.Produtos.FindAsync(produto.Id))!.QuantidadeVasilhamesEmprestados);
        Assert.Single(db.EmprestimosVasilhames);

        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));
        await handler.Handle(new ExcluirVendaCommand(resultado.VendaId), CancellationToken.None);

        var produtoAtual = await db.Produtos.FindAsync(produto.Id);

        // Reversão: Estoque +2 (20), Emprestados −2 (0), linha aberta da venda removida.
        Assert.Equal(20, produtoAtual!.QuantidadeEstoque);
        Assert.Equal(10, produtoAtual.QuantidadeVasilhames);
        Assert.Equal(0, produtoAtual.QuantidadeVasilhamesEmprestados);
        Assert.Empty(db.EmprestimosVasilhames);
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

    [Fact]
    public async Task Handle_ExcluirVendaDeEmprestimo_DeveConservarEmprestimoAnterior_QuandoClienteJaTinhaEmprestimoAberto()
    {
        // Cliente já tinha 3 vasilhames em aberto (linha mais antiga). Novo empréstimo de 2 pela
        // venda registra outra linha (mais recente). Ao excluir a venda, apenas a linha da venda é
        // removida (FIFO), preservando os 3 antigos no agregado de Emprestados.
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        produto.RegistrarEntrada(20);
        produto.RegistrarEntradaVasilhame(10);
        produto.RegistrarEmprestimoVasilhame(3); // contador operacional dos 3 antigos
        db.Produtos.Add(produto);
        db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(
            cliente.Id, produto.Id, 3, data: DateTime.UtcNow.AddDays(-5), observacao: "anterior"));
        await db.SaveChangesAsync();

        var registrarHandler = new RegistrarVendaCommandHandler(db);
        var resultado = await registrarHandler.Handle(
            new RegistrarVendaCommand(cliente.Id, null, null, null, FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 2, EhEmprestimo: true)]),
            CancellationToken.None);

        // 2 linhas abertas: a antiga (3) + a da venda (2); Emprestados 5.
        Assert.Equal(5, (await db.Produtos.FindAsync(produto.Id))!.QuantidadeVasilhamesEmprestados);
        Assert.Equal(2, db.EmprestimosVasilhames.Count(e => e.DevolvidoEm == null));

        var handler = new ExcluirVendaCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));
        await handler.Handle(new ExcluirVendaCommand(resultado.VendaId), CancellationToken.None);

        // Reversão: Emprestados −2 → 3; a linha da venda removida; a antiga (DevolvidoEm null) preservada.
        var produtoAtual = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(20, produtoAtual!.QuantidadeEstoque);
        Assert.Equal(3, produtoAtual.QuantidadeVasilhamesEmprestados);
        var linhaAntiga = Assert.Single(db.EmprestimosVasilhames.Where(e => e.ClienteId == cliente.Id));
        Assert.Equal(3, linhaAntiga.Quantidade);
        Assert.Null(linhaAntiga.DevolvidoEm);
    }
}
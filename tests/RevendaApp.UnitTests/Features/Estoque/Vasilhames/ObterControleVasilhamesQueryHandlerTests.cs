using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.Infrastructure;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Estoque.Vasilhames;

namespace RevendaApp.UnitTests.Features.Estoque.Vasilhames;

public class ObterControleVasilhamesQueryHandlerTests
{
    private static async Task<(AppDbContext Db, Cliente ClienteA, Cliente ClienteB, Produto ProdutoA, Produto ProdutoB, Produto ProdutoSemControle)> CriarAsync()
    {
        var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var clienteA = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var clienteB = new Cliente("João Pereira", "11977776666", "Rua B, 2");
        var produtoA = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        var produtoB = new Produto("Botijão 05kg", TipoProduto.Gas, 50m, 30m, 40m, controlaVasilhame: true);
        var semControle = new Produto("Galão Água 20L", TipoProduto.Agua, 20m, 10m, 15m);
        db.Clientes.AddRange(clienteA, clienteB);
        db.Produtos.AddRange(produtoA, produtoB, semControle);

        // produtoA começa com 5 vasilhames disponíveis.
        produtoA.RegistrarEntradaVasilhame(5);
        await db.SaveChangesAsync();
        return (db, clienteA, clienteB, produtoA, produtoB, semControle);
    }

    /// <summary>
    /// Espelha o handler de empréstimo: cria a linha aberta E atualiza os contadores do produto
    /// (Emprestados += Q; devolução parcial reduz), mantendo o estado consistente com a produção.
    /// </summary>
    private static async Task RegistrarEmprestimoAsync(
        AppDbContext db,
        Cliente cliente,
        Produto produto,
        int quantidade,
        int quantidadeDevolvida = 0)
    {
        produto.RegistrarEmprestimoVasilhame(quantidade);
        if (quantidadeDevolvida > 0)
        {
            produto.RegistrarDevolucaoVasilhame(quantidadeDevolvida);
        }

        var emprestimo = new EmprestimoVasilhame(
            cliente.Id,
            produto.Id,
            quantidade,
            new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc),
            null);
        if (quantidadeDevolvida > 0)
        {
            emprestimo.RegistrarDevolucao(quantidadeDevolvida);
        }

        db.EmprestimosVasilhames.Add(emprestimo);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_DeveRetornarApenasProdutosQueControlamVasilhame()
    {
        var (db, _, _, produtoA, produtoB, produtoSemControle) = await CriarAsync();
        var handler = new ObterControleVasilhamesQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterControleVasilhamesQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.ProdutoId == produtoA.Id);
        Assert.Contains(result, p => p.ProdutoId == produtoB.Id);
        Assert.DoesNotContain(result, p => p.ProdutoId == produtoSemControle.Id);
    }

    [Fact]
    public async Task Handle_DeveAgregarDisponivelEmprestadosETotal()
    {
        var (db, clienteA, _, produtoA, _, _) = await CriarAsync();
        await RegistrarEmprestimoAsync(db, clienteA, produtoA, 3);
        var handler = new ObterControleVasilhamesQueryHandler(db, TenantTeste.Contexto());

        var item = Assert.Single(
            await handler.Handle(new ObterControleVasilhamesQuery(), CancellationToken.None),
            p => p.ProdutoId == produtoA.Id);

        Assert.Equal(5, item.Disponivel);   // 5 do CriarAsync (disponíveis não mudam no empréstimo)
        Assert.Equal(3, item.Emprestados);
        Assert.Equal(8, item.Total);        // 5 disponíveis + 3 emprestados
    }

    [Fact]
    public async Task Handle_EmprestimosPara_DeveListarClientesComSaldoEmAberto_AgrupadosPorCliente()
    {
        var (db, clienteA, clienteB, produtoA, _, _) = await CriarAsync();
        await RegistrarEmprestimoAsync(db, clienteA, produtoA, 3);
        await RegistrarEmprestimoAsync(db, clienteA, produtoA, 2); // 2º empréstimo do mesmo cliente
        await RegistrarEmprestimoAsync(db, clienteB, produtoA, 4);
        var handler = new ObterControleVasilhamesQueryHandler(db, TenantTeste.Contexto());

        var item = Assert.Single(
            await handler.Handle(new ObterControleVasilhamesQuery(), CancellationToken.None),
            p => p.ProdutoId == produtoA.Id);

        Assert.Equal(2, item.EmprestimosPara.Count);

        var maria = Assert.Single(item.EmprestimosPara, c => c.ClienteId == clienteA.Id);
        Assert.Equal("Maria Souza", maria.ClienteNome);
        Assert.Equal(5, maria.Quantidade); // 3 + 2 consolidados num único "para quem"

        var joao = Assert.Single(item.EmprestimosPara, c => c.ClienteId == clienteB.Id);
        Assert.Equal("João Pereira", joao.ClienteNome);
        Assert.Equal(4, joao.Quantidade);
    }

    [Fact]
    public async Task Handle_EmprestimosPara_DeveAgruparPorProduto()
    {
        var (db, clienteA, _, produtoA, produtoB, _) = await CriarAsync();
        await RegistrarEmprestimoAsync(db, clienteA, produtoA, 3);
        await RegistrarEmprestimoAsync(db, clienteA, produtoB, 7);
        var handler = new ObterControleVasilhamesQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterControleVasilhamesQuery(), CancellationToken.None);

        var itemA = Assert.Single(result, p => p.ProdutoId == produtoA.Id);
        var itemB = Assert.Single(result, p => p.ProdutoId == produtoB.Id);
        Assert.Single(itemA.EmprestimosPara);
        Assert.Equal(3, Assert.Single(itemA.EmprestimosPara).Quantidade);
        Assert.Equal("Maria Souza", Assert.Single(itemA.EmprestimosPara).ClienteNome);
        Assert.Single(itemB.EmprestimosPara);
        Assert.Equal(7, Assert.Single(itemB.EmprestimosPara).Quantidade);
    }

    [Fact]
    public async Task Handle_DevolucaoIntegral_DeveFicarForaDeEmprestimosPara_EZerarEmprestados()
    {
        var (db, clienteA, _, produtoA, _, _) = await CriarAsync();
        await RegistrarEmprestimoAsync(db, clienteA, produtoA, 6, quantidadeDevolvida: 6); // quitado
        var handler = new ObterControleVasilhamesQueryHandler(db, TenantTeste.Contexto());

        var item = Assert.Single(
            await handler.Handle(new ObterControleVasilhamesQuery(), CancellationToken.None),
            p => p.ProdutoId == produtoA.Id);

        Assert.Empty(item.EmprestimosPara);
        Assert.Equal(0, item.Emprestados); // contador do produto espelha a quitação
    }

    [Fact]
    public async Task Handle_ProdutoSemDisponiveisMasComEmprestados_DevePermanecerListado()
    {
        var (db, clienteA, _, _, _, _) = await CriarAsync();
        var semDisponiveis = new Produto("Botijão 45kg", TipoProduto.Gas, 200m, 140m, 160m, controlaVasilhame: true);
        db.Produtos.Add(semDisponiveis);
        await db.SaveChangesAsync();
        await RegistrarEmprestimoAsync(db, clienteA, semDisponiveis, 2);
        var handler = new ObterControleVasilhamesQueryHandler(db, TenantTeste.Contexto());

        var item = Assert.Single(
            await handler.Handle(new ObterControleVasilhamesQuery(), CancellationToken.None),
            p => p.ProdutoId == semDisponiveis.Id);

        Assert.Equal(0, item.Disponivel);
        Assert.Equal(2, item.Emprestados);
        Assert.Equal(2, item.Total);
        Assert.Single(item.EmprestimosPara);
    }
}
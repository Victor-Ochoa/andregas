using AndreGas.Domain.Entities;
using AndreGas.UnitTests.TestHelpers;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Domain;

public class EmprestimoVasilhameTests
{
    private static Guid ClienteId => Guid.NewGuid();
    private static Guid ProdutoId => Guid.NewGuid();

    [Fact]
    public void Construtor_DevePreencherCamposESaldoInicial()
    {
        var clienteId = ClienteId;
        var produtoId = ProdutoId;
        var emprestimo = new EmprestimoVasilhame(clienteId, produtoId, 5, null, "Empréstimo de teste");

        Assert.NotEqual(Guid.Empty, emprestimo.Id);
        Assert.Equal(clienteId, emprestimo.ClienteId);
        Assert.Equal(produtoId, emprestimo.ProdutoId);
        Assert.Equal(5, emprestimo.Quantidade);
        Assert.Equal("Empréstimo de teste", emprestimo.Observacao);
        Assert.Equal(5, emprestimo.Saldo);
        Assert.Equal(0, emprestimo.QuantidadeDevolvida);
        Assert.Null(emprestimo.DevolvidoEm);
    }

    [Fact]
    public void Construtor_ComDataNula_DeveUsarDataAtual()
    {
        var antes = DateTime.UtcNow.AddSeconds(-1);

        var emprestimo = new EmprestimoVasilhame(ClienteId, ProdutoId, 5, null, null);

        var depois = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(emprestimo.Data, antes, depois);
    }

    [Fact]
    public void Construtor_ComDataInformada_DevePreservarData()
    {
        var data = new DateTime(2026, 10, 10, 8, 30, 0, DateTimeKind.Utc);

        var emprestimo = new EmprestimoVasilhame(ClienteId, ProdutoId, 5, data, null);

        Assert.Equal(data, emprestimo.Data);
    }

    [Fact]
    public void Construtor_ComQuantidadeZeroOuNegativa_DeveLancar()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmprestimoVasilhame(ClienteId, ProdutoId, 0, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmprestimoVasilhame(ClienteId, ProdutoId, -3, null, null));
    }

    [Fact]
    public void RegistrarDevolucao_DevolucaoParcial_DeveAumentarDevolvidaDiminuirSaldoEManterDevolvidoEmNulo()
    {
        var emprestimo = new EmprestimoVasilhame(ClienteId, ProdutoId, 10, null, null);

        emprestimo.RegistrarDevolucao(4);

        Assert.Equal(4, emprestimo.QuantidadeDevolvida);
        Assert.Equal(6, emprestimo.Saldo);
        Assert.Null(emprestimo.DevolvidoEm);
    }

    [Fact]
    public void RegistrarDevolucao_AteQuitar_DeveDefinirDevolvidoEm()
    {
        var emprestimo = new EmprestimoVasilhame(ClienteId, ProdutoId, 10, null, null);

        emprestimo.RegistrarDevolucao(10);

        Assert.Equal(10, emprestimo.QuantidadeDevolvida);
        Assert.Equal(0, emprestimo.Saldo);
        Assert.NotNull(emprestimo.DevolvidoEm);
    }

    [Fact]
    public void RegistrarDevolucao_ParcialDepoisTotal_DeveDefinirDevolvidoEmApenasNoFinal()
    {
        var emprestimo = new EmprestimoVasilhame(ClienteId, ProdutoId, 10, null, null);

        emprestimo.RegistrarDevolucao(3);
        Assert.Null(emprestimo.DevolvidoEm);

        emprestimo.RegistrarDevolucao(7);
        Assert.NotNull(emprestimo.DevolvidoEm);
        Assert.Equal(0, emprestimo.Saldo);
    }

    [Fact]
    public void RegistrarDevolucao_ComQuantidadeMaiorQueSaldo_DeveLancar()
    {
        var emprestimo = new EmprestimoVasilhame(ClienteId, ProdutoId, 5, null, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => emprestimo.RegistrarDevolucao(6));
    }

    [Fact]
    public void RegistrarDevolucao_ComQuantidadeZeroOuNegativa_DeveLancar()
    {
        var emprestimo = new EmprestimoVasilhame(ClienteId, ProdutoId, 5, null, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => emprestimo.RegistrarDevolucao(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => emprestimo.RegistrarDevolucao(-2));
    }

    [Fact]
    public async Task EmprestimoVasilhame_DevePersistirEVoltarDoBanco()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var clienteId = ClienteId;
        var produtoId = ProdutoId;

        var emprestimo = new EmprestimoVasilhame(clienteId, produtoId, 8, null, "Persistência");
        db.EmprestimosVasilhames.Add(emprestimo);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var carregado = await db.EmprestimosVasilhames.SingleOrDefaultAsync(e => e.Id == emprestimo.Id);

        Assert.NotNull(carregado);
        Assert.Equal(clienteId, carregado.ClienteId);
        Assert.Equal(produtoId, carregado.ProdutoId);
        Assert.Equal(8, carregado.Quantidade);
        Assert.Equal(8, carregado.Saldo);
        Assert.Equal("Persistência", carregado.Observacao);
    }
}
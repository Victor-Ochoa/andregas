using AndreGas.Domain.Entities;

namespace AndreGas.UnitTests.Domain;

public class ClienteTests
{
    [Fact]
    public void Construtor_DeveIniciarComSaldoDevedorZeradoEAtivo()
    {
        var cliente = new Cliente("Maria Souza", "(11) 98888-7777", "Rua das Flores, 123");

        Assert.Equal(0m, cliente.SaldoDevedor);
        Assert.True(cliente.Ativo);
    }

    [Fact]
    public void Construtor_DeveNormalizarTelefoneRemovendoFormatacao()
    {
        var cliente = new Cliente("Maria Souza", "(11) 98888-7777", "Rua das Flores, 123");

        Assert.Equal("11988887777", cliente.Telefone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Construtor_DeveLancarExcecao_QuandoNomeVazio(string nome)
    {
        Assert.Throws<ArgumentException>(() => new Cliente(nome, "11988887777", "Rua A, 1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void Construtor_DeveLancarExcecao_QuandoTelefoneInvalido(string telefone)
    {
        Assert.Throws<ArgumentException>(() => new Cliente("Maria Souza", telefone, "Rua A, 1"));
    }

    [Fact]
    public void Construtor_DeveLancarExcecao_QuandoEnderecoVazio()
    {
        Assert.Throws<ArgumentException>(() => new Cliente("Maria Souza", "11988887777", ""));
    }

    [Fact]
    public void AdicionarSaldoDevedor_DeveSomarAoSaldoExistente()
    {
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");

        cliente.AdicionarSaldoDevedor(100m);
        cliente.AdicionarSaldoDevedor(50m);

        Assert.Equal(150m, cliente.SaldoDevedor);
    }

    [Fact]
    public void AdicionarSaldoDevedor_DeveLancarExcecao_QuandoValorNegativo()
    {
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");

        Assert.Throws<ArgumentOutOfRangeException>(() => cliente.AdicionarSaldoDevedor(-10m));
    }

    [Fact]
    public void RegistrarPagamento_DeveAbaterDoSaldoDevedor()
    {
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        cliente.AdicionarSaldoDevedor(100m);

        cliente.RegistrarPagamento(40m);

        Assert.Equal(60m, cliente.SaldoDevedor);
    }

    [Fact]
    public void RegistrarPagamento_NaoDeveDeixarSaldoNegativo_QuandoPagamentoMaiorQueSaldo()
    {
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        cliente.AdicionarSaldoDevedor(30m);

        cliente.RegistrarPagamento(100m);

        Assert.Equal(0m, cliente.SaldoDevedor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RegistrarPagamento_DeveLancarExcecao_QuandoValorNaoPositivo(decimal valor)
    {
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");

        Assert.Throws<ArgumentOutOfRangeException>(() => cliente.RegistrarPagamento(valor));
    }

    [Fact]
    public void AtualizarDados_DeveAlterarNomeTelefoneEEndereco()
    {
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");

        cliente.AtualizarDados("Maria S. Oliveira", "11999998888", "Rua B, 2");

        Assert.Equal("Maria S. Oliveira", cliente.Nome);
        Assert.Equal("11999998888", cliente.Telefone);
        Assert.Equal("Rua B, 2", cliente.Endereco);
    }

    [Fact]
    public void Desativar_DeveMarcarClienteComoInativo()
    {
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");

        cliente.Desativar();

        Assert.False(cliente.Ativo);
    }
}
using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;

namespace AndreGas.UnitTests.Domain;

public class HistoricoEstoqueTests
{
    [Fact]
    public void Construtor_DevePreencherCampos()
    {
        var data = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);

        var historico = new HistoricoEstoque(
            Guid.NewGuid(), TipoHistoricoEstoque.Entrada,
            "Entrada de 10 produtos", "Compra", "maria@teste.com", quantidade: 10,
            vendaId: null, data: data);

        Assert.Equal(10, historico.Quantidade);
        Assert.Equal(TipoHistoricoEstoque.Entrada, historico.Tipo);
        Assert.Equal("Entrada de 10 produtos", historico.Descricao);
        Assert.Equal("Compra", historico.Motivo);
        Assert.Equal("maria@teste.com", historico.Usuario);
        Assert.Equal(data, historico.Data);
        Assert.Null(historico.VendaId);
    }

    [Fact]
    public void Construtor_ComUsuarioEmBranco_DeveUsarSistema()
    {
        var historico = new HistoricoEstoque(
            Guid.NewGuid(), TipoHistoricoEstoque.Cadastro, "Cadastro inicial", "Cadastro inicial", "");

        Assert.Equal("sistema", historico.Usuario);
    }

    [Fact]
    public void Construtor_ComQuantidadeNula_DeveRegistrarNull()
    {
        var historico = new HistoricoEstoque(
            Guid.NewGuid(), TipoHistoricoEstoque.Edicao, "Nome: X → Y", "Edição de dados", "joao@teste.com");

        Assert.Null(historico.Quantidade);
    }
}
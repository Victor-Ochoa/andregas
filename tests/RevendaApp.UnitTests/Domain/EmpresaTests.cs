using RevendaApp.Domain.Entities;

namespace RevendaApp.UnitTests.Domain;

public class EmpresaTests
{
    [Fact]
    public void Empresa_criada_inicializa_CriadaEm_e_Ativo_true()
    {
        var antes = DateTime.UtcNow;

        var empresa = new Empresa("andre", "André Gás e Água");

        Assert.True(empresa.Ativo);
        Assert.True(empresa.CriadaEm >= antes);
        Assert.True(empresa.CriadaEm <= DateTime.UtcNow);
    }

    [Fact]
    public void Empresa_Desativar_marca_Ativo_false_e_Ativar_true()
    {
        var empresa = new Empresa("andre", "André Gás e Água");

        empresa.Desativar();

        Assert.False(empresa.Ativo);

        empresa.Ativar();

        Assert.True(empresa.Ativo);
    }

    [Fact]
    public void Empresa_AtualizarDados_altera_nome_cnpj_endereco_telefones_cor_logo()
    {
        var empresa = new Empresa("andre", "André Gás e Água");

        empresa.AtualizarDados(
            nome: "André Gás e Água Ltda",
            cnpj: "12.345.678/0001-90",
            endereco: "Rua das Flores, 123",
            numero: "123",
            cep: "01234-567",
            bairro: "Centro",
            cidade: "São Paulo",
            estado: "SP",
            telefones: new List<string> { "(11) 98888-7777", "11 9999-8888" },
            corPrimaria: "#0A1F44",
            logo: "data:image/png;base64,xxx");

        Assert.Equal("André Gás e Água Ltda", empresa.Nome);
        Assert.Equal("12.345.678/0001-90", empresa.Cnpj);
        Assert.Equal("Rua das Flores, 123", empresa.Endereco);
        Assert.Equal("123", empresa.Numero);
        Assert.Equal("01234-567", empresa.Cep);
        Assert.Equal("Centro", empresa.Bairro);
        Assert.Equal("São Paulo", empresa.Cidade);
        Assert.Equal("SP", empresa.Estado);
        Assert.Equal(new List<string> { "11988887777", "1199998888" }, empresa.Telefones);
        Assert.Equal("#0A1F44", empresa.CorPrimaria);
        Assert.Equal("data:image/png;base64,xxx", empresa.Logo);
    }

    [Fact]
    public void Empresa_AtualizarDados_nunca_muda_Identificador()
    {
        var empresa = new Empresa("andre", "André Gás e Água");

        empresa.AtualizarDados(
            nome: "Outro Nome",
            cnpj: "98.765.432/0001-10",
            endereco: null,
            numero: null,
            cep: null,
            bairro: null,
            cidade: null,
            estado: null,
            telefones: null,
            corPrimaria: null,
            logo: null);

        Assert.Equal("andre", empresa.Identificador);
    }

    [Fact]
    public void Empresa_criada_sem_valor_usa_telefones_vazio()
    {
        var empresa = new Empresa("andre", "André Gás e Água");

        Assert.Empty(empresa.Telefones);
    }

    [Fact]
    public void Empresa_construtor_normaliza_telefones_para_digitos()
    {
        var empresa = new Empresa("andre", "André Gás e Água", telefones: new List<string> { "(11) 98888-7777", "11 9999-8888" });

        Assert.Equal(new List<string> { "11988887777", "1199998888" }, empresa.Telefones);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empresa_construtor_lanca_excecao_quando_identificador_vazio(string identificador)
    {
        Assert.Throws<ArgumentException>(() => new Empresa(identificador, "André Gás e Água"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empresa_construtor_lanca_excecao_quando_nome_vazio(string nome)
    {
        Assert.Throws<ArgumentException>(() => new Empresa("andre", nome));
    }
}
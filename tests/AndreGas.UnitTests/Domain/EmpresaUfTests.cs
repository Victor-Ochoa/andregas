using AndreGas.Domain.Entities;

namespace AndreGas.UnitTests.Domain;

/// <summary>
/// A UF é a única unidade federativa guardada em coluna: se entrar como o operador digitou
/// ("sp", " sp "), a mesma empresa fica com dois formatos no banco e filtros futuros por estado
/// (já previstos — o campo foi criado para features futuras) deixam de casar.
/// </summary>
public class EmpresaUfTests
{
    [Theory]
    [InlineData("sp", "SP")]
    [InlineData(" sp ", "SP")]
    [InlineData("SP", "SP")]
    [InlineData("rj", "RJ")]
    public void NormalizarUf_DeveGuardarEmMaiusculasSemEspacos(string entrada, string esperado) =>
        Assert.Equal(esperado, Empresa.NormalizarUf(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizarUf_DeveManterVazioComoNulo(string? entrada) =>
        Assert.Null(Empresa.NormalizarUf(entrada));

    /// <summary>
    /// Regressão: criar empresa pela central gravava a UF crua, enquanto "Minha Empresa"
    /// normalizava — a mesma entidade em dois formatos dependendo da tela de origem.
    /// </summary>
    [Fact]
    public void Criar_DeveNormalizarUfRecebida()
    {
        var empresa = new Empresa("andre-gas", "André Gás", ["11999990000"], estado: "sp");

        Assert.Equal("SP", empresa.Estado);
    }

    [Fact]
    public void AtualizarDados_DeveNormalizarUfRecebida()
    {
        var empresa = new Empresa("andre-gas", "André Gás", ["11999990000"], estado: "SP");

        empresa.AtualizarDados("André Gás", estado: " mg ");

        Assert.Equal("MG", empresa.Estado);
    }
}
using AndreGas.Web.Common;

namespace AndreGas.UnitTests.Common;

/// <summary>
/// O <c>returnUrl</c> do login é entrada de usuário (query string) e vira destino de navegação
/// após a autenticação. Sem filtro, um link com o domínio real na frente entrega o usuário a um
/// site externo logo depois de ele digitar a senha verdadeira.
/// </summary>
public class DestinoSeguroTests
{
    [Theory]
    [InlineData("/clientes")]
    [InlineData("/estoque?filtro=gas")]
    [InlineData("/")]
    public void EhLocal_DeveAceitarCaminhoAbsolutoLocal(string returnUrl) =>
        Assert.True(DestinoSeguro.EhLocal(returnUrl));

    [Theory]
    [InlineData("https://site-malicioso.com")]
    [InlineData("http://site-malicioso.com/login")]
    [InlineData("//site-malicioso.com")]
    [InlineData("/\\site-malicioso.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("clientes")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EhLocal_DeveRecusarDestinoExternoOuInvalido(string? returnUrl) =>
        Assert.False(DestinoSeguro.EhLocal(returnUrl));

    /// <summary>Caso de ataque concreto: o destino padrão do login é usado no lugar do externo.</summary>
    [Fact]
    public void OuPadrao_DeveTrocarDestinoExternoPeloPadrao()
    {
        var destino = DestinoSeguro.OuPadrao("https://site-malicioso.com", "/central");

        Assert.Equal("/central", destino);
    }

    [Fact]
    public void OuPadrao_DevePreservarCaminhoLocal()
    {
        var destino = DestinoSeguro.OuPadrao("/clientes", "/central");

        Assert.Equal("/clientes", destino);
    }

    [Fact]
    public void OuPadrao_DeveUsarPadraoQuandoReturnUrlAusente()
    {
        var destino = DestinoSeguro.OuPadrao(null, "/");

        Assert.Equal("/", destino);
    }
}
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Common;
using AndreGas.Web.Components.Layout;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor.Services;

namespace AndreGas.UnitTests.Components;

/// <summary>
/// O menu superior do <see cref="MainLayout"/> é o de <b>uma empresa</b>: Home, Clientes, Estoque e
/// "Nova Venda" só fazem sentido para um usuário de empresa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bug que estes testes travam:</b> o layout é compartilhado por todas as rotas, e os quatro
/// controles eram renderizados incondicionalmente. No modo central (super admin, sem empresa) eles
/// apareciam em <c>/central</c> e levavam a telas de domínio que o super admin não pode abrir —
/// a política <c>ApenasEmpresa</c> recusa e o <c>RoleAwareNotAuthorized</c> o devolve para
/// <c>/central</c>, ou seja, o clique não fazia nada. Um menu que oferece o que a rota não permite
/// é um convite a erro.
/// </para>
/// <para>
/// A condição é <see cref="IdentidadeEmpresaService.TemEmpresa"/> — a mesma que decide entre a
/// identidade da empresa e a do produto no app bar, para o menu não divergir do nome exibido.
/// </para>
/// <para>
/// <b>Limite desta suíte:</b> o item "Configurações" do menu do usuário recebe a mesma correção
/// (a rota é <c>ApenasAdmin</c> e o super admin da central não tem a role), mas o conteúdo de um
/// <c>MudMenu</c> só é renderizado ao abrir o popover — uma asserção sobre ele no markup inicial
/// passaria por vacuidade. Fica coberto só por leitura, não por teste.
/// </para>
/// </remarks>
public class MainLayoutMenuTests
{
    /// <summary>Controle negativo: no modo central o menu de domínio e o "Nova Venda" não aparecem.</summary>
    [Fact]
    public async Task Layout_em_modo_central_nao_renderiza_o_menu_de_dominio_nem_nova_venda()
    {
        await using var ctx = CriarContexto(empresaId: null, ehSuperAdmin: true);

        var cut = RenderizarLayout(ctx);

        Assert.DoesNotContain("Home", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Clientes", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Estoque", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Nova Venda", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Controle positivo: com empresa selecionada os quatro controles continuam lá. Sem ele o teste
    /// acima passaria mesmo que o layout parasse de renderizar o menu para todos.
    /// </summary>
    [Fact]
    public async Task Layout_com_empresa_selecionada_renderiza_o_menu_de_dominio_e_nova_venda()
    {
        await using var ctx = CriarContexto(TenantTeste.EmpresaId, ehSuperAdmin: false);

        var cut = RenderizarLayout(ctx);

        Assert.Contains("Home", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Clientes", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Estoque", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Nova Venda", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// O "Entrar como" é o caso que fecha a conta: o super admin impersonando tem empresa
    /// selecionada (<c>EmpresaId</c> preenchido), então o menu <b>precisa</b> aparecer — é dentro da
    /// empresa que ele vai trabalhar.
    /// </summary>
    [Fact]
    public async Task Layout_com_super_admin_impersonando_renderiza_o_menu_de_dominio()
    {
        await using var ctx = CriarContexto(TenantTeste.EmpresaId, ehSuperAdmin: false, impersonando: true);

        var cut = RenderizarLayout(ctx);

        Assert.Contains("Nova Venda", cut.Markup, StringComparison.Ordinal);
    }

    private static BunitContext CriarContexto(Guid? empresaId, bool ehSuperAdmin, bool impersonando = false)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // bUnit intercepta a autorização: o `AuthorizeView` do menu do usuário precisa do contexto
        // dele, registrado ANTES de qualquer serviço nosso. Não é o objeto do teste (o menu de
        // domínio depende de TemEmpresa, não de role).
        var autorizacao = ctx.AddAuthorization();
        autorizacao.SetAuthorized("usuario@teste.com");

        ctx.Services.AddMudServices();

        // O app bar lê nome/cor/logo do serviço de identidade; em modo central ele não toca o banco
        // (sem empresa, o logo é null), então um AppDbContext de teste basta para a assinatura.
        ctx.Services.AddSingleton<AppDbContext>(InMemoryDbContextFactory.Create());
        ctx.Services.AddSingleton<AndreGas.Infrastructure.Tenancy.ITenantContext>(
            new FakeTenantContext(empresaId, nomeEmpresa: "Empresa Teste", ehSuperAdmin: ehSuperAdmin, impersonando: impersonando));
        ctx.Services.AddSingleton<IdentidadeEmpresaService>();

        return ctx;
    }

    /// <summary>Autoriza qualquer política: aqui só interessa o que o layout renderiza.</summary>
    private sealed class AutorizacaoPermissiva : Microsoft.AspNetCore.Authorization.IAuthorizationService
    {
        public Task<Microsoft.AspNetCore.Authorization.AuthorizationResult> AuthorizeAsync(
            System.Security.Claims.ClaimsPrincipal user, object? resource,
            IEnumerable<Microsoft.AspNetCore.Authorization.IAuthorizationRequirement> requirements) =>
            Task.FromResult(Microsoft.AspNetCore.Authorization.AuthorizationResult.Success());

        public Task<Microsoft.AspNetCore.Authorization.AuthorizationResult> AuthorizeAsync(
            System.Security.Claims.ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(Microsoft.AspNetCore.Authorization.AuthorizationResult.Success());
    }

    private static IRenderedComponent<MainLayout> RenderizarLayout(BunitContext ctx) =>
        ctx.Render<MainLayout>(parametros => parametros
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddContent(0, "conteúdo"))));
}
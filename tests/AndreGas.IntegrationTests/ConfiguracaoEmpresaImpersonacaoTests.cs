using System.Net;
using System.Text.RegularExpressions;

using AndreGas.Domain.Entities;
using AndreGas.Infrastructure.Identity;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Features.Auth;
using AndreGas.Web.Features.ConfiguracaoEmpresa;
using AndreGas.Web.Features.Seed;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Prova que o <c>POST /configuracoes/atualizar-sessao</c> (recarga de sessão depois de editar a
/// empresa) <b>preserva a impersonation</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Regressão que este teste trava:</b> o super admin em "Entrar como" recebe um principal de
/// <c>Admin</c>, que passa em <c>ApenasAdmin</c> e portanto <b>chega</b> em <c>/configuracoes</c>.
/// Reemitir o cookie pela factory de claims (sem reaplicar as claims de impersonation) apagaria
/// <c>Impersonando</c> e <c>SuperAdminOriginalId</c>: o badge "Entrando como" sumiria, o
/// "Voltar à central" falharia <b>para sempre</b> e o super admin ficaria preso na empresa.
/// </para>
/// <para>
/// A edição em si não é o alvo (já coberta em <c>ConfiguracaoEmpresaTests</c>): aqui só importa o
/// efeito da recarga sobre a sessão impersonada.
/// </para>
/// </remarks>
public class ConfiguracaoEmpresaImpersonacaoTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>, IAsyncLifetime
{
    private const string SuperAdminEmail = SeedData.DefaultEmail;
    private const string SuperAdminPassword = SeedData.DefaultPassword;

    public async Task InitializeAsync()
    {
        await using (var db = fixture.CreateDbContext())
        {
            var empresa = await db.Set<Empresa>()
                .FirstOrDefaultAsync(e => e.Identificador == SeedDemoData.IdentificadorEmpresaDemo);

            if (empresa is null)
            {
                db.Set<Empresa>().Add(new Empresa(SeedDemoData.IdentificadorEmpresaDemo, SeedDemoData.NomeEmpresaDemo));
                await db.SaveChangesAsync();
            }
            else if (!empresa.Ativo)
            {
                empresa.Ativar();
                await db.SaveChangesAsync();
            }
        }

        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            await SeedData.GarantirRolesAsync(roleManager);
            await GarantirSuperAdminAsync(userManager);
            await GarantirAdminDemoAsync(userManager);
        }
        finally
        {
            provider.Dispose();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Impersonando → POST de recarga → a sessão continua impersonada (badge visível) e
    /// <c>/central/voltar</c> ainda funciona.
    /// </summary>
    [Fact]
    public async Task Recarga_de_sessao_durante_impersonation_preserva_claims_e_volta_a_central()
    {
        using var client = fixture.CreateClient();
        await AutenticarAsync(client, SuperAdminEmail, SuperAdminPassword, destinoEsperado: "/central");

        // Entra como a empresa demo (mesmo caminho real do "Entrar como").
        var empresaId = await ObterEmpresaDemoIdAsync();
        var tokenEntrar = await ObterTokenAntiforgeryAsync(client);
        using var entrada = await client.PostAsync("/central/entrar-como", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["empresaId"] = empresaId.ToString(),
                ["__RequestVerificationToken"] = tokenEntrar,
            }));
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);

        // Controle positivo: já impersonando, o badge aparece.
        var antes = await ObterHtmlAsync(client, "/estoque");
        Assert.Contains("Entrando como", antes, StringComparison.Ordinal);

        // A recarga de sessão que a aba "Minha Empresa" dispara depois de salvar.
        var token = await ObterTokenAntiforgeryAsync(client);
        using var recarga = await client.PostAsync(
            ConfiguracaoEmpresaEndpoints.RotaAtualizarSessao,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));
        Assert.Equal(HttpStatusCode.Redirect, recarga.StatusCode);

        // (1) O badge de impersonation continua no app bar — a claim não foi perdida.
        var depois = await ObterHtmlAsync(client, "/estoque");
        Assert.Contains("Entrando como", depois, StringComparison.Ordinal);

        // (2) O principal continua sendo o da empresa (não voltou a ser super admin).
        using var centralDurante = await client.GetAsync("/central");
        AssertRedirecionadoParaSemAutorizacao(centralDurante);

        // (3) E o caminho de volta continua existindo: /central/voltar restaura a central.
        var tokenVoltar = await ObterTokenAntiforgeryAsync(client);
        using var volta = await client.PostAsync("/central/voltar", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = tokenVoltar,
            }));
        Assert.Equal(HttpStatusCode.Redirect, volta.StatusCode);

        using var centralDepois = await client.GetAsync("/central");
        Assert.Equal(HttpStatusCode.OK, centralDepois.StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static void AssertRedirecionadoParaSemAutorizacao(HttpResponseMessage resposta)
    {
        if (resposta.StatusCode == HttpStatusCode.Forbidden)
        {
            return;
        }

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("/sem-autorizacao", resposta.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    private static async Task<string> ObterHtmlAsync(HttpClient client, string caminho)
    {
        using var resposta = await client.GetAsync(caminho);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.Content.ReadAsStringAsync();
    }

    private static async Task AutenticarAsync(HttpClient client, string email, string senha, string destinoEsperado)
    {
        using var pagina = await client.GetAsync("/login");
        var html = await pagina.Content.ReadAsStringAsync();

        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        var handler = Regex.Match(html, "name=\"_handler\" value=\"([^\"]*)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(string.IsNullOrEmpty(handler));

        using var resposta = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = handler,
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = email,
            ["Input.Password"] = senha,
            ["Input.RememberMe"] = "true",
        }));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal(destinoEsperado, Caminho(resposta.Headers.Location!));
    }

    private static string Caminho(Uri local)
    {
        var texto = local.OriginalString;
        return Uri.TryCreate(texto, UriKind.Absolute, out var absoluto) ? absoluto.AbsolutePath : texto;
    }

    /// <summary>Token antiforgery de <c>/login</c> (SSR estático; o cookie vale para a sessão).</summary>
    private static async Task<string> ObterTokenAntiforgeryAsync(HttpClient client)
    {
        using var resposta = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var html = await resposta.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        return token;
    }

    private async Task<Guid> ObterEmpresaDemoIdAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await db.Set<Empresa>()
            .Where(e => e.Identificador == SeedDemoData.IdentificadorEmpresaDemo)
            .Select(e => e.Id)
            .SingleAsync();
    }

    private static async Task GarantirSuperAdminAsync(UserManager<ApplicationUser> userManager)
    {
        var usuario = await userManager.FindByEmailAsync(SuperAdminEmail);
        if (usuario is null)
        {
            usuario = new ApplicationUser
            {
                UserName = SuperAdminEmail,
                Email = SuperAdminEmail,
                EmailConfirmed = true,
                Nome = SeedData.NomeSuperAdmin,
                EmpresaId = null,
                Ativo = true,
            };

            var criacao = await userManager.CreateAsync(usuario, SuperAdminPassword);
            Assert.True(criacao.Succeeded, string.Join("; ", criacao.Errors.Select(e => e.Description)));
        }
        else
        {
            usuario.EmpresaId = null;
            usuario.Ativo = true;
            await userManager.UpdateAsync(usuario);
        }

        if (!await userManager.IsInRoleAsync(usuario, ApplicationRole.SuperAdmin))
        {
            await userManager.AddToRoleAsync(usuario, ApplicationRole.SuperAdmin);
        }
    }

    private async Task GarantirAdminDemoAsync(UserManager<ApplicationUser> userManager)
    {
        var empresaId = await ObterEmpresaDemoIdAsync();

        var admin = await userManager.FindByEmailAsync(SeedDemoData.EmailAdminDemo);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = SeedDemoData.EmailAdminDemo,
                Email = SeedDemoData.EmailAdminDemo,
                EmailConfirmed = true,
                Nome = SeedDemoData.NomeAdminDemo,
                EmpresaId = empresaId,
                Ativo = true,
            };

            var criacao = await userManager.CreateAsync(admin, SeedDemoData.SenhaAdminDemo);
            Assert.True(criacao.Succeeded, string.Join("; ", criacao.Errors.Select(e => e.Description)));
        }
        else if (admin.EmpresaId != empresaId || !admin.Ativo)
        {
            admin.EmpresaId = empresaId;
            admin.Ativo = true;
            await userManager.UpdateAsync(admin);
        }

        if (!await userManager.IsInRoleAsync(admin, ApplicationRole.Admin))
        {
            await userManager.AddToRoleAsync(admin, ApplicationRole.Admin);
        }
    }
}
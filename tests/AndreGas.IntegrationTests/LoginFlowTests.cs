using System.Net;
using System.Text.RegularExpressions;

using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Features.Auth;
using AndreGas.Web.Features.Seed;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração ponta a ponta do fluxo de login: sobem a aplicação real com Postgres
/// (via <see cref="WebAppFixture"/>), simulam o form POST estático (SSR) que o Blazor gera,
/// e verificam o cookie de autenticação do Identity e o destino da navegação.
/// </summary>
/// <remarks>
/// Multi-empresa: o destino pós-login vem do <c>LoginCommandHandler</c> — super admin da central
/// (sem empresa) vai para <c>/central</c> e o admin de empresa vai para <c>/</c>. Os usuários/empresa
/// usados aqui são garantidos no <see cref="InitializeAsync"/> porque o Postgres do AppHost tem
/// volume persistente e outra classe de teste pode tê-los removido com o reset do banco.
/// </remarks>
public class LoginFlowTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>, IAsyncLifetime
{
    private const string SuperAdminEmail = SeedData.DefaultEmail;
    private const string SuperAdminPassword = SeedData.DefaultPassword;

    public async Task InitializeAsync()
    {
        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            await GarantirSuperAdminAsync(userManager, roleManager);
            await GarantirEmpresaDemoEAdminAsync(userManager, roleManager);
        }
        finally
        {
            provider.Dispose();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UnauthenticatedRequest_IsRedirectedToLogin()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WithValidCredentials_SetsAuthCookieAndAllowsAccessToHome()
    {
        using var client = fixture.CreateClient();

        var (token, handler) = await GetAntiforgeryFieldsAsync(client, "/login");

        using var loginResponse = await PostLoginAsync(client, token, handler, SeedDemoData.EmailAdminDemo, SeedDemoData.SenhaAdminDemo);

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        // Admin de empresa: o destino do login é o app ("/").
        Assert.Equal("/", loginResponse.Headers.Location!.AbsolutePath);

        using var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);

        var homeHtml = await homeResponse.Content.ReadAsStringAsync();
        Assert.Contains("Painel — André Gas e Água", homeHtml, StringComparison.Ordinal);

        // O painel só renderiza depois que a query de dashboard (dados de domínio) responde; cobre a
        // renderização autenticada da home de um usuário de empresa.
        Assert.Contains("Vendas no período", homeHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_SuperAdmin_IsRedirectedToCentral()
    {
        using var client = fixture.CreateClient();

        var (token, handler) = await GetAntiforgeryFieldsAsync(client, "/login");

        using var loginResponse = await PostLoginAsync(client, token, handler, SuperAdminEmail, SuperAdminPassword);

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        // Super admin da central (sem empresa) cai em /central (a tela é de outra task).
        Assert.Equal("/central", loginResponse.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsErrorAndDoesNotAuthenticate()
    {
        using var client = fixture.CreateClient();

        var (token, handler) = await GetAntiforgeryFieldsAsync(client, "/login");

        using var loginResponse = await PostLoginAsync(client, token, handler, SeedDemoData.EmailAdminDemo, "SenhaErrada123");

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var html = await loginResponse.Content.ReadAsStringAsync();
        Assert.Contains("E-mail ou senha inv", html, StringComparison.Ordinal);

        // Still unauthenticated: hitting home should redirect to login again.
        using var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
    }

    [Fact]
    public async Task Login_UsuarioInativo_IsDeniedBeforeSignIn()
    {
        var (email, senha) = await CriarUsuarioDeEmpresaAsync(empresaAtiva: true, usuarioAtivo: false);

        using var client = fixture.CreateClient();
        var (token, handler) = await GetAntiforgeryFieldsAsync(client, "/login");

        using var loginResponse = await PostLoginAsync(client, token, handler, email, senha);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var html = await loginResponse.Content.ReadAsStringAsync();
        // A mensagem é renderizada com HTML encoding (acentos viram entidades), então
        // assertamos por um trecho sem acentos.
        Assert.Contains("desativado. Fale com o administrador.", html, StringComparison.Ordinal);

        // Nenhum cookie de autenticação: a home continua exigindo login.
        using var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
        Assert.Contains("/login", homeResponse.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_EmpresaInativa_IsDeniedBeforeSignIn()
    {
        var (email, senha) = await CriarUsuarioDeEmpresaAsync(empresaAtiva: false, usuarioAtivo: true);

        using var client = fixture.CreateClient();
        var (token, handler) = await GetAntiforgeryFieldsAsync(client, "/login");

        using var loginResponse = await PostLoginAsync(client, token, handler, email, senha);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var html = await loginResponse.Content.ReadAsStringAsync();
        Assert.Contains("Empresa desativada. Fale com o administrador.", html, StringComparison.Ordinal);

        using var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
        Assert.Contains("/login", homeResponse.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    /// <summary>Garante o super admin da central (sem empresa, role SuperAdmin, ativo).</summary>
    private static async Task GarantirSuperAdminAsync(
        UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        await SeedData.GarantirRolesAsync(roleManager);

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
            // O reset de outra classe de teste pode ter removido a role/empresa do super admin.
            usuario.EmpresaId = null;
            usuario.Ativo = true;
            await userManager.UpdateAsync(usuario);
        }

        if (!await userManager.IsInRoleAsync(usuario, ApplicationRole.SuperAdmin))
        {
            await userManager.AddToRoleAsync(usuario, ApplicationRole.SuperAdmin);
        }
    }

    /// <summary>Garante a empresa demo (ativa) e o admin dela, ativo e na role Admin.</summary>
    private async Task GarantirEmpresaDemoEAdminAsync(
        UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        await using var db = fixture.CreateDbContext();

        var empresa = await db.Set<Empresa>()
            .FirstOrDefaultAsync(e => e.Identificador == SeedDemoData.IdentificadorEmpresaDemo);

        if (empresa is null)
        {
            empresa = new Empresa(SeedDemoData.IdentificadorEmpresaDemo, "Demo Ltda");
            db.Set<Empresa>().Add(empresa);
            await db.SaveChangesAsync();
        }
        else if (!empresa.Ativo)
        {
            empresa.Ativar();
            await db.SaveChangesAsync();
        }

        var admin = await userManager.FindByEmailAsync(SeedDemoData.EmailAdminDemo);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = SeedDemoData.EmailAdminDemo,
                Email = SeedDemoData.EmailAdminDemo,
                EmailConfirmed = true,
                Nome = SeedDemoData.NomeAdminDemo,
                EmpresaId = empresa.Id,
                Ativo = true,
            };

            var criacao = await userManager.CreateAsync(admin, SeedDemoData.SenhaAdminDemo);
            Assert.True(criacao.Succeeded, string.Join("; ", criacao.Errors.Select(e => e.Description)));
        }
        else if (admin.EmpresaId != empresa.Id || !admin.Ativo)
        {
            admin.EmpresaId = empresa.Id;
            admin.Ativo = true;
            await userManager.UpdateAsync(admin);
        }

        if (!await userManager.IsInRoleAsync(admin, ApplicationRole.Admin))
        {
            await userManager.AddToRoleAsync(admin, ApplicationRole.Admin);
        }
    }

    /// <summary>Cria uma empresa (ativa ou inativa) e um usuário Admin com e-mail único.</summary>
    private async Task<(string Email, string Senha)> CriarUsuarioDeEmpresaAsync(
        bool empresaAtiva, bool usuarioAtivo)
    {
        await using var db = fixture.CreateDbContext();

        var empresa = new Empresa($"teste-{Guid.NewGuid():N}", "Empresa de Teste");
        if (!empresaAtiva)
        {
            empresa.Desativar();
        }

        db.Set<Empresa>().Add(empresa);
        await db.SaveChangesAsync();

        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            await SeedData.GarantirRolesAsync(roleManager);

            var email = $"inativo-{Guid.NewGuid():N}@andregas.com.br";
            const string senha = "Senha@123";

            var usuario = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Nome = "Usuário de Teste",
                EmpresaId = empresa.Id,
                Ativo = usuarioAtivo,
            };

            var criacao = await userManager.CreateAsync(usuario, senha);
            Assert.True(criacao.Succeeded, string.Join("; ", criacao.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(usuario, ApplicationRole.Admin);

            return (email, senha);
        }
        finally
        {
            provider.Dispose();
        }
    }

    private static async Task<(string Token, string Handler)> GetAntiforgeryFieldsAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        var handler = Regex.Match(html, "name=\"_handler\" value=\"([^\"]*)\"").Groups[1].Value;

        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(string.IsNullOrEmpty(handler));

        return (token, handler);
    }

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string token, string handler, string email, string password)
    {
        var form = new Dictionary<string, string>
        {
            ["_handler"] = handler,
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "true",
        };

        return client.PostAsync("/login", new FormUrlEncodedContent(form));
    }
}

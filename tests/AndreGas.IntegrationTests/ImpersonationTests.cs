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
/// Testes de integração ponta a ponta da <b>impersonation entre empresas</b>: o super admin da
/// central "entra como" o admin de uma empresa (POST <c>/central/entrar-como</c>) e volta
/// (POST <c>/central/voltar</c>), ambos com antiforgery e troca de cookie.
/// </summary>
/// <remarks>
/// <para>
/// Estes testes são o limite real de segurança das duas operações: provam que (a) só o super admin
/// não-impersonando entra como empresa, (b) o principal impersonado age no tenant da empresa e
/// deixa de ser super admin, e (c) <c>/central/voltar</c> só restaura a central a partir de uma
/// sessão impersonada (a claim assinada <c>SuperAdminOriginalId</c>, nunca um id do corpo).
/// </para>
/// <para>
/// xUnit cria uma instância da classe por teste, então o <see cref="InitializeAsync"/> roda por
/// teste e (re)garante o super admin, a empresa demo e o admin demo — o Postgres do AppHost tem
/// volume persistente compartilhado com as classes que usam <c>DatabaseFixture</c>, que resetam as
/// tabelas.
/// </para>
/// </remarks>
public class ImpersonationTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>, IAsyncLifetime
{
    private const string SuperAdminEmail = SeedData.DefaultEmail;
    private const string SuperAdminPassword = SeedData.DefaultPassword;

    public async Task InitializeAsync()
    {
        await using (var db = fixture.CreateDbContext())
        {
            await GarantirEmpresaDemoAsync(db);
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

    [Fact]
    public async Task SuperAdmin_entra_como_admin_empresa_e_age_na_empresa()
    {
        using var client = fixture.CreateClient();
        await AutenticarAsync(client, SuperAdminEmail, SuperAdminPassword, destinoEsperado: "/central");

        // Controle positivo: antes da impersonation a central responde 200 para o super admin.
        using var centralAntes = await client.GetAsync("/central");
        Assert.Equal(HttpStatusCode.OK, centralAntes.StatusCode);

        var empresaId = await ObterEmpresaDemoIdAsync();
        var token = await ObterTokenAntiforgeryAsync(client);

        using var entrada = await PostEntrarComoAsync(client, token, empresaId);
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        Assert.Equal("/", Caminho(entrada.Headers.Location!));

        // O principal passou a ser o admin da empresa demo: a página de domínio abre e o app bar
        // exibe o nome da EMPRESA (prova que a claim EmpresaId veio do admin impersonado).
        using var estoque = await client.GetAsync("/estoque");
        Assert.Equal(HttpStatusCode.OK, estoque.StatusCode);
        var estoqueHtml = await estoque.Content.ReadAsStringAsync();
        Assert.Contains(SeedDemoData.NomeEmpresaDemo, estoqueHtml, StringComparison.Ordinal);

        // O badge de impersonation (estático, lido das claims) aparece no app bar.
        Assert.Contains("Entrando como", estoqueHtml, StringComparison.Ordinal);

        // E o principal impersonado NÃO é mais super admin: a central deixa de responder 200.
        using var centralDepois = await client.GetAsync("/central");
        AssertRedirecionadoParaSemAutorizacao(centralDepois);
    }

    [Fact]
    public async Task SuperAdmin_volta_para_central_apos_entrar_como()
    {
        using var client = fixture.CreateClient();
        await AutenticarAsync(client, SuperAdminEmail, SuperAdminPassword, destinoEsperado: "/central");

        var empresaId = await ObterEmpresaDemoIdAsync();
        var tokenEntrar = await ObterTokenAntiforgeryAsync(client);

        using var entrada = await PostEntrarComoAsync(client, tokenEntrar, empresaId);
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);

        // A página de domínio já abre como a empresa demo (controle positivo do passo anterior).
        using var estoqueImpersonado = await client.GetAsync("/estoque");
        Assert.Equal(HttpStatusCode.OK, estoqueImpersonado.StatusCode);

        // Voltar à central: o token vem da própria página de estoque (form do menu do usuário).
        var tokenVoltar = await ObterTokenAntiforgeryAsync(client);
        using var volta = await PostVoltarAsync(client, tokenVoltar);
        Assert.Equal(HttpStatusCode.Redirect, volta.StatusCode);
        Assert.Equal("/central", Caminho(volta.Headers.Location!));

        // A central volta a responder 200 (o cookie do super admin original foi reemitido)...
        using var central = await client.GetAsync("/central");
        Assert.Equal(HttpStatusCode.OK, central.StatusCode);
        var centralHtml = await central.Content.ReadAsStringAsync();
        Assert.Contains("Central", centralHtml, StringComparison.Ordinal);

        // ...e a página de domínio deixa de ser acessível (não é mais um usuário de empresa):
        // o super admin sem impersonation é mandado para a central.
        using var estoqueDepois = await client.GetAsync("/estoque");
        AssertRedirecionadoParaCentral(estoqueDepois);
    }

    [Fact]
    public async Task Admin_empresa_nao_pode_entrar_como()
    {
        using var client = fixture.CreateClient();
        await AutenticarAsync(client, SeedDemoData.EmailAdminDemo, SeedDemoData.SenhaAdminDemo, destinoEsperado: "/");

        var empresaId = await ObterEmpresaDemoIdAsync();
        var token = await ObterTokenAntiforgeryAsync(client);

        using var entrada = await PostEntrarComoAsync(client, token, empresaId);
        AssertRedirecionadoParaSemAutorizacao(entrada);

        // A sessão continua sendo a do admin da empresa demo, sem badge de impersonation.
        using var estoque = await client.GetAsync("/estoque");
        Assert.Equal(HttpStatusCode.OK, estoque.StatusCode);
        var html = await estoque.Content.ReadAsStringAsync();
        Assert.Contains(SeedDemoData.NomeEmpresaDemo, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Entrando como", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Voltar_sem_impersonacao_nao_autentica_como_super_admin()
    {
        using var client = fixture.CreateClient();
        await AutenticarAsync(client, SeedDemoData.EmailAdminDemo, SeedDemoData.SenhaAdminDemo, destinoEsperado: "/");

        var token = await ObterTokenAntiforgeryAsync(client);

        using var volta = await PostVoltarAsync(client, token);
        AssertRedirecionadoParaSemAutorizacao(volta);

        // Nada mudou: continua autenticado como o admin da empresa demo...
        using var estoque = await client.GetAsync("/estoque");
        Assert.Equal(HttpStatusCode.OK, estoque.StatusCode);

        // ...e não virou super admin (a central continua negada).
        using var central = await client.GetAsync("/central");
        AssertRedirecionadoParaSemAutorizacao(central);
    }

    [Fact]
    public async Task SuperAdmin_nao_acessa_pagina_de_dominio_sem_impersonar()
    {
        using var client = fixture.CreateClient();
        await AutenticarAsync(client, SuperAdminEmail, SuperAdminPassword, destinoEsperado: "/central");

        using var estoque = await client.GetAsync("/estoque");

        // Nem 500 (EmpresaIdRequerida estourando) nem 200 com dados: o super admin é mandado
        // para a central pela política ApenasEmpresa + RoleAwareNotAuthorized.
        AssertRedirecionadoParaCentral(estoque);
    }

    [Fact]
    public async Task Entrar_como_sem_token_antiforgery_e_rejeitado()
    {
        using var client = fixture.CreateClient();
        await AutenticarAsync(client, SuperAdminEmail, SuperAdminPassword, destinoEsperado: "/central");

        var empresaId = await ObterEmpresaDemoIdAsync();
        using var entrada = await PostEntrarComoAsync(client, token: null, empresaId);

        // Sem antiforgery a requisição é rejeitada antes do handler (400 pelo AntiforgeryService).
        Assert.Equal(HttpStatusCode.BadRequest, entrada.StatusCode);

        // E o principal não foi trocado: a central continua respondendo 200 ao super admin.
        using var central = await client.GetAsync("/central");
        Assert.Equal(HttpStatusCode.OK, central.StatusCode);
    }

    [Fact]
    public async Task Voltar_sem_token_antiforgery_e_rejeitado()
    {
        using var client = fixture.CreateClient();
        await AutenticarAsync(client, SuperAdminEmail, SuperAdminPassword, destinoEsperado: "/central");

        var empresaId = await ObterEmpresaDemoIdAsync();
        var tokenEntrar = await ObterTokenAntiforgeryAsync(client);
        using var entrada = await PostEntrarComoAsync(client, tokenEntrar, empresaId);
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);

        using var volta = await PostVoltarAsync(client, token: null);
        Assert.Equal(HttpStatusCode.BadRequest, volta.StatusCode);

        // Continua impersonando: o badge segue visível na página de domínio.
        using var estoque = await client.GetAsync("/estoque");
        Assert.Equal(HttpStatusCode.OK, estoque.StatusCode);
        Assert.Contains("Entrando como", await estoque.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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

    /// <summary>Caminho de um Location que pode ser absoluto ou relativo (LocalRedirect).</summary>
    private static string Caminho(Uri local)
    {
        var texto = local.OriginalString;
        return Uri.TryCreate(texto, UriKind.Absolute, out var absoluto) ? absoluto.AbsolutePath : texto;
    }

    private static void AssertRedirecionadoParaCentral(HttpResponseMessage resposta)
    {
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/central", Caminho(resposta.Headers.Location!));
    }

    /// <summary>Executa o POST do form de login (SSR estático) e confirma o destino pós-login.</summary>
    private static async Task AutenticarAsync(HttpClient client, string email, string senha, string destinoEsperado)
    {
        using var loginPage = await client.GetAsync("/login");
        var html = await loginPage.Content.ReadAsStringAsync();

        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        var handler = Regex.Match(html, "name=\"_handler\" value=\"([^\"]*)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(string.IsNullOrEmpty(handler));

        var form = new Dictionary<string, string>
        {
            ["_handler"] = handler,
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = email,
            ["Input.Password"] = senha,
            ["Input.RememberMe"] = "true",
        };

        using var loginResponse = await client.PostAsync("/login", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal(destinoEsperado, Caminho(loginResponse.Headers.Location!));
    }

    /// <summary>
    /// Lê um token antiforgery de uma página renderizada no servidor. Usa <c>/login</c> (SSR
    /// estático, <c>[AllowAnonymous]</c>) porque os forms com <c>&lt;AntiforgeryToken /&gt;</c> do
    /// app autenticado vivem dentro de um <c>MudMenu</c>, que só é renderizado no cliente — o HTML
    /// do prerender não os contém.
    /// </summary>
    private static async Task<string> ObterTokenAntiforgeryAsync(HttpClient client, string caminho = "/login")
    {
        using var response = await client.GetAsync(caminho);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;

        Assert.False(string.IsNullOrEmpty(token));
        return token;
    }

    private static Task<HttpResponseMessage> PostEntrarComoAsync(HttpClient client, string? token, Guid empresaId)
    {
        var form = new Dictionary<string, string> { ["empresaId"] = empresaId.ToString() };
        if (token is not null)
        {
            form["__RequestVerificationToken"] = token;
        }

        return client.PostAsync("/central/entrar-como", new FormUrlEncodedContent(form));
    }

    private static Task<HttpResponseMessage> PostVoltarAsync(HttpClient client, string? token)
    {
        var form = new Dictionary<string, string>();
        if (token is not null)
        {
            form["__RequestVerificationToken"] = token;
        }

        return client.PostAsync("/central/voltar", new FormUrlEncodedContent(form));
    }

    private async Task<Guid> ObterEmpresaDemoIdAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await db.Set<Empresa>()
            .Where(e => e.Identificador == SeedDemoData.IdentificadorEmpresaDemo)
            .Select(e => e.Id)
            .SingleAsync();
    }

    /// <summary>Garante a empresa demo (ativa) e devolve o Id.</summary>
    private static async Task<Guid> GarantirEmpresaDemoAsync(AppDbContext db)
    {
        var empresa = await db.Set<Empresa>()
            .FirstOrDefaultAsync(e => e.Identificador == SeedDemoData.IdentificadorEmpresaDemo);

        if (empresa is null)
        {
            empresa = new Empresa(SeedDemoData.IdentificadorEmpresaDemo, SeedDemoData.NomeEmpresaDemo);
            db.Set<Empresa>().Add(empresa);
            await db.SaveChangesAsync();
        }
        else if (!empresa.Ativo)
        {
            empresa.Ativar();
            await db.SaveChangesAsync();
        }

        return empresa.Id;
    }

    /// <summary>Garante o super admin da central (sem empresa, role SuperAdmin, ativo).</summary>
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

    /// <summary>Garante o admin da empresa demo (ativo, na role Admin).</summary>
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
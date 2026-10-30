using System.Security.Claims;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Identity;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Common.Authorization;
using RevendaApp.Web.Common.Tenancy;

namespace RevendaApp.UnitTests.Features.Auth;

/// <summary>
/// Testes do <see cref="ApplicationUserClaimsPrincipalFactory"/> — o ponto que garante a
/// invariante de tenancy: <b>todo</b> usuário com <c>EmpresaId</c> precisa receber a claim
/// <see cref="TenantContext.ClaimEmpresaId"/>, senão o <see cref="TenantContext"/> fail-closed
/// lança ao ler dados de domínio.
/// </summary>
public class ApplicationUserClaimsPrincipalFactoryTests
{
    [Fact]
    public async Task CreateAsync_usuario_de_empresa_emite_empresa_id_nome_e_cor()
    {
        await using var cenario = await Cenario.CriarAsync("Demo Ltda", "#0A1F44", ApplicationRole.Admin);

        var principal = await cenario.Factory.CreateAsync(cenario.Usuario);

        Assert.Equal(cenario.EmpresaId.ToString(), principal.FindFirst(TenantContext.ClaimEmpresaId)?.Value);
        Assert.Equal("Demo Ltda", principal.FindFirst(TenantContext.ClaimEmpresaNome)?.Value);
        Assert.Equal("#0A1F44", principal.FindFirst(TenantContext.ClaimEmpresaCor)?.Value);
        Assert.Equal("false", principal.FindFirst(TenantContext.ClaimEhSuperAdmin)?.Value);
    }

    [Fact]
    public async Task CreateAsync_empresa_ausente_ainda_emite_empresa_id_para_nao_alargar_o_tenant()
    {
        await using var cenario = await Cenario.CriarAsync(nomeEmpresa: null, corPrimaria: null, ApplicationRole.Vendedor);

        var principal = await cenario.Factory.CreateAsync(cenario.Usuario);

        // EmpresaId aponta para uma empresa sem linha correspondente: a claim EmpresaId é emitida
        // de qualquer forma (o filtro de tenant não pode virar "vê tudo" por omissão da claim).
        Assert.Equal(cenario.EmpresaId.ToString(), principal.FindFirst(TenantContext.ClaimEmpresaId)?.Value);
        Assert.Null(principal.FindFirst(TenantContext.ClaimEmpresaNome));
        Assert.Null(principal.FindFirst(TenantContext.ClaimEmpresaCor));
    }

    [Fact]
    public async Task CreateAsync_super_admin_emite_eh_super_admin_true_e_sem_empresa_id()
    {
        await using var cenario = await Cenario.CriarAsync(
            nomeEmpresa: null, corPrimaria: null, ApplicationRole.SuperAdmin, pertenceAEmpresa: false);

        var principal = await cenario.Factory.CreateAsync(cenario.Usuario);

        Assert.Null(principal.FindFirst(TenantContext.ClaimEmpresaId));
        Assert.Equal("true", principal.FindFirst(TenantContext.ClaimEhSuperAdmin)?.Value);
        Assert.True(principal.IsInRole(ApplicationRole.SuperAdmin));
    }

    [Fact]
    public async Task CreateAsync_admin_de_empresa_nao_e_super_admin()
    {
        await using var cenario = await Cenario.CriarAsync("Demo Ltda", corPrimaria: null, ApplicationRole.Admin);

        var principal = await cenario.Factory.CreateAsync(cenario.Usuario);

        Assert.False(principal.IsInRole(ApplicationRole.SuperAdmin));
        Assert.Equal("false", principal.FindFirst(TenantContext.ClaimEhSuperAdmin)?.Value);
        // Empresa sem cor cadastrada: a claim de cor não é emitida (o tema usa a cor padrão).
        Assert.Null(principal.FindFirst(TenantContext.ClaimEmpresaCor));
    }

    /// <summary>
    /// Monta o Identity em memória (UserManager/RoleManager), o usuário (com ou sem empresa) e a
    /// fábrica sob teste, mantendo o provider vivo até o fim do teste.
    /// </summary>
    private sealed class Cenario : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AppDbContext _db;

        private Cenario(ServiceProvider provider, AppDbContext db, ApplicationUser usuario, Guid empresaId, ApplicationUserClaimsPrincipalFactory factory)
        {
            _provider = provider;
            _db = db;
            Usuario = usuario;
            EmpresaId = empresaId;
            Factory = factory;
        }

        public ApplicationUser Usuario { get; }

        /// <summary>Empresa do usuário, ou um Guid sem linha correspondente no banco.</summary>
        public Guid EmpresaId { get; }

        public ApplicationUserClaimsPrincipalFactory Factory { get; }

        public static async Task<Cenario> CriarAsync(
            string? nomeEmpresa, string? corPrimaria, string roleNome, bool pertenceAEmpresa = true)
        {
            var banco = Guid.NewGuid().ToString();
            var db = InMemoryDbContextFactory.Create(nomeBanco: banco);

            var empresaId = Guid.NewGuid();
            if (nomeEmpresa is not null)
            {
                var empresa = new Empresa("demo", nomeEmpresa, corPrimaria: corPrimaria);
                db.Set<Empresa>().Add(empresa);
                await db.SaveChangesAsync();
                empresaId = empresa.Id;
            }

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(banco));
            services
                .AddIdentity<ApplicationUser, ApplicationRole>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = false;
                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<AppDbContext>();

            var provider = services.BuildServiceProvider();
            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();

            await roleManager.CreateAsync(new ApplicationRole(roleNome));

            var usuario = new ApplicationUser
            {
                UserName = "usuario@demo.revenda.app.com.br",
                Email = "usuario@demo.revenda.app.com.br",
                EmailConfirmed = true,
                Nome = "Usuário Teste",
                EmpresaId = pertenceAEmpresa ? empresaId : null,
                Ativo = true,
            };

            var criacao = await userManager.CreateAsync(usuario, "Senha@123");
            Assert.True(criacao.Succeeded, string.Join("; ", criacao.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(usuario, roleNome);

            var factory = new ApplicationUserClaimsPrincipalFactory(
                userManager,
                roleManager,
                Options.Create(new IdentityOptions()),
                db);

            return new Cenario(provider, db, usuario, empresaId, factory);
        }

        public async ValueTask DisposeAsync()
        {
            await _db.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
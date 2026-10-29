using System.Text.RegularExpressions;

using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Features.Auth;
using AndreGas.Web.Features.Seed;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Regressão de <b>vazamento cross-tenant no nível HTTP</b>.
/// </summary>
/// <remarks>
/// <para>
/// Bug histórico: o <c>AppDbContext</c> scoped era resolvido durante o prerender SSR (de dentro do
/// <c>SecurityStampValidator</c> da autenticação por cookie), quando o
/// <c>AuthenticationStateProvider</c> ainda não existia no escopo. O <c>TenantContext</c> degradava
/// para "não autenticado" (modo central) e o <c>AppDbContext</c> congelava esse tenant nulo por um
/// campo <c>readonly</c> — a página renderizava com dados de <b>todas</b> as empresas.
/// </para>
/// <para>
/// Estes testes logam como o admin da empresa demo e verificam que a resposta HTML <b>não</b>
/// contém produtos de outra empresa. É o teste que pina a correção (tenant avaliado por query +
/// principal HTTP no <c>TenantContext</c>).
/// </para>
/// <para>
/// <b>Por que não há teste HTTP para a lista de clientes:</b> <c>/clientes</c> usa
/// <c>MudTable.ServerData</c>, que não executa durante o prerender SSR — o HTML não contém
/// <i>nenhuma</i> linha de cliente, nem da própria empresa. Um assert de "não contém cliente da
/// empresa B" nessa página passaria mesmo com o vazamento presente (teste vacuoso). O isolamento
/// de clientes é coberto onde ele é real: filtro global do <c>AppDbContext</c>
/// (<c>TenantIsolationTests</c>) e o handler de listagem.
/// </para>
/// <para>
/// xUnit cria uma instância da classe por teste, então o <see cref="InitializeAsync"/> roda por
/// teste. Os nomes de dados usam um sufixo único porque o Postgres do AppHost tem volume
/// persistente (nada é limpo entre execuções de classes diferentes).
/// </para>
/// </remarks>
public class TenantLeakHttpTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>, IAsyncLifetime
{
    private readonly string _produtoOutraEmpresa = $"PRODUTO-EMPRESA-B-NAO-VAZAR-{Guid.NewGuid():N}";
    private readonly string _produtoEmpresaA = $"PRODUTO-EMPRESA-A-ESPERADO-{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        Guid empresaDemoId;
        Guid empresaBId;

        await using (var db = fixture.CreateDbContext())
        {
            empresaDemoId = await GarantirEmpresaAsync(db, SeedDemoData.IdentificadorEmpresaDemo, "Demo Ltda");
            empresaBId = await GarantirEmpresaAsync(db, "empresa-b-teste", "Empresa B Teste");

            // Produto da empresa B, com nome inconfundível. `EmpresaId` tem setter privado (a
            // tenancy na escrita é responsabilidade do interceptor), então a atribuição é feita
            // por SQL direto para simular dados legítimos de outra empresa.
            var produtoB = new Produto(_produtoOutraEmpresa, TipoProduto.Gas, 111m, 55m, 99m);
            db.Produtos.Add(produtoB);
            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Produtos\" SET \"EmpresaId\" = {0} WHERE \"Id\" = {1}", empresaBId, produtoB.Id);

            // E um produto da própria empresa demo: garante que a tela renderiza (a ausência do
            // produto de B precisa ser por filtro, não porque a lista veio vazia).
            var produtoDemo = new Produto(_produtoEmpresaA, TipoProduto.Gas, 120m, 60m, 100m);
            db.Produtos.Add(produtoDemo);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Produtos\" SET \"EmpresaId\" = {0} WHERE \"Id\" = {1}", empresaDemoId, produtoDemo.Id);
        }

        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            await SeedData.GarantirRolesAsync(roleManager);

            var admin = await userManager.FindByEmailAsync(SeedDemoData.EmailAdminDemo);
            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = SeedDemoData.EmailAdminDemo,
                    Email = SeedDemoData.EmailAdminDemo,
                    EmailConfirmed = true,
                    Nome = SeedDemoData.NomeAdminDemo,
                    EmpresaId = empresaDemoId,
                    Ativo = true,
                };

                var criacao = await userManager.CreateAsync(admin, SeedDemoData.SenhaAdminDemo);
                Assert.True(criacao.Succeeded, string.Join("; ", criacao.Errors.Select(e => e.Description)));
            }
            else
            {
                admin.EmpresaId = empresaDemoId;
                admin.Ativo = true;
                await userManager.UpdateAsync(admin);
            }

            if (!await userManager.IsInRoleAsync(admin, ApplicationRole.Admin))
            {
                await userManager.AddToRoleAsync(admin, ApplicationRole.Admin);
            }
        }
        finally
        {
            provider.Dispose();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Admin_da_empresa_A_nao_ve_produtos_da_empresa_B_no_estoque()
    {
        using var client = fixture.CreateClient();
        await AutenticarComoAdminDemoAsync(client);

        using var response = await client.GetAsync("/estoque");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // A página renderizou o estoque da empresa A...
        Assert.Contains(_produtoEmpresaA, html, StringComparison.Ordinal);

        // ...e NÃO vazou o produto da empresa B.
        Assert.DoesNotContain(_produtoOutraEmpresa, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_da_empresa_A_nao_ve_dados_da_empresa_B_na_home()
    {
        using var client = fixture.CreateClient();
        await AutenticarComoAdminDemoAsync(client);

        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // Branding dinâmico: o título traz o nome da EMPRESA logada. Além de provar que a página
        // renderizou de fato (controle positivo do assert de ausência abaixo), confirma que o
        // tenant resolvido é o da empresa demo.
        Assert.Contains($"Painel — {SeedDemoData.NomeEmpresaDemo}", html, StringComparison.Ordinal);
        Assert.DoesNotContain(_produtoOutraEmpresa, html, StringComparison.Ordinal);
    }

    /// <summary>Garante a empresa (idempotente pelo identificador único) e devolve o Id.</summary>
    private static async Task<Guid> GarantirEmpresaAsync(AppDbContext db, string identificador, string nome)
    {
        var existente = await db.Set<Empresa>()
            .FirstOrDefaultAsync(e => e.Identificador == identificador);

        if (existente is not null)
        {
            return existente.Id;
        }

        var empresa = new Empresa(identificador, nome);
        db.Set<Empresa>().Add(empresa);
        await db.SaveChangesAsync();

        return empresa.Id;
    }

    /// <summary>Executa o POST do form de login (SSR estático) e confirma o redirect de sucesso.</summary>
    private static async Task AutenticarComoAdminDemoAsync(HttpClient client)
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
            ["Input.Email"] = SeedDemoData.EmailAdminDemo,
            ["Input.Password"] = SeedDemoData.SenhaAdminDemo,
            ["Input.RememberMe"] = "true",
        };

        using var loginResponse = await client.PostAsync("/login", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/", loginResponse.Headers.Location!.AbsolutePath);
    }
}

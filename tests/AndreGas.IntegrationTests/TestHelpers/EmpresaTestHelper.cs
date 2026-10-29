using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Features.Auth;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.IntegrationTests.TestHelpers;

/// <summary>
/// Helper para criar <b>empresas reais</b> (e seus administradores) nos testes de integração.
/// </summary>
/// <remarks>
/// <para>
/// Complementa o <see cref="TenantIntegracao"/>, que representa a <b>empresa padrão</b> dos
/// testes. Use este helper quando o teste precisar de mais de uma empresa para provar isolamento
/// entre tenants (ver <c>TenancyIsolationTests</c>).
/// </para>
/// <para>
/// O Postgres do AppHost usa volume persistente, então os dados sobrevivem entre execuções de
/// classes diferentes. Por isso o <c>Identificador</c> (índice único) recebe um sufixo aleatório
/// por padrão — dois testes nunca colidem no mesmo slug.
/// </para>
/// </remarks>
public static class EmpresaTestHelper
{
    /// <summary>
    /// Cria (e salva) uma <see cref="Empresa"/> com dados mínimos válidos e identificador único.
    /// </summary>
    /// <param name="db">
    /// Contexto usado para persistir. Deve ser <b>central</b> (<c>CreateDbContextCentral()</c>) para
    /// que a empresa seja criada independentemente do tenant em teste.
    /// </param>
    /// <param name="nome">Nome exibido da empresa.</param>
    /// <param name="identificador">
    /// Slug único. Quando omitido, é derivado de <paramref name="nome"/> + sufixo aleatório curto.
    /// </param>
    public static async Task<Empresa> CriarEmpresaAsync(
        AppDbContext db, string nome, string? identificador = null)
    {
        ArgumentNullException.ThrowIfNull(db);

        var empresa = new Empresa(
            identificador ?? IdentificadorUnico(nome),
            nome,
            telefones: ["11999990000"],
            cnpj: "12345678000199",
            endereco: "Rua de Teste",
            numero: "100",
            cep: "01001000",
            bairro: "Centro",
            cidade: "São Paulo",
            estado: "SP");

        db.Set<Empresa>().Add(empresa);
        await db.SaveChangesAsync();

        return empresa;
    }

    /// <summary>
    /// Cria uma <see cref="Empresa"/> e um <see cref="ApplicationUser"/> na role
    /// <see cref="ApplicationRole.Admin"/>, com <c>EmpresaId</c> setado e <c>Ativo = true</c>.
    /// </summary>
    /// <remarks>
    /// Garante as roles antes de atribuir (a role pode não existir em banco recém-resetado) e
    /// <b>falha alto</b> se a criação do usuário ou a atribuição de role falharem — um teste que
    /// segue com um usuário inexistente seria vacuoso.
    /// </remarks>
    public static async Task<(Empresa Empresa, ApplicationUser Admin)> CriarEmpresaEAdminAsync(
        string connectionString, string nome, string identificador, string email, string senha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(identificador);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Empresa empresa;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var db = new AppDbContext(options))
        {
            empresa = await CriarEmpresaAsync(db, nome, identificador);
        }

        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(connectionString);
        try
        {
            await SeedData.GarantirRolesAsync(roleManager);

            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Nome = $"Admin {nome}",
                EmpresaId = empresa.Id,
                Ativo = true,
            };

            var criacao = await userManager.CreateAsync(admin, senha);
            if (!criacao.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Falha ao criar o admin da empresa '{nome}': {string.Join("; ", criacao.Errors.Select(e => e.Description))}");
            }

            var atribuicao = await userManager.AddToRoleAsync(admin, ApplicationRole.Admin);
            if (!atribuicao.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Falha ao atribuir a role Admin ao usuário '{email}': {string.Join("; ", atribuicao.Errors.Select(e => e.Description))}");
            }

            return (empresa, admin);
        }
        finally
        {
            provider.Dispose();
        }
    }

    /// <summary>Slug único derivado do nome, com sufixo aleatório curto.</summary>
    private static string IdentificadorUnico(string nome)
    {
        var baseSlug = new string(nome
            .ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');

        if (baseSlug.Length > 40)
        {
            baseSlug = baseSlug[..40].Trim('-');
        }

        if (baseSlug.Length == 0)
        {
            baseSlug = "empresa";
        }

        return $"{baseSlug}-{Guid.NewGuid().ToString("N")[..8]}";
    }
}
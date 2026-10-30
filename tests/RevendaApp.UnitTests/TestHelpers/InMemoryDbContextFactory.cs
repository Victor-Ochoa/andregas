using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.UnitTests.TestHelpers;

/// <summary>Cria instâncias de <see cref="AppDbContext"/> com o provider InMemory do EF Core,
/// para testar validators/handlers sem precisar de um Postgres real.</summary>
/// <remarks>
/// <para>
/// Passe <paramref name="empresaId"/> para simular um tenant específico: o filtro global isola os
/// dados por empresa e o <see cref="SetEmpresaIdInterceptor"/> preenche o <c>EmpresaId</c> das
/// linhas novas.
/// </para>
/// <para>
/// Passe <paramref name="tenant"/> quando o teste precisar de um <see cref="ITenantContext"/> com
/// mais nuances do que "empresa X" — por exemplo um super admin da central (<c>EmpresaId</c> nulo,
/// <c>EhSuperAdmin</c> verdadeiro), para exercitar o <c>EmpresaIdRequerida</c> dos handlers. Tem
/// precedência sobre <paramref name="empresaId"/>.
/// </para>
/// <para>
/// Sem <paramref name="empresaId"/> nem <paramref name="tenant"/> (o padrão), o contexto roda em
/// <b>modo central</b> — sem tenant injetado — enxergando e gravando todas as empresas, exatamente
/// como antes da migração multi-empresa. Isso mantém os testes existentes (que não exercitam
/// tenancy) intactos.
/// </para>
/// <para>
/// <paramref name="nomeBanco"/> permite compartilhar a mesma base InMemory entre vários contextos
/// (necessário para testar o isolamento entre empresas). Quando omitido, cada chamada cria uma
/// base isolada.
/// </para>
/// </remarks>
public static class InMemoryDbContextFactory
{
    public static AppDbContext Create(Guid? empresaId = null, string? nomeBanco = null, ITenantContext? tenant = null)
    {
        var banco = nomeBanco ?? Guid.NewGuid().ToString();
        tenant ??= empresaId is null ? null : new FakeTenantContext(empresaId.Value);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(banco)
            .AddInterceptors(new SetEmpresaIdInterceptor(tenant))
            .Options;

        return new AppDbContext(options, tenant);
    }
}
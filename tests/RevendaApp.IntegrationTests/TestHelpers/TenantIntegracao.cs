using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.IntegrationTests.TestHelpers;

/// <summary>
/// <b>Empresa padrão</b> dos testes de integração que constroem handlers de domínio diretamente.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DatabaseFixture.CreateDbContext()"/> cria o <c>AppDbContext</c> já <b>filtrado por
/// esta empresa</b> e com o <c>SetEmpresaIdInterceptor</c> apontando para ela. Consequência: os
/// testes que usam <c>fixture.CreateDbContext()</c> + <c>TenantIntegracao.Contexto()</c> exercitam
/// o filtro global e o tagueamento de <c>EmpresaId</c> na escrita, como em produção.
/// </para>
/// <para>
/// <b>Limite de cobertura:</b> como estes testes só têm dados desta empresa, eles provam que os
/// fluxos funcionam sob o filtro — <b>não</b> provam isolamento entre empresas. A cobertura real de
/// isolamento (duas empresas reais, dados nas duas) fica em <c>TenancyIsolationTests</c>, que usa
/// <see cref="EmpresaTestHelper"/> para criar os tenants adicionais e
/// <c>DatabaseFixture.CreateDbContext(Guid?)</c> para agir como cada uma.
/// </para>
/// <para>
/// A empresa precisa existir na tabela <c>Empresas</c> (FK Restrict de <c>AspNetUsers.EmpresaId</c>
/// para criar usuários): <c>DatabaseFixture.ResetDatabaseAsync()</c> a insere ao final de cada
/// reset.
/// </para>
/// </remarks>
public static class TenantIntegracao
{
    /// <summary>Id fixo da empresa padrão usada pelos testes de integração.</summary>
    public static readonly Guid EmpresaId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Novo <see cref="ITenantContext"/> apontando para <see cref="EmpresaId"/>.</summary>
    public static FakeTenantContext Contexto() => new(EmpresaId);
}

/// <summary>Implementação mínima de <see cref="ITenantContext"/> para os testes de integração.</summary>
/// <remarks>
/// <paramref name="ehSuperAdmin"/> permite simular o <b>modo central</b> (super admin sem empresa),
/// exigido pelos handlers da central (<c>Features/Central/*</c>), que recusam qualquer outro
/// usuário. O padrão <c>false</c> mantém o comportamento dos testes de domínio existentes.
/// </remarks>
public sealed class FakeTenantContext(Guid? empresaId = null, bool ehSuperAdmin = false) : ITenantContext
{
    public Guid? EmpresaId { get; } = empresaId;

    public string NomeEmpresa => string.Empty;

    public string CorPrimaria => string.Empty;

    public bool EhSuperAdmin { get; } = ehSuperAdmin;

    public bool Impersonando => false;

    public Guid EmpresaIdRequerida(string acao) =>
        EmpresaId ?? throw new InvalidOperationException(
            $"A operação '{acao}' exige uma empresa selecionada.");
}
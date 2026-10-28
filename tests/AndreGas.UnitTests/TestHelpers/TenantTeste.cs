namespace AndreGas.UnitTests.TestHelpers;

/// <summary>
/// Empresa (tenant) padrão dos testes unitários que exercitam handlers de domínio. Os handlers
/// exigem empresa selecionada (<c>EmpresaIdRequerida</c>), então esses testes precisam de um
/// contexto com empresa — e o mesmo <see cref="Guid"/> precisa ser usado no <c>AppDbContext</c>
/// (filtro global + interceptor) e no handler.
/// </summary>
public static class TenantTeste
{
    /// <summary>Id fixo da empresa usada pelos testes (estável entre contextos do mesmo teste).</summary>
    public static readonly Guid EmpresaId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Novo <see cref="FakeTenantContext"/> apontando para <see cref="EmpresaId"/>.</summary>
    public static FakeTenantContext Contexto() => new(EmpresaId);
}
namespace RevendaApp.Infrastructure.Tenancy;

/// <summary>
/// Implementação de <see cref="ITenantContext"/> com a empresa (tenant) fixada no construtor.
/// </summary>
/// <remarks>
/// Usada por rotinas que precisam operar <b>em nome de uma empresa específica</b> sem depender do
/// usuário autenticado — principalmente o seed de demonstração, que roda no startup (sem circuito
/// Blazor, portanto sem claims) e mesmo assim precisa gravar os dados de domínio sob a empresa demo.
/// Com um <see cref="TenantFixo"/>, o filtro global do <c>AppDbContext</c> e o
/// <see cref="SetEmpresaIdInterceptor"/> apontam para a mesma empresa.
/// </remarks>
public sealed class TenantFixo(Guid empresaId) : ITenantContext
{
    public Guid? EmpresaId { get; } = empresaId;

    public string NomeEmpresa => string.Empty;

    public string CorPrimaria => string.Empty;

    public bool EhSuperAdmin => false;

    public bool Impersonando => false;

    public Guid EmpresaIdRequerida(string acao) => empresaId;
}
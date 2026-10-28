using AndreGas.Infrastructure.Tenancy;

namespace AndreGas.IntegrationTests.TestHelpers;

/// <summary>
/// Tenant fixo usado pelos testes de integração que constroem handlers de domínio diretamente.
/// Os handlers exigem empresa selecionada (<c>EmpresaIdRequerida</c>); este helper apenas evita
/// repetir o <see cref="Guid"/> em cada construção.
/// </summary>
/// <remarks>
/// <b>Temporário (Task 6):</b> a suíte de integração ainda não cria empresas reais — a fixture
/// company-aware é da Task 7. Até lá, estes testes podem falhar em tempo de execução (os dados
/// gravados pelo <c>DatabaseFixture</c> em modo central não aparecem para um contexto com tenant);
/// o objetivo aqui é apenas manter o projeto compilando.
/// </remarks>
public static class TenantIntegracao
{
    /// <summary>Id fixo da empresa usada pelos testes de integração.</summary>
    public static readonly Guid EmpresaId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Novo <see cref="ITenantContext"/> apontando para <see cref="EmpresaId"/>.</summary>
    public static FakeTenantContext Contexto() => new(EmpresaId);
}

/// <summary>Implementação mínima de <see cref="ITenantContext"/> para os testes de integração.</summary>
public sealed class FakeTenantContext(Guid? empresaId = null) : ITenantContext
{
    public Guid? EmpresaId { get; } = empresaId;

    public string NomeEmpresa => string.Empty;

    public string CorPrimaria => string.Empty;

    public bool EhSuperAdmin => false;

    public bool Impersonando => false;

    public Guid EmpresaIdRequerida(string acao) =>
        EmpresaId ?? throw new InvalidOperationException(
            $"A operação '{acao}' exige uma empresa selecionada.");
}
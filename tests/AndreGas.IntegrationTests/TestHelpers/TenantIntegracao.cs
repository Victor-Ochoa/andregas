using AndreGas.Infrastructure.Tenancy;

namespace AndreGas.IntegrationTests.TestHelpers;

/// <summary>
/// Tenant fixo usado pelos testes de integração que constroem handlers de domínio diretamente.
/// </summary>
/// <remarks>
/// <para>
/// <b>Limite de cobertura (importante):</b> estes testes passam um tenant ao handler apenas para
/// satisfazer a guarda <c>EmpresaIdRequerida</c> dos handlers de domínio (Task 6). O
/// <c>DatabaseFixture.CreateDbContext()</c> cria o <c>AppDbContext</c> <b>sem</b> tenant — portanto
/// em modo central, sem filtro global por empresa. Consequência: estes testes <b>não exercitam</b>
/// o filtro global e <b>não provam isolamento por empresa</b>; eles provam que os fluxos
/// continuam funcionando com a guarda ativa. A cobertura real de isolamento por empresa (empresas
/// reais, contexto com tenant) é da <b>Task 7</b>, que substitui este helper.
/// </para>
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
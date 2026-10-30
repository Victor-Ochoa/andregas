using AndreGas.Infrastructure.Tenancy;

namespace AndreGas.UnitTests.TestHelpers;

/// <summary>
/// Implementação falsa de <see cref="ITenantContext"/> para testes: permite fixar a empresa
/// (tenant) — ou deixá-la nula, simulando a central/super admin — sem depender de claims.
/// </summary>
public sealed class FakeTenantContext : ITenantContext
{
    public FakeTenantContext(
        Guid? empresaId = null,
        string nomeEmpresa = "",
        string corPrimaria = "",
        bool ehSuperAdmin = false,
        bool impersonando = false)
    {
        EmpresaId = empresaId;
        NomeEmpresa = nomeEmpresa;
        CorPrimaria = corPrimaria;
        EhSuperAdmin = ehSuperAdmin;
        Impersonando = impersonando;
    }

    public Guid? EmpresaId { get; }

    public string NomeEmpresa { get; }

    public string CorPrimaria { get; }

    public bool EhSuperAdmin { get; }

    public bool Impersonando { get; }

    public Guid EmpresaIdRequerida(string acao) =>
        EmpresaId ?? throw new InvalidOperationException(
            $"A operação '{acao}' exige uma empresa selecionada.");
}
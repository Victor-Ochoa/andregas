using AndreGas.Web.Common.Authorization;

namespace AndreGas.UnitTests.TestHelpers;

/// <summary>
/// Fake de <see cref="IUsuarioAutenticado"/> para testes: permite simular usuário admin, não-admin
/// ou lançar, sem depender do circuito Blazor e sem biblioteca de mocking.
/// </summary>
public sealed class FakeUsuarioAutenticado(bool ehAdmin) : IUsuarioAutenticado
{
    public Task<bool> EhAdministradorAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ehAdmin);

    public Task RequerAdminAsync(CancellationToken cancellationToken = default)
        => ehAdmin
            ? Task.CompletedTask
            : throw new InvalidOperationException("Somente administradores podem executar esta operação.");
}
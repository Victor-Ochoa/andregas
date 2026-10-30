using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using RevendaApp.Domain.Entities;

namespace RevendaApp.IntegrationTests.TestHelpers;

/// <summary>
/// Interceptor que deixa o <c>SaveChanges</c> que grava o <see cref="RegistroAuditoria"/>
/// <b>concluir</b> e só então lança — simulando a falha no <c>CommitAsync</c> da transação
/// explícita.
/// </summary>
/// <remarks>
/// <para>
/// Complementa <see cref="FalhaAoGravarAuditoria"/>, que falha <b>antes</b> de a linha chegar ao
/// banco. Aqui a linha de auditoria já foi <b>escrita</b> dentro da transação; a exceção sobe em
/// <c>SavedChangesAsync</c>, o handler nunca chama <c>CommitAsync</c> e o <c>Dispose</c> da
/// transação faz o rollback. É a prova de que o rollback desfaz o registro <b>já rastreado e
/// gravado</b>, junto com a ação.
/// </para>
/// <para>
/// O <c>SaveChanges</c> anterior (a ação em si) não é afetado: o flag só liga quando há um
/// <see cref="RegistroAuditoria"/> <c>Added</c> no tracker.
/// </para>
/// </remarks>
public sealed class FalhaAposGravarAuditoria : SaveChangesInterceptor
{
    private bool _auditoriaFoiGravada;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _auditoriaFoiGravada = eventData.Context?.ChangeTracker
            .Entries<RegistroAuditoria>()
            .Any(entrada => entrada.State == EntityState.Added) ?? false;

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_auditoriaFoiGravada)
        {
            _auditoriaFoiGravada = false;
            throw new InvalidOperationException("falha simulada ao commitar a auditoria já gravada");
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
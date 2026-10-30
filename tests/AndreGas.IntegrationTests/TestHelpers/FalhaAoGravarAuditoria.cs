using AndreGas.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AndreGas.IntegrationTests.TestHelpers;

/// <summary>
/// Interceptor que <b>falha o <c>SaveChanges</c></b> sempre que houver um
/// <see cref="RegistroAuditoria"/> novo no change tracker.
/// </summary>
/// <remarks>
/// <para>
/// É a prova de <b>atomicidade ação + auditoria</b>: quando o handler grava a ação e o registro no
/// <b>mesmo</b> <c>SaveChanges</c> (ou dentro da mesma transação explícita), a falha impede a ação
/// de persistir. Se o handler quebrar essa garantia — dois <c>SaveChanges</c>, ou um
/// <c>CommitAsync</c> antes do save da auditoria — a ação <b>já estará</b> no banco quando este
/// interceptor lançar, e o teste fica vermelho.
/// </para>
/// <para>
/// Instalado junto do <c>SetEmpresaIdInterceptor</c> nas opções do <c>AppDbContext</c> do teste,
/// exatamente como o interceptor de tenancy é instalado em produção.
/// </para>
/// </remarks>
public sealed class FalhaAoGravarAuditoria : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var temAuditoria = eventData.Context?.ChangeTracker
            .Entries<RegistroAuditoria>()
            .Any(entrada => entrada.State == EntityState.Added) ?? false;

        if (temAuditoria)
        {
            throw new InvalidOperationException("falha simulada ao gravar auditoria");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
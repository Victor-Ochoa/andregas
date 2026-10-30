using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using RevendaApp.Domain.Entities;

namespace RevendaApp.Infrastructure.Tenancy;

/// <summary>
/// Interceptor de <c>SaveChanges</c> que preenche automaticamente o <c>EmpresaId</c> (tenant) das
/// entidades de domínio recém-inseridas (<see cref="EntityState.Added"/>) que ainda estão com o
/// valor padrão (<see cref="Guid.Empty"/>). Os handlers de domínio <b>não</b> setam
/// <c>EmpresaId</c> na mão: esta é a camada única de tenancy na escrita.
/// </summary>
/// <remarks>
/// <para>
/// Com uma empresa ativa no <see cref="ITenantContext"/>, toda linha de domínio nova recebe esse
/// <c>EmpresaId</c> (via <see cref="ITenantContext.EmpresaIdRequerida"/>). Se houver um
/// <see cref="ITenantContext"/> porém sem empresa selecionada (modo central/super admin),
/// <see cref="ITenantContext.EmpresaIdRequerida"/> lança — não existe linha de domínio "órfã".
/// </para>
/// <para>
/// Sem <see cref="ITenantContext"/> injetado (<c>null</c> — contexto de design-time, seed legado e
/// testes que não exercitam tenancy) o interceptor é no-op, preservando o comportamento anterior.
/// </para>
/// </remarks>
public sealed class SetEmpresaIdInterceptor(ITenantContext? tenant) : SaveChangesInterceptor
{
    private const string NomePropriedadeEmpresaId = "EmpresaId";

    /// <summary>Entidades de domínio que pertencem a uma empresa (todas as 10 tabelas de domínio).</summary>
    private static readonly HashSet<Type> EntidadesDeDominio =
    [
        typeof(Cliente),
        typeof(Produto),
        typeof(Venda),
        typeof(ItemVenda),
        typeof(Pagamento),
        typeof(MovimentacaoEstoque),
        typeof(HistoricoEstoque),
        typeof(PrecoPersonalizadoCliente),
        typeof(EmprestimoVasilhame),
        typeof(RecorrenciaCompra),
    ];

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        PreencherEmpresaId(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        PreencherEmpresaId(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void PreencherEmpresaId(DbContext? context)
    {
        // Sem tenant injetado (design-time, seed legado, testes sem tenancy): no-op.
        if (context is null || tenant is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added || !EntidadesDeDominio.Contains(entry.Metadata.ClrType))
            {
                continue;
            }

            var empresaIdAtual = entry.Property(NomePropriedadeEmpresaId).CurrentValue;
            if (empresaIdAtual is Guid valorAtual && valorAtual != Guid.Empty)
            {
                // Já preenchido explicitamente (ex.: seed com empresa definida) — respeita.
                continue;
            }

            // Lança se não houver empresa selecionada (modo central tentando gravar domínio).
            entry.Property(NomePropriedadeEmpresaId).CurrentValue =
                tenant.EmpresaIdRequerida($"salvar {entry.Metadata.ClrType.Name}");
        }
    }
}
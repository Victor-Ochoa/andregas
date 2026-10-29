using AndreGas.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AndreGas.Web.Common;

/// <summary>
/// Executa uma unidade de trabalho que precisa de uma <b>transação explícita</b> de forma
/// compatível com a <i>execution strategy</i> de retry do EF Core.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que isto existe:</b> o <c>Program.cs</c> chama <c>EnrichNpgsqlDbContext&lt;AppDbContext&gt;()</c>,
/// que liga <c>EnableRetryOnFailure()</c>. Com uma execution strategy de retry, <b>todo</b>
/// <c>SaveChanges</c> dentro de <c>Database.BeginTransactionAsync()</c> lança
/// <c>InvalidOperationException: The configured execution strategy 'NpgsqlRetryingExecutionStrategy'
/// does not support user-initiated transactions</c>. A transação precisa ser aberta <b>dentro</b>
/// de <c>Database.CreateExecutionStrategy().ExecuteAsync(...)</c>, que é o que este helper faz.
/// </para>
/// <para>
/// <b>Contrato com a operação:</b> ela recebe a transação e deve chamar <c>CommitAsync</c> quando
/// quiser persistir. Se retornar sem commitar (recusa de negócio), o <c>Dispose</c> da transação
/// faz o rollback — não é preciso desfazer nada na mão.
/// </para>
/// <para>
/// <b>Retry:</b> em falha transitória o bloco inteiro é reexecutado, com o <b>mesmo</b>
/// <see cref="AppDbContext"/>. A operação é responsável por limpar o change tracker no início de
/// cada tentativa (senão a entidade da tentativa anterior continua <c>Added</c> e seria inserida
/// duas vezes).
/// </para>
/// </remarks>
public static class TransacaoComRetry
{
    /// <summary>
    /// Executa <paramref name="operacao"/> dentro de uma transação aberta sob a execution strategy
    /// do contexto.
    /// </summary>
    /// <param name="db">Contexto cuja estratégia de execução será usada.</param>
    /// <param name="operacao">Unidade de trabalho; deve chamar <c>CommitAsync</c> para persistir.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <param name="nivelIsolamento">
    /// Nível de isolamento da transação. Use <see cref="System.Data.IsolationLevel.Serializable"/>
    /// quando a decisão depender de uma leitura que outra transação concorrente pode invalidar
    /// (ex.: "este é o último administrador ativo?"): sob <c>ReadCommitted</c> duas desativações
    /// simultâneas dos dois únicos admins passariam as duas, deixando a empresa sem administrador.
    /// </param>
    public static async Task<T> ExecutarAsync<T>(
        AppDbContext db,
        Func<IDbContextTransaction, Task<T>> operacao,
        CancellationToken cancellationToken,
        System.Data.IsolationLevel? nivelIsolamento = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(operacao);

        var estrategia = db.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(
            async ct =>
            {
                await using var transacao = nivelIsolamento is { } nivel
                    ? await db.Database.BeginTransactionAsync(nivel, ct)
                    : await db.Database.BeginTransactionAsync(ct);

                return await operacao(transacao);
            },
            cancellationToken);
    }
}
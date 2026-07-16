using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// Review #4 §4 + §6: Npgsql <c>EnableRetryOnFailure</c> rejects user-initiated
/// transactions unless they run inside the execution strategy. Callers return
/// <c>commit: false</c> on validation failures so the transaction rolls back.
/// </summary>
public static class TransactionHelper
{
    public static Task<TResult> ExecuteInTransactionAsync<TResult>(
        DbContext context,
        Func<Task<(bool Commit, TResult Result)>> action) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await context.Database.BeginTransactionAsync();
            var (commit, result) = await action();
            if (commit)
                await tx.CommitAsync();
            return result;
        });
}

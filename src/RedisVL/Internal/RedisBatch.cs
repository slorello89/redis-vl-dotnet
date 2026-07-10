namespace RedisVL.Internal;

/// <summary>
/// Runs a per-item Redis operation over a list of items with bounded concurrency, preserving
/// input order in the returned results.
/// </summary>
/// <remarks>
/// <para>
/// Each operation is <em>dispatched</em> synchronously in a tight loop and only then awaited as a
/// group. Because StackExchange.Redis queues a command and returns immediately (the reply is not
/// awaited inline), dispatching a whole batch this way pipelines every command in the batch onto
/// the connection instead of paying one round trip per item. A batch of <c>N</c> single-command
/// operations therefore costs roughly one round trip rather than <c>N</c>.
/// </para>
/// <para>
/// Work is issued in batches of at most <see cref="DefaultBatchSize" /> commands, awaiting each
/// batch before dispatching the next. This bounds the number of in-flight commands (and buffered
/// results) for very large inputs while still collapsing the round trips within a batch.
/// </para>
/// <para>
/// <strong>Failure semantics.</strong> Commands in a batch are dispatched concurrently and are not
/// transactional. If any dispatched command faults, the exception surfaces once the batch is
/// awaited, but sibling commands in the same batch may already have been applied and are not rolled
/// back; commands in later batches are not dispatched. Callers that need all-or-nothing behaviour
/// should validate every item before invoking this helper so that no command is dispatched for a
/// structurally invalid input.
/// </para>
/// </remarks>
internal static class RedisBatch
{
    /// <summary>The maximum number of commands dispatched concurrently before awaiting a batch.</summary>
    internal const int DefaultBatchSize = 1000;

    /// <summary>Runs <paramref name="operation" /> for every item, returning results in input order.</summary>
    public static async Task<TResult[]> RunAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<TItem, int, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken,
        int batchSize = DefaultBatchSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var results = new TResult[items.Count];
        for (var start = 0; start < items.Count; start += batchSize)
        {
            var length = Math.Min(batchSize, items.Count - start);
            var tasks = new List<Task<TResult>>(length);

            // Dispatch is synchronous, so checking the token between commands stops us queueing
            // further work the instant a cancellation is observed without breaking pipelining.
            var canceled = false;
            for (var offset = 0; offset < length && !canceled; offset++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    canceled = true;
                    break;
                }

                var index = start + offset;
                tasks.Add(operation(items[index], index, cancellationToken));
            }

            // Await the commands that were actually dispatched (even when cancelling) so a faulted
            // in-flight command is never left unobserved.
            var batchResults = await Task.WhenAll(tasks).ConfigureAwait(false);
            batchResults.CopyTo(results, start);

            if (canceled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return results;
    }

    /// <summary>Runs a result-less <paramref name="operation" /> for every item.</summary>
    public static async Task RunAsync<TItem>(
        IReadOnlyList<TItem> items,
        Func<TItem, int, CancellationToken, Task> operation,
        CancellationToken cancellationToken,
        int batchSize = DefaultBatchSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        for (var start = 0; start < items.Count; start += batchSize)
        {
            var length = Math.Min(batchSize, items.Count - start);
            var tasks = new List<Task>(length);

            var canceled = false;
            for (var offset = 0; offset < length && !canceled; offset++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    canceled = true;
                    break;
                }

                var index = start + offset;
                tasks.Add(operation(items[index], index, cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (canceled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}

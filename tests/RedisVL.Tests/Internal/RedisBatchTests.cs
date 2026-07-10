using RedisVL.Internal;

namespace RedisVL.Tests.Internal;

public sealed class RedisBatchTests
{
    [Fact]
    public async Task RunAsync_ReturnsResultsInInputOrder_EvenWhenCompletionIsReordered()
    {
        var items = Enumerable.Range(0, 200).ToList();

        var results = await RedisBatch.RunAsync(
            items,
            // Delay lower indices longer so completion order differs from input order; the helper
            // must still place each result at its original index.
            async (item, _, _) =>
            {
                await Task.Delay(item % 7 == 0 ? 15 : 1).ConfigureAwait(false);
                return item * 2;
            },
            CancellationToken.None);

        Assert.Equal(items.Select(static item => item * 2).ToArray(), results);
    }

    [Fact]
    public async Task RunAsync_DispatchesEveryCommandInABatchBeforeAwaitingReplies()
    {
        // Every operation returns an uncompleted task, so if the helper awaited one command before
        // issuing the next it would stall after the first dispatch. Pipelining issues them all.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatched = 0;

        var run = RedisBatch.RunAsync(
            Enumerable.Range(0, 5).ToList(),
            (_, _, _) =>
            {
                Interlocked.Increment(ref dispatched);
                return gate.Task;
            },
            CancellationToken.None);

        Assert.Equal(5, Volatile.Read(ref dispatched));
        Assert.False(run.IsCompleted);

        gate.SetResult();
        await run;
    }

    [Fact]
    public async Task RunAsync_DispatchesOneBoundedBatchAtATime()
    {
        var gates = Enumerable.Range(0, 5)
            .Select(static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var dispatched = 0;

        var run = RedisBatch.RunAsync(
            Enumerable.Range(0, 5).ToList(),
            (_, index, _) =>
            {
                Interlocked.Increment(ref dispatched);
                return gates[index].Task;
            },
            CancellationToken.None,
            batchSize: 2);

        // Only the first batch of two is in flight; the helper caps concurrent commands at batchSize.
        Assert.Equal(2, Volatile.Read(ref dispatched));

        gates[0].SetResult();
        gates[1].SetResult();
        await WaitForAsync(() => Volatile.Read(ref dispatched) >= 4);
        Assert.Equal(4, Volatile.Read(ref dispatched));

        gates[2].SetResult();
        gates[3].SetResult();
        await WaitForAsync(() => Volatile.Read(ref dispatched) >= 5);
        Assert.Equal(5, Volatile.Read(ref dispatched));

        gates[4].SetResult();
        await run;
    }

    [Fact]
    public async Task RunAsync_StopsDispatchingOnceCancellationIsObserved()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var dispatched = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() => RedisBatch.RunAsync(
            Enumerable.Range(0, 5).ToList(),
            (_, index, _) =>
            {
                Interlocked.Increment(ref dispatched);
                if (index == 0)
                {
                    cancellationTokenSource.Cancel();
                }

                return Task.CompletedTask;
            },
            cancellationTokenSource.Token,
            batchSize: 10));

        // The token was cancelled while item 0 was dispatched, so items 1-4 are never issued.
        Assert.Equal(1, dispatched);
    }

    [Fact]
    public async Task RunAsync_AwaitsAlreadyDispatchedTasksBeforeSurfacingCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var completed = 0;
        var firstTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = RedisBatch.RunAsync(
            Enumerable.Range(0, 5).ToList(),
            (_, index, _) =>
            {
                if (index == 0)
                {
                    // Dispatched but not yet complete; the helper must observe it before throwing.
                    return firstTask.Task.ContinueWith(_ => Interlocked.Increment(ref completed), TaskScheduler.Default);
                }

                cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token,
            batchSize: 10);

        // Cancellation is requested during item 1's dispatch, but item 0 is still in flight.
        Assert.False(run.IsCompleted);

        firstTask.SetResult();
        await Assert.ThrowsAsync<OperationCanceledException>(() => run);
        Assert.Equal(1, Volatile.Read(ref completed));
    }

    [Fact]
    public async Task RunAsync_WithEmptyInput_DoesNotInvokeOperation()
    {
        var invoked = false;

        var results = await RedisBatch.RunAsync(
            Array.Empty<int>(),
            (_, _, _) =>
            {
                invoked = true;
                return Task.FromResult(0);
            },
            CancellationToken.None);

        Assert.Empty(results);
        Assert.False(invoked);
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for the batch helper to advance.");
    }
}

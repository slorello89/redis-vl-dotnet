using RedisVlDotNet.Indexes;
using System.Globalization;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Indexes;

internal static class RedisSearchTestEnvironment
{
    public const string RedisUrlEnvironmentVariable = "REDIS_VL_REDIS_URL";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable(RedisUrlEnvironmentVariable);

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(ConnectionString)
            ? $"Set {RedisUrlEnvironmentVariable} to run Redis Stack integration tests."
            : null;

    public static async Task<IConnectionMultiplexer> ConnectAsync() =>
        await ConnectionMultiplexer.ConnectAsync(ConnectionString!);

    public static async Task WaitForIndexDocumentCountAsync(
        SearchIndex index,
        long expectedDocumentCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedDocumentCount);

        await WaitForAsync(
            async () =>
            {
                var info = await index.InfoAsync(cancellationToken).ConfigureAwait(false);
                return TryGetDocumentCount(info, out var actualDocumentCount) &&
                    actualDocumentCount >= expectedDocumentCount;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task WaitForAsync(
        Func<Task<bool>> predicate,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        var delay = pollInterval ?? TimeSpan.FromMilliseconds(100);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await predicate().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for Redis integration test state to become ready.");
    }

    private static bool TryGetDocumentCount(SearchIndexInfo info, out long documentCount)
    {
        ArgumentNullException.ThrowIfNull(info);

        var value = info.GetString("num_docs");
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedCount))
        {
            documentCount = Convert.ToInt64(Math.Truncate(parsedCount));
            return true;
        }

        documentCount = 0;
        return false;
    }
}

using RedisVL.Indexes;
using System.Globalization;
using StackExchange.Redis;

namespace RedisVL.Tests.Indexes;

internal static class RedisSearchTestEnvironment
{
    public const string RedisUrlEnvironmentVariable = "REDIS_VL_REDIS_URL";
    public const string RedisProtocolEnvironmentVariable = "REDIS_VL_REDIS_PROTOCOL";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable(RedisUrlEnvironmentVariable);

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(ConnectionString)
            ? $"Set {RedisUrlEnvironmentVariable} to run Redis integration tests."
            : null;

    public static ConfigurationOptions CreateOptions()
    {
        var options = ConfigurationOptions.Parse(ConnectionString!);
        var protocol = Environment.GetEnvironmentVariable(RedisProtocolEnvironmentVariable)?.Trim();
        if (!string.IsNullOrEmpty(protocol) && (protocol is "3" || protocol.Equals("resp3", StringComparison.OrdinalIgnoreCase)))
        {
            options.Protocol = RedisProtocol.Resp3;
        }

        return options;
    }

    public static async Task<IConnectionMultiplexer> ConnectAsync() =>
        await ConnectionMultiplexer.ConnectAsync(CreateOptions());

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
                    actualDocumentCount >= expectedDocumentCount &&
                    IsIndexingComplete(info);
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

    /// <summary>
    /// Determines whether RediSearch has finished indexing the documents behind an index.
    /// </summary>
    /// <remarks>
    /// RediSearch increments <c>num_docs</c> as soon as a document is added to the index, but the
    /// document's field values may not yet be returnable by a projection query (<c>FT.SEARCH ... RETURN</c>).
    /// Under CI's slower, parallel environment this races: the count is satisfied while a subsequent
    /// projection read still comes back missing a field, and the mapper throws on the absent required field.
    /// <c>indexing</c> (1 while a background scan is running) and <c>percent_indexed</c> (fraction of the
    /// index built) reflect the true readiness of those field values, so gating on them in addition to the
    /// document count removes the flake. Attributes that are absent are treated as complete so the helper
    /// keeps working against servers that do not report them.
    /// </remarks>
    private static bool IsIndexingComplete(SearchIndexInfo info)
    {
        if (info.GetString("indexing") is { } indexing &&
            double.TryParse(indexing, NumberStyles.Float, CultureInfo.InvariantCulture, out var indexingValue) &&
            indexingValue != 0d)
        {
            return false;
        }

        if (info.GetString("percent_indexed") is { } percentIndexed &&
            double.TryParse(percentIndexed, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue) &&
            percentValue < 1d)
        {
            return false;
        }

        return true;
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

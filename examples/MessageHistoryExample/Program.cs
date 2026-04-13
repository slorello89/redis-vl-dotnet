using RedisVlDotNet.Caches;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Schema;
using RedisVlDotNet.Workflows;
using StackExchange.Redis;

var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";
await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

var history = new SemanticMessageHistory(
    database,
    new SemanticMessageHistoryOptions(
        "example-history",
        new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            2),
        distanceThreshold: 0.25d,
        keyNamespace: "samples"));
var embeddingGenerator = new KeywordEmbeddingGenerator();

try
{
    await history.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));

    await history.AppendAsync("session-1", "user", "I need help with a refund", embeddingGenerator, new { turn = 1, topic = "billing" });
    await history.AppendAsync("session-1", "assistant", "I can help check your refund status", embeddingGenerator, new { turn = 2, topic = "billing" });
    await history.AppendAsync("session-1", "assistant", "Your shipment will arrive tomorrow", embeddingGenerator, new { turn = 3, topic = "shipping" });

    var recent = await history.GetRecentAsync("session-1", limit: 2);
    var relevant = await history.GetRelevantAsync("session-1", "refund update", embeddingGenerator, limit: 2);

    Console.WriteLine("Recent session messages:");
    foreach (var message in recent)
    {
        Console.WriteLine($"- #{message.Sequence} [{message.Role}] {message.Content} metadata={message.Metadata ?? "null"}");
    }

    Console.WriteLine();
    Console.WriteLine("Semantically relevant messages:");
    foreach (var match in relevant)
    {
        Console.WriteLine($"- distance={match.Distance:F3} #{match.Message.Sequence} [{match.Message.Role}] {match.Message.Content}");
    }
}
finally
{
    if (await history.ExistsAsync())
    {
        await history.DropAsync(deleteDocuments: true);
    }
}

file sealed class KeywordEmbeddingGenerator : ITextEmbeddingGenerator
{
    public Task<float[]> GenerateAsync(string input, CancellationToken cancellationToken = default)
    {
        var normalized = input.ToLowerInvariant();
        var embedding = normalized.Contains("refund", StringComparison.Ordinal)
            ? new[] { 1f, 0f }
            : normalized.Contains("ship", StringComparison.Ordinal)
                ? new[] { 0f, 1f }
                : new[] { 0.5f, 0.5f };

        return Task.FromResult(embedding);
    }
}

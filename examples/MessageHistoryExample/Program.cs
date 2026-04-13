using RedisVlDotNet.Indexes;
using RedisVlDotNet.Workflows;
using StackExchange.Redis;

var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";
await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

var history = new MessageHistory(database, new MessageHistoryOptions("example-history", "samples"));

try
{
    await history.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));

    await history.AppendAsync("session-1", "user", "Hi there", new { turn = 1, channel = "cli" });
    await history.AppendAsync("session-1", "assistant", "Hello. How can I help?", new { turn = 2, sentiment = "neutral" });
    await history.AppendAsync("session-1", "user", "Show me my last two messages", new { turn = 3 });

    var recent = await history.GetRecentAsync("session-1", limit: 2);
    var assistantMessages = await history.GetRecentAsync("session-1", limit: 5, role: "assistant");

    Console.WriteLine("Recent session messages:");
    foreach (var message in recent)
    {
        Console.WriteLine($"- #{message.Sequence} [{message.Role}] {message.Content} metadata={message.Metadata ?? "null"}");
    }

    Console.WriteLine();
    Console.WriteLine("Assistant-only messages:");
    foreach (var message in assistantMessages)
    {
        Console.WriteLine($"- #{message.Sequence} [{message.Role}] {message.Content}");
    }
}
finally
{
    if (await history.ExistsAsync())
    {
        await history.DropAsync(deleteDocuments: true);
    }
}

using RedisVl.Indexes;
using RedisVl.Queries;
using RedisVl.Rerankers;
using RedisVl.Rerankers.Cohere;
using RedisVl.Schema;
using StackExchange.Redis;

var apiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Set COHERE_API_KEY before running the Cohere reranker example.");
}

var model = Environment.GetEnvironmentVariable("COHERE_RERANK_MODEL") ?? "rerank-v4.0-pro";
var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";

var reranker = new CohereTextReranker(
    model,
    apiKey,
    new CohereRerankerOptions
    {
        ClientName = "redis-vl-dotnet-example",
        MaxTokensPerDocument = 512
    });

await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

var schema = new SearchSchema(
    new IndexDefinition("cohere-reranker-example", "article:", StorageType.Json),
    [
        new TextFieldDefinition("title"),
        new TextFieldDefinition("body")
    ]);
var index = new SearchIndex(database, schema);

var articles =
    new[]
    {
        new Article("reset-password", "Reset your password", "Open Settings, select Security, and follow the password reset prompts."),
        new Article("billing-history", "Export billing history", "Open Billing and download invoices or payment history as CSV."),
        new Article("rotate-token", "Rotate API tokens", "Create a replacement token first, update clients, then revoke the old token.")
    };

try
{
    await index.CreateAsync(new CreateIndexOptions(skipIfExists: true));
    await index.LoadJsonAsync(articles);

    var initialResults = await index.SearchAsync<Article>(
        new TextQuery("password | token", ["title", "body"], limit: 3));

    var rerankResults = await reranker.RerankAsync(
        new RerankRequest(
            "How do I reset my password?",
            initialResults.Documents
                .Select(article => new RerankDocument(
                    $"{article.Title}\n{article.Body}",
                    article.Id,
                    article))
                .ToArray(),
            topN: 2));

    Console.WriteLine("Initial Redis search order:");
    foreach (var article in initialResults.Documents)
    {
        Console.WriteLine($"- {article.Id}: {article.Title}");
    }

    Console.WriteLine();
    Console.WriteLine("Cohere reranked order:");
    foreach (var result in rerankResults)
    {
        var article = (Article)result.Document.Metadata!;
        Console.WriteLine($"- {article.Id}: {article.Title} (score: {result.Score:F4})");
    }
}
finally
{
    if (await index.ExistsAsync())
    {
        await index.DropAsync(deleteDocuments: true);
    }
}

public sealed record Article(string Id, string Title, string Body);

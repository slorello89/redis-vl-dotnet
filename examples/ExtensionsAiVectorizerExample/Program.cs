using Microsoft.Extensions.AI;
using RedisVL.Vectorizers.ExtensionsAI;

using var generator = new KeywordEmbeddingGenerator();
var vectorizer = new ExtensionsAiTextVectorizer(generator);

var singleEmbedding = await vectorizer.VectorizeAsync("redis vector search");
var batchEmbeddings = await vectorizer.VectorizeAsync(
    [
        "redis vector search",
        "semantic cache"
    ]);

Console.WriteLine("Single embedding:");
Console.WriteLine($"- [{string.Join(", ", singleEmbedding.Select(static value => value.ToString("F2")))}]");

Console.WriteLine();
Console.WriteLine("Batch embeddings:");
for (var index = 0; index < batchEmbeddings.Count; index++)
{
    Console.WriteLine($"- {index}: [{string.Join(", ", batchEmbeddings[index].Select(static value => value.ToString("F2")))}]");
}

internal sealed class KeywordEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedValue = value.ToLowerInvariant();
            embeddings.Add(new Embedding<float>(
                new float[]
                {
                    normalizedValue.Contains("redis", StringComparison.Ordinal) ? 1f : 0f,
                    normalizedValue.Contains("vector", StringComparison.Ordinal) ? 1f : 0f,
                    normalizedValue.Contains("cache", StringComparison.Ordinal) ? 1f : 0f
                }));
        }

        return Task.FromResult(embeddings);
    }
}

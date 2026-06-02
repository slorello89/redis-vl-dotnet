using Microsoft.Extensions.AI;
using OpenAI.Embeddings;
using RedisVL.Vectorizers.ExtensionsAI;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "Set OPENAI_API_KEY before running the Microsoft.Extensions.AI vectorizer example.");
}

var model = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_MODEL") ?? "text-embedding-3-small";
var dimensionsValue = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_DIMENSIONS");
int? dimensions = string.IsNullOrWhiteSpace(dimensionsValue) ? null : int.Parse(dimensionsValue);

var embeddingClient = new EmbeddingClient(model, apiKey);
using var embeddingGenerator = embeddingClient.AsIEmbeddingGenerator(dimensions);
var vectorizer = new ExtensionsAiTextVectorizer(embeddingGenerator);

var singleInput = "redis vector search";
var batchInputs = new[]
{
    "redis vector search",
    "semantic cache"
};

var singleEmbedding = await vectorizer.VectorizeAsync(singleInput);
var batchEmbeddings = await vectorizer.VectorizeAsync(batchInputs);

Console.WriteLine("OpenAI-backed Microsoft.Extensions.AI adapter");
Console.WriteLine($"- model: {model}");
Console.WriteLine($"- single input dimension: {singleEmbedding.Length}");

Console.WriteLine();
Console.WriteLine("Batch embeddings:");
for (var index = 0; index < batchEmbeddings.Count; index++)
{
    Console.WriteLine(
        $"- {index}: input='{batchInputs[index]}' dimension={batchEmbeddings[index].Length} first3=[{string.Join(", ", batchEmbeddings[index].Take(3).Select(static value => value.ToString("F4")))}]");
}

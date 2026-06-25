using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;
using OpenAI.Embeddings;
using RedisVL.Connectors.VectorData;
using StackExchange.Redis;

// This example shows Semantic Kernel consuming the RedisVL Microsoft.Extensions.VectorData (MEVD)
// connector. Because SK's vector-store/text-search abstractions are built on MEVD, the same
// RedisVLVectorStore / RedisVLCollection used in the VectorDataConnectorExample plugs straight into
// SK's VectorStoreTextSearch<T> for retrieval-augmented generation - no Redis-specific SK connector
// needed. Query and document embeddings are produced by OpenAI through Microsoft.Extensions.AI.

// text-embedding-3-* models emit 1536-dimensional vectors by default; this must match the
// [VectorStoreVector(...)] dimension on the Movie record below.
const int EmbeddingDimensions = 1536;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Set OPENAI_API_KEY before running the Semantic Kernel connector example.");
}

var embeddingModel = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_MODEL") ?? "text-embedding-3-small";
var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";

Console.WriteLine($"Connecting to Redis at {redisUrl}...");
using var redis = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = redis.GetDatabase();

// OpenAI embeddings exposed as a Microsoft.Extensions.AI IEmbeddingGenerator.
using IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
    new EmbeddingClient(embeddingModel, apiKey).AsIEmbeddingGenerator(EmbeddingDimensions);

// 1. The RedisVL MEVD store and collection - identical to the plain MEVD example.
var store = new RedisVLVectorStore(database);
var movies = store.GetCollection<string, Movie>("sk-example-movies");

await store.EnsureCollectionDeletedAsync("sk-example-movies");
await movies.EnsureCollectionExistsAsync();

// 2. Seed records, embedding each summary with OpenAI.
var catalog = new[]
{
    new Movie { Id = "thematrix", Title = "The Matrix", Year = 1999, Summary = "A hacker learns reality is a simulation and joins a rebellion against the machines." },
    new Movie { Id = "arrival", Title = "Arrival", Year = 2016, Summary = "A linguist decodes the language of aliens who arrive on Earth in towering ships." },
    new Movie { Id = "heat", Title = "Heat", Year = 1995, Summary = "A detective hunts a disciplined crew of bank robbers across Los Angeles." },
    new Movie { Id = "interstellar", Title = "Interstellar", Year = 2014, Summary = "Astronauts travel through a wormhole near Saturn seeking a new home for humanity." },
};

var summaryEmbeddings = await embeddingGenerator.GenerateAsync(catalog.Select(movie => movie.Summary));
for (var index = 0; index < catalog.Length; index++)
{
    catalog[index].Embedding = summaryEmbeddings[index].Vector;
}

await movies.UpsertAsync(catalog);

// 3. Wrap the RedisVL collection in SK's VectorStoreTextSearch. SK embeds the query string with the
//    same generator, then calls the RedisVL connector's vector search.
var textSearch = new VectorStoreTextSearch<Movie>(
    movies,
    embeddingGenerator,
    stringMapper: new MapFromResultToString(result => ((Movie)result).Summary),
    resultMapper: new MapFromResultToTextSearchResult(result =>
    {
        var movie = (Movie)result;
        return new TextSearchResult(movie.Summary) { Name = movie.Title };
    }));

const string question = "aliens arriving from distant worlds and astronauts traveling through space";

Console.WriteLine($"\nQuestion: {question}\n");
Console.WriteLine("SK GetTextSearchResultsAsync (RAG retrieval over RedisVL):");
var searchResults = await textSearch.GetTextSearchResultsAsync(question, new TextSearchOptions { Top = 3 });
await foreach (var result in searchResults.Results)
{
    Console.WriteLine($"  - {result.Name}: {result.Value}");
}

// 4. SK's generic TextSearchOptions<TRecord> carries a LINQ filter, which SK passes straight through
//    to the RedisVL connector's vector search (translated to a RedisVL FilterExpression).
ITextSearch<Movie> filterableSearch = textSearch;
Console.WriteLine("\nSame query, filtered with a LINQ predicate (m => m.Year >= 2000):");
var filteredResults = await filterableSearch.GetTextSearchResultsAsync(
    question,
    new TextSearchOptions<Movie> { Top = 3, Filter = m => m.Year >= 2000 });
await foreach (var result in filteredResults.Results)
{
    Console.WriteLine($"  - {result.Name}: {result.Value}");
}

// 5. The same text search can be exposed to a Kernel as a plugin for grounded prompts / tool calling.
var searchPlugin = textSearch.CreateWithGetTextSearchResults("RedisVLSearch");
Console.WriteLine($"\nRegistered SK plugin '{searchPlugin.Name}' with function(s): {string.Join(", ", searchPlugin.Select(f => f.Name))}");

Console.WriteLine("\nDone.");

internal sealed class Movie
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int Year { get; set; }

    [VectorStoreData]
    public string Summary { get; set; } = string.Empty;

    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineDistance, IndexKind = IndexKind.Flat)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}

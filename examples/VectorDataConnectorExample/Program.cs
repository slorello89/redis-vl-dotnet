using System.Linq.Expressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using OpenAI.Embeddings;
using RedisVL.Connectors.VectorData;
using RedisVL.Connectors.VectorData.ChatMemory;
using RedisVL.Workflows;
using StackExchange.Redis;

// text-embedding-3-* models emit 1536-dimensional vectors by default; this must match the
// [VectorStoreVector(...)] dimension on the Movie record below.
const int EmbeddingDimensions = 1536;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Set OPENAI_API_KEY before running the vector-data connector example.");
}

var embeddingModel = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_MODEL") ?? "text-embedding-3-small";
var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";

Console.WriteLine($"Connecting to Redis at {redisUrl}...");
using var redis = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = redis.GetDatabase();

// OpenAI embeddings exposed as a Microsoft.Extensions.AI IEmbeddingGenerator.
using var embeddingGenerator = new EmbeddingClient(embeddingModel, apiKey)
    .AsIEmbeddingGenerator(EmbeddingDimensions);

// 1. Create a Microsoft.Extensions.VectorData store backed by RedisVL.
//    The same VectorStore / VectorStoreCollection types are consumable by Semantic Kernel.
var store = new RedisVLVectorStore(database);
var movies = store.GetCollection<string, Movie>("vectordata-example-movies");

await store.EnsureCollectionDeletedAsync("vectordata-example-movies");
await movies.EnsureCollectionExistsAsync();

// 2. Embed each movie summary with OpenAI, then upsert. Vectors round-trip as JSON arrays.
var catalog = new[]
{
    new Movie { Id = "thematrix", Title = "The Matrix", Genre = "scifi", Year = 1999, Summary = "A hacker discovers reality is a simulation and joins a rebellion against intelligent machines." },
    new Movie { Id = "heat", Title = "Heat", Genre = "crime", Year = 1995, Summary = "A detective hunts a disciplined crew of bank robbers across Los Angeles." },
    new Movie { Id = "arrival", Title = "Arrival", Genre = "scifi", Year = 2016, Summary = "A linguist decodes the language of aliens who arrive on Earth in towering ships." },
    new Movie { Id = "se7en", Title = "Se7en", Genre = "crime", Year = 1995, Summary = "Two detectives track a serial killer who stages murders around the seven deadly sins." },
    new Movie { Id = "interstellar", Title = "Interstellar", Genre = "scifi", Year = 2014, Summary = "Astronauts travel through a wormhole near Saturn seeking a new home for humanity." },
};

var summaryEmbeddings = await embeddingGenerator.GenerateAsync(catalog.Select(movie => movie.Summary));
for (var index = 0; index < catalog.Length; index++)
{
    catalog[index].Embedding = summaryEmbeddings[index].Vector;
}

await movies.UpsertAsync(catalog);

// 3. Fetch a single record by key.
var fetched = await movies.GetAsync("arrival");
Console.WriteLine($"\nFetched by key: {fetched?.Title} ({fetched?.Year})");

// 4. Vector search with a LINQ metadata pre-filter (translated to a RedisVL FilterExpression).
var query = (await embeddingGenerator.GenerateAsync(["aliens making first contact with humanity"]))[0].Vector;
Console.WriteLine("\nTop sci-fi matches for the query (vector search + LINQ filter m => m.Genre == \"scifi\"):");
await foreach (var result in movies.SearchAsync(
                   query,
                   top: 3,
                   new VectorSearchOptions<Movie> { Filter = movie => movie.Genre == "scifi" }))
{
    Console.WriteLine($"  {result.Record.Title,-12} score={result.Score:F4}");
}

// 5. Filtered (non-vector) retrieval - the LINQ predicates below are translated by the connector
//    into RedisVL FilterExpressions and run as FT.SEARCH queries.
var preferredGenres = new[] { "scifi", "noir" };
var linqQueries = new (string Description, Expression<Func<Movie, bool>> Filter)[]
{
    ("equality:           m => m.Genre == \"crime\"", m => m.Genre == "crime"),
    ("numeric range:      m => m.Year >= 2000", m => m.Year >= 2000),
    ("AND:                m => m.Genre == \"crime\" && m.Year == 1995", m => m.Genre == "crime" && m.Year == 1995),
    ("OR:                 m => m.Year < 1996 || m.Year > 2015", m => m.Year < 1996 || m.Year > 2015),
    ("IN (Contains):      preferredGenres.Contains(m.Genre)", m => preferredGenres.Contains(m.Genre)),
    ("negation:           m => !(m.Genre == \"scifi\")", m => !(m.Genre == "scifi")),
};

foreach (var (description, filter) in linqQueries)
{
    var titles = new List<string>();
    await foreach (var movie in movies.GetAsync(filter, top: 10))
    {
        titles.Add($"{movie.Title} ({movie.Year})");
    }

    Console.WriteLine($"\nLINQ {description}");
    Console.WriteLine($"  -> {string.Join(", ", titles.OrderBy(t => t))}");
}

// 6. Chat-memory store on top of the MessageHistory workflow.
var chat = new RedisVLChatMessageStore(
    database,
    new MessageHistoryOptions("vectordata-example-chat"));
await chat.CreateAsync();

const string sessionId = "session-42";
await chat.AddMessagesAsync(sessionId, new[]
{
    new ChatMessage(ChatRole.User, "Recommend a sci-fi movie."),
    new ChatMessage(ChatRole.Assistant, "Try Arrival (2016)."),
    new ChatMessage(ChatRole.User, "Anything older?"),
});

Console.WriteLine("\nChat history (chronological):");
foreach (var message in await chat.GetMessagesAsync(sessionId))
{
    Console.WriteLine($"  [{message.Role.Value}] {message.Text}");
}

Console.WriteLine("\nDone.");

internal sealed class Movie
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Genre { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int Year { get; set; }

    [VectorStoreData]
    public string Summary { get; set; } = string.Empty;

    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineDistance, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}

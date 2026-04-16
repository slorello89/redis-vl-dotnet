using System.Security.Cryptography;
using System.Text;
using RedisVl.Caches;
using RedisVl.Indexes;
using RedisVl.Queries;
using RedisVl.Schema;
using RedisVl.Vectorizers;
using StackExchange.Redis;

namespace RedisVl.Workflows;

public sealed class SemanticRouter
{
    private readonly IDatabase _database;
    private readonly SearchIndex _index;

    public SemanticRouter(IDatabase database, SemanticRouterOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _database = database;
        Options = options;
        _index = new SearchIndex(
            database,
            new SearchSchema(
                new IndexDefinition(CreateIndexName(options), CreateKeyPrefix(options), StorageType.Hash),
                [
                    new TextFieldDefinition(options.RouteNameFieldName),
                    new TextFieldDefinition(options.ReferenceFieldName),
                    new VectorFieldDefinition(options.EmbeddingFieldName, options.EmbeddingFieldAttributes)
                ]));
    }

    public SemanticRouterOptions Options { get; }

    public string Name => Options.Name;

    public string? KeyNamespace => Options.KeyNamespace;

    public double DistanceThreshold => Options.DistanceThreshold;

    public bool Create(CreateIndexOptions? options = null) =>
        CreateAsync(options).GetAwaiter().GetResult();

    public Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default) =>
        _index.CreateAsync(options, cancellationToken);

    public bool Exists() =>
        ExistsAsync().GetAwaiter().GetResult();

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        _index.ExistsAsync(cancellationToken);

    public void Drop(bool deleteDocuments = false) =>
        DropAsync(deleteDocuments).GetAwaiter().GetResult();

    public Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default) =>
        _index.DropAsync(deleteDocuments, cancellationToken);

    public string AddRoute(string routeName, string reference, float[] embedding) =>
        AddRouteAsync(routeName, reference, embedding).GetAwaiter().GetResult();

    public string AddRoute(string routeName, string reference, ITextVectorizer vectorizer) =>
        AddRouteAsync(routeName, reference, vectorizer).GetAwaiter().GetResult();

    public async Task<string> AddRouteAsync(
        string routeName,
        string reference,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        var normalizedRouteName = NormalizeRouteName(routeName);
        var normalizedReference = NormalizeReference(reference);
        ArgumentNullException.ThrowIfNull(embedding);

        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(normalizedRouteName, normalizedReference);
        await _database.HashSetAsync(
            key,
            [
                new HashEntry(Options.RouteNameFieldName, normalizedRouteName),
                new HashEntry(Options.ReferenceFieldName, normalizedReference),
                new HashEntry(Options.EmbeddingFieldName, EmbeddingsCache.EncodeFloat32(embedding))
            ]).WaitAsync(cancellationToken).ConfigureAwait(false);

        return key!;
    }

    public async Task<string> AddRouteAsync(
        string routeName,
        string reference,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var embedding = await vectorizer.VectorizeAsync(NormalizeReference(reference), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await AddRouteAsync(routeName, reference, embedding, cancellationToken).ConfigureAwait(false);
    }

    public SemanticRouteMatch? Route(string input, float[] embedding) =>
        RouteAsync(input, embedding).GetAwaiter().GetResult();

    public SemanticRouteMatch? Route(string input, ITextVectorizer vectorizer) =>
        RouteAsync(input, vectorizer).GetAwaiter().GetResult();

    public async Task<SemanticRouteMatch?> RouteAsync(
        string input,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        var normalizedInput = NormalizeInput(input);
        ArgumentNullException.ThrowIfNull(embedding);

        cancellationToken.ThrowIfCancellationRequested();

        var results = await _index.SearchAsync<SemanticRouteDocument>(
            VectorRangeQuery.FromFloat32(
                Options.EmbeddingFieldName,
                embedding,
                DistanceThreshold,
                returnFields: [Options.RouteNameFieldName, Options.ReferenceFieldName],
                scoreAlias: "distance",
                limit: 1),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var match = results.Documents.FirstOrDefault();
        return match is null
            ? null
            : new SemanticRouteMatch(normalizedInput, match.RouteName, match.Reference, match.Distance);
    }

    public async Task<SemanticRouteMatch?> RouteAsync(
        string input,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var normalizedInput = NormalizeInput(input);
        var embedding = await vectorizer.VectorizeAsync(normalizedInput, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await RouteAsync(normalizedInput, embedding, cancellationToken).ConfigureAwait(false);
    }

    internal RedisKey CreateKey(string routeName, string reference)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{routeName}\n{reference}")));
        return $"{CreateKeyPrefix(Options)}{hash}";
    }

    private static string CreateIndexName(SemanticRouterOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic-router:{options.Name}"
            : $"semantic-router:{options.Name}:{options.KeyNamespace}";

    private static string CreateKeyPrefix(SemanticRouterOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic-router:{options.Name}:"
            : $"semantic-router:{options.Name}:{options.KeyNamespace}:";

    private static string NormalizeInput(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        return input;
    }

    private static string NormalizeRouteName(string routeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);
        return routeName;
    }

    private static string NormalizeReference(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return reference;
    }

    private sealed record SemanticRouteDocument(string RouteName, string Reference, double Distance);
}

public sealed record SemanticRouteMatch(string Input, string RouteName, string Reference, double Distance);

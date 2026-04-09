using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Caches;

public sealed class SemanticCache
{
    private readonly IDatabase _database;
    private readonly SearchIndex _index;

    public SemanticCache(IDatabase database, SemanticCacheOptions options)
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
                    new TextFieldDefinition(options.PromptFieldName),
                    new TextFieldDefinition(options.ResponseFieldName),
                    new VectorFieldDefinition(options.EmbeddingFieldName, options.EmbeddingFieldAttributes)
                ]));
    }

    public SemanticCacheOptions Options { get; }

    public string Name => Options.Name;

    public string? KeyNamespace => Options.KeyNamespace;

    public TimeSpan? TimeToLive => Options.TimeToLive;

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

    public SemanticCacheHit? Check(string prompt, float[] embedding) =>
        CheckAsync(prompt, embedding).GetAwaiter().GetResult();

    public SemanticCacheHit? Check(string prompt, ITextEmbeddingGenerator embeddingGenerator) =>
        CheckAsync(prompt, embeddingGenerator).GetAwaiter().GetResult();

    public async Task<SemanticCacheHit?> CheckAsync(
        string prompt,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        NormalizePrompt(prompt);
        ArgumentNullException.ThrowIfNull(embedding);

        cancellationToken.ThrowIfCancellationRequested();

        var results = await _index.SearchAsync<SemanticCacheSearchDocument>(
            VectorRangeQuery.FromFloat32(
                Options.EmbeddingFieldName,
                embedding,
                DistanceThreshold,
                returnFields: [Options.PromptFieldName, Options.ResponseFieldName],
                scoreAlias: "distance",
                limit: 1),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var document = results.Documents.FirstOrDefault();
        if (document is null)
        {
            return null;
        }

        return new SemanticCacheHit(document.Prompt, document.Response, document.Distance);
    }

    public async Task<SemanticCacheHit?> CheckAsync(
        string prompt,
        ITextEmbeddingGenerator embeddingGenerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(embeddingGenerator);

        var embedding = await embeddingGenerator.GenerateAsync(NormalizePrompt(prompt), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await CheckAsync(prompt, embedding, cancellationToken).ConfigureAwait(false);
    }

    public string Store(string prompt, string response, float[] embedding) =>
        StoreAsync(prompt, response, embedding).GetAwaiter().GetResult();

    public string Store(string prompt, string response, ITextEmbeddingGenerator embeddingGenerator) =>
        StoreAsync(prompt, response, embeddingGenerator).GetAwaiter().GetResult();

    public async Task<string> StoreAsync(
        string prompt,
        string response,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        var normalizedPrompt = NormalizePrompt(prompt);
        var normalizedResponse = NormalizeResponse(response);
        ArgumentNullException.ThrowIfNull(embedding);

        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(normalizedPrompt);
        await _database.HashSetAsync(
            key,
            [
                new HashEntry(Options.PromptFieldName, normalizedPrompt),
                new HashEntry(Options.ResponseFieldName, normalizedResponse),
                new HashEntry(Options.EmbeddingFieldName, EmbeddingsCache.EncodeFloat32(embedding))
            ]).WaitAsync(cancellationToken).ConfigureAwait(false);

        if (TimeToLive.HasValue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _database.KeyExpireAsync(key, TimeToLive).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return key!;
    }

    public async Task<string> StoreAsync(
        string prompt,
        string response,
        ITextEmbeddingGenerator embeddingGenerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(embeddingGenerator);

        var embedding = await embeddingGenerator.GenerateAsync(NormalizePrompt(prompt), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await StoreAsync(prompt, response, embedding, cancellationToken).ConfigureAwait(false);
    }

    internal RedisKey CreateKey(string prompt)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prompt)));
        return $"{CreateKeyPrefix(Options)}{hash}";
    }

    private static string CreateIndexName(SemanticCacheOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic-cache:{options.Name}"
            : $"semantic-cache:{options.Name}:{options.KeyNamespace}";

    private static string CreateKeyPrefix(SemanticCacheOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic:{options.Name}:"
            : $"semantic:{options.Name}:{options.KeyNamespace}:";

    private static string NormalizePrompt(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return prompt;
    }

    private static string NormalizeResponse(string response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);
        return response;
    }

    private sealed record SemanticCacheSearchDocument(string Prompt, string Response, double Distance);
}

public sealed record SemanticCacheHit(string Prompt, string Response, double Distance);

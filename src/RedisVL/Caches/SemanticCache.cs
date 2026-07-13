using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Internal;
using RedisVL.Queries;
using RedisVL.Schema;
using RedisVL.Vectorizers;
using StackExchange.Redis;

namespace RedisVL.Caches;

/// <summary>
/// A semantic (embedding-similarity) cache for prompt/response pairs backed by a RediSearch vector index.
/// Lookups return previously stored responses whose prompt embedding is within the configured distance threshold.
/// </summary>
public sealed class SemanticCache
{
    private readonly IDatabase _database;
    private readonly SearchIndex _index;
    private readonly JsonSerializerOptions _serializerOptions;
    private long _hitCount;
    private long _missCount;

    /// <summary>Initializes a new <see cref="SemanticCache" /> over the given database and options.</summary>
    /// <param name="database">The Redis database used for storage and search.</param>
    /// <param name="options">The cache configuration, including schema, field names, and matching threshold.</param>
    /// <exception cref="ArgumentNullException"><paramref name="database" /> or <paramref name="options" /> is <see langword="null" />.</exception>
    public SemanticCache(IDatabase database, SemanticCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _database = database;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Options = options;
        _index = new SearchIndex(database, CreateSchema(options));
    }

    /// <summary>Gets the configuration this cache was created with.</summary>
    public SemanticCacheOptions Options { get; }

    /// <summary>Gets the cache name (from <see cref="Options" />).</summary>
    public string Name => Options.Name;

    /// <summary>Gets the optional key namespace (from <see cref="Options" />), or <see langword="null" /> when unset.</summary>
    public string? KeyNamespace => Options.KeyNamespace;

    /// <summary>Gets the default entry expiry (from <see cref="Options" />), or <see langword="null" /> for no expiry.</summary>
    public TimeSpan? TimeToLive => Options.TimeToLive;

    /// <summary>Gets the maximum vector distance for an entry to count as a match (from <see cref="Options" />).</summary>
    public double DistanceThreshold => Options.DistanceThreshold;

    /// <summary>
    /// Gets the number of cache lookups that returned a hit. Only tracked when
    /// <see cref="SemanticCacheOptions.TrackStatistics" /> is enabled; otherwise zero.
    /// </summary>
    public long HitCount => Interlocked.Read(ref _hitCount);

    /// <summary>
    /// Gets the number of cache lookups that returned a miss. Only tracked when
    /// <see cref="SemanticCacheOptions.TrackStatistics" /> is enabled; otherwise zero.
    /// </summary>
    public long MissCount => Interlocked.Read(ref _missCount);

    /// <summary>
    /// Gets the fraction of cache lookups that returned a hit (<c>hits / (hits + misses)</c>),
    /// or <c>0</c> when no lookups have been tracked.
    /// </summary>
    public double HitRate
    {
        get
        {
            var hits = Interlocked.Read(ref _hitCount);
            var misses = Interlocked.Read(ref _missCount);
            var total = hits + misses;
            return total == 0 ? 0d : (double)hits / total;
        }
    }

    /// <summary>Resets the tracked hit and miss counters to zero.</summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _hitCount, 0);
        Interlocked.Exchange(ref _missCount, 0);
    }

    /// <summary>Creates the underlying search index for the cache.</summary>
    /// <param name="options">Index creation options. When <see langword="null" />, defaults are used.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns><see langword="true" /> if the index was created; <see langword="false" /> if it already existed and creation was skipped.</returns>
    /// <exception cref="InvalidOperationException">An existing index schema is incompatible with the configured cache options.</exception>
    public async Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new CreateIndexOptions();
        if (!options.Overwrite && options.SkipIfExists && await _index.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await ValidateExistingSchemaAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        return await _index.CreateAsync(options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Determines whether the cache's underlying search index exists.</summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns><see langword="true" /> if the index exists; otherwise <see langword="false" />.</returns>
    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        _index.ExistsAsync(cancellationToken);

    /// <summary>Drops the cache's underlying search index.</summary>
    /// <param name="deleteDocuments">When <see langword="true" />, also deletes the cached hash entries; otherwise only the index is removed.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default) =>
        _index.DropAsync(deleteDocuments, cancellationToken);

    /// <summary>Looks up the single nearest cached entry within the configured distance threshold using a precomputed embedding.</summary>
    /// <param name="prompt">The prompt being looked up; validated but matching is performed on <paramref name="embedding" />.</param>
    /// <param name="embedding">The precomputed query embedding.</param>
    /// <param name="filter">An optional filter restricting candidates; requires configured filterable fields.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The nearest matching entry, or <see langword="null" /> on a cache miss.</returns>
    public async Task<SemanticCacheHit?> CheckAsync(
        string prompt,
        float[] embedding,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default)
    {
        NormalizePrompt(prompt);
        var hits = await SearchHitsAsync(embedding, 1, filter, cancellationToken).ConfigureAwait(false);
        return hits.Count > 0 ? hits[0] : null;
    }

    /// <summary>Looks up the single nearest cached entry within the configured distance threshold, vectorizing <paramref name="prompt" /> with <paramref name="vectorizer" />.</summary>
    /// <param name="prompt">The prompt to embed and look up.</param>
    /// <param name="vectorizer">The vectorizer used to embed <paramref name="prompt" />.</param>
    /// <param name="filter">An optional filter restricting candidates; requires configured filterable fields.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The nearest matching entry, or <see langword="null" /> on a cache miss.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="vectorizer" /> is <see langword="null" />.</exception>
    public async Task<SemanticCacheHit?> CheckAsync(
        string prompt,
        ITextVectorizer vectorizer,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var embedding = await vectorizer.VectorizeAsync(NormalizePrompt(prompt), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await CheckAsync(prompt, embedding, filter, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns up to <paramref name="topK" /> cached entries within the configured distance threshold,
    /// ordered nearest-first. An empty list indicates a cache miss.
    /// </summary>
    public async Task<IReadOnlyList<SemanticCacheHit>> CheckTopKAsync(
        string prompt,
        float[] embedding,
        int topK,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default)
    {
        NormalizePrompt(prompt);
        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "Semantic cache topK must be greater than zero.");
        }

        return await SearchHitsAsync(embedding, topK, filter, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns up to <paramref name="topK" /> cached entries within the configured distance threshold, ordered
    /// nearest-first, vectorizing <paramref name="prompt" /> with <paramref name="vectorizer" />. An empty list indicates a cache miss.
    /// </summary>
    /// <param name="prompt">The prompt to embed and look up.</param>
    /// <param name="vectorizer">The vectorizer used to embed <paramref name="prompt" />.</param>
    /// <param name="topK">The maximum number of matches to return; must be greater than zero.</param>
    /// <param name="filter">An optional filter restricting candidates; requires configured filterable fields.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="vectorizer" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topK" /> is less than or equal to zero.</exception>
    public async Task<IReadOnlyList<SemanticCacheHit>> CheckTopKAsync(
        string prompt,
        ITextVectorizer vectorizer,
        int topK,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var embedding = await vectorizer.VectorizeAsync(NormalizePrompt(prompt), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await CheckTopKAsync(prompt, embedding, topK, filter, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a batch of cache lookups. The result list is aligned to the input order; a <see langword="null" />
    /// element indicates a miss for the request at that position.
    /// </summary>
    public async Task<IReadOnlyList<SemanticCacheHit?>> CheckManyAsync(
        IEnumerable<SemanticCacheCheckRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        // Validate every request before issuing any lookup so a malformed request fails the whole
        // call rather than after some lookups have already run.
        var materialized = requests.ToList();
        foreach (var request in materialized)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Embedding is null)
            {
                throw new ArgumentException("Each check request must provide an embedding when no vectorizer is supplied.", nameof(requests));
            }
        }

        return await RedisBatch.RunAsync(
            materialized,
            (request, _, token) => CheckAsync(request.Prompt, request.Embedding!, request.Filter, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a batch of cache lookups, vectorizing every request prompt in a single batch via <paramref name="vectorizer" />.
    /// The result list is aligned to the input order; a <see langword="null" /> element indicates a miss for the request
    /// at that position. Any embedding supplied on a request is ignored.
    /// </summary>
    /// <param name="requests">The lookup requests to run.</param>
    /// <param name="vectorizer">The vectorizer used to embed each request prompt.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="requests" /> or <paramref name="vectorizer" /> is <see langword="null" />.</exception>
    public async Task<IReadOnlyList<SemanticCacheHit?>> CheckManyAsync(
        IEnumerable<SemanticCacheCheckRequest> requests,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(vectorizer);

        var materialized = requests.ToList();
        var prompts = materialized.Select(request =>
        {
            ArgumentNullException.ThrowIfNull(request);
            return NormalizePrompt(request.Prompt);
        }).ToList();

        var embeddings = await vectorizer.VectorizeManyAsync(prompts, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return await RedisBatch.RunAsync(
            materialized,
            (request, index, token) => CheckAsync(request.Prompt, embeddings[index], request.Filter, token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SemanticCacheHit>> SearchHitsAsync(
        float[] embedding,
        int limit,
        FilterExpression? filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        ValidateFilterUsage(filter);

        cancellationToken.ThrowIfCancellationRequested();

        var results = await _index.SearchAsync(
            VectorRangeQuery.FromFloat32(
                Options.EmbeddingFieldName,
                embedding,
                DistanceThreshold,
                filter,
                returnFields: [Options.PromptFieldName, Options.ResponseFieldName, Options.MetadataFieldName],
                scoreAlias: "distance",
                limit: limit),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var hits = new List<SemanticCacheHit>(results.Documents.Count);
        foreach (var document in results.Documents)
        {
            if (TryMapSearchHit(document, out var hit))
            {
                hits.Add(hit);
            }
        }

        RecordLookup(hits.Count > 0);
        return hits;
    }

    private void RecordLookup(bool hit)
    {
        if (!Options.TrackStatistics)
        {
            return;
        }

        if (hit)
        {
            Interlocked.Increment(ref _hitCount);
        }
        else
        {
            Interlocked.Increment(ref _missCount);
        }
    }

    /// <summary>Stores a prompt/response pair using a precomputed embedding, returning the Redis key it was stored under.</summary>
    /// <param name="prompt">The prompt to cache; forms part of the entry's key identity.</param>
    /// <param name="response">The response to cache for the prompt.</param>
    /// <param name="embedding">The precomputed prompt embedding; must match the configured embedding dimensions.</param>
    /// <param name="metadata">Optional metadata serialized and stored alongside the entry.</param>
    /// <param name="filterValues">Optional values for configured filterable fields; folded into the entry's key identity.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The Redis key the entry was stored under.</returns>
    /// <exception cref="ArgumentException"><paramref name="embedding" /> length does not match the configured dimensions, or a filter value is invalid.</exception>
    public async Task<string> StoreAsync(
        string prompt,
        string response,
        float[] embedding,
        object? metadata = null,
        IReadOnlyDictionary<string, object?>? filterValues = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPrompt = NormalizePrompt(prompt);
        var normalizedResponse = NormalizeResponse(response);
        ValidateEmbedding(embedding);
        var normalizedFilterValues = NormalizeFilterValues(filterValues);

        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(normalizedPrompt, normalizedFilterValues);
        var entries = new List<HashEntry>
        {
            new(Options.PromptFieldName, normalizedPrompt),
            new(Options.ResponseFieldName, normalizedResponse),
            new(Options.EmbeddingFieldName, EmbeddingsCache.EncodeFloat32(embedding))
        };

        // Metadata is the only optional field on the key (filter values are folded into the key
        // identity), so clear it when this store carries none to avoid an HSET-merge leaving a
        // previous entry's metadata paired with the new response.
        var metadataPayload = SerializeMetadata(metadata);
        RedisValue[] fieldsToClear = metadataPayload is null
            ? [Options.MetadataFieldName]
            : [];
        if (metadataPayload is not null)
        {
            entries.Add(new HashEntry(Options.MetadataFieldName, metadataPayload));
        }

        foreach (var filterValue in normalizedFilterValues)
        {
            entries.Add(new HashEntry(filterValue.Key, filterValue.Value));
        }

        await WriteEntriesAsync(key, entries, fieldsToClear, TimeToLive, cancellationToken).ConfigureAwait(false);

        return key!;
    }

    /// <summary>Stores a prompt/response pair, vectorizing <paramref name="prompt" /> with <paramref name="vectorizer" />, returning the Redis key it was stored under.</summary>
    /// <param name="prompt">The prompt to embed and cache; forms part of the entry's key identity.</param>
    /// <param name="response">The response to cache for the prompt.</param>
    /// <param name="vectorizer">The vectorizer used to embed <paramref name="prompt" />.</param>
    /// <param name="metadata">Optional metadata serialized and stored alongside the entry.</param>
    /// <param name="filterValues">Optional values for configured filterable fields; folded into the entry's key identity.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The Redis key the entry was stored under.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="vectorizer" /> is <see langword="null" />.</exception>
    public async Task<string> StoreAsync(
        string prompt,
        string response,
        ITextVectorizer vectorizer,
        object? metadata = null,
        IReadOnlyDictionary<string, object?>? filterValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var embedding = await vectorizer.VectorizeAsync(NormalizePrompt(prompt), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await StoreAsync(prompt, response, embedding, metadata, filterValues, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stores multiple prompt/response pairs. Each request must carry its own embedding; the returned key
    /// list is aligned to the input order.
    /// </summary>
    /// <remarks>
    /// Requests are validated up front, then the writes are pipelined (dispatched concurrently) rather
    /// than awaited one at a time. The batch is not transactional: if a write fails, entries dispatched
    /// alongside it may already have been stored and are not rolled back.
    /// </remarks>
    public async Task<IReadOnlyList<string>> StoreManyAsync(
        IEnumerable<SemanticCacheStoreRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        // Validate every request before issuing any write so a malformed request fails the whole
        // call rather than after some entries have already been stored.
        var materialized = requests.ToList();
        foreach (var request in materialized)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Embedding is null)
            {
                throw new ArgumentException("Each store request must provide an embedding when no vectorizer is supplied.", nameof(requests));
            }
        }

        return await RedisBatch.RunAsync(
            materialized,
            (request, _, token) => StoreAsync(request.Prompt, request.Response, request.Embedding!, request.Metadata, request.FilterValues, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stores multiple prompt/response pairs, vectorizing all prompts in a single batch via
    /// <paramref name="vectorizer" />. Any embedding supplied on a request is ignored.
    /// </summary>
    public async Task<IReadOnlyList<string>> StoreManyAsync(
        IEnumerable<SemanticCacheStoreRequest> requests,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(vectorizer);

        var materialized = requests.ToList();
        var prompts = materialized.Select(request =>
        {
            ArgumentNullException.ThrowIfNull(request);
            return NormalizePrompt(request.Prompt);
        }).ToList();

        var embeddings = await vectorizer.VectorizeManyAsync(prompts, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return await RedisBatch.RunAsync(
            materialized,
            (request, index, token) => StoreAsync(request.Prompt, request.Response, embeddings[index], request.Metadata, request.FilterValues, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the response and/or metadata of an existing cached entry identified by <paramref name="key" />
    /// (as returned by <c>Store</c>). Refreshes the TTL when one is configured. Returns <see langword="false" />
    /// when the key does not exist; the embedding and filter values are left unchanged.
    /// </summary>
    public async Task<bool> UpdateAsync(
        string key,
        string? response = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (response is null && metadata is null)
        {
            throw new ArgumentException("Specify a response and/or metadata to update.", nameof(response));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!await _database.KeyExistsAsync(key).WaitAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var entries = new List<HashEntry>(2);
        if (response is not null)
        {
            entries.Add(new HashEntry(Options.ResponseFieldName, NormalizeResponse(response)));
        }

        if (metadata is not null)
        {
            entries.Add(new HashEntry(Options.MetadataFieldName, SerializeMetadata(metadata)));
        }

        // An update patches only the supplied fields, so nothing is cleared here; pass no
        // fields-to-clear and let the shared writer bundle the HSET and TTL refresh atomically.
        await WriteEntriesAsync((RedisKey)key, entries, fieldsToClear: [], TimeToLive, cancellationToken).ConfigureAwait(false);

        return true;
    }

    internal RedisKey CreateKey(string prompt, IReadOnlyDictionary<string, RedisValue>? filterValues = null)
    {
        var hashInput = CreateCacheIdentityPayload(prompt, filterValues);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant();
        return $"{CreateKeyPrefix(Options)}{hash}";
    }

    private void ValidateEmbedding(float[] embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        // RediSearch silently rejects (and never indexes) a hash whose vector length does not match
        // the field's declared dimensions, so validate on write rather than storing an entry that
        // can never match a query. Query-side vectors are validated by the search command builder.
        if (embedding.Length != Options.EmbeddingFieldAttributes.Dimensions)
        {
            throw new ArgumentException(
                $"Semantic cache embedding must contain exactly {Options.EmbeddingFieldAttributes.Dimensions} values.",
                nameof(embedding));
        }
    }

    private async Task WriteEntriesAsync(
        RedisKey key,
        IReadOnlyList<HashEntry> entries,
        IReadOnlyList<RedisValue> fieldsToClear,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken)
    {
        // A plain HSET suffices only when there is nothing else to do; otherwise group the write,
        // the stale-field cleanup, and the TTL into a single MULTI/EXEC so an entry can never be
        // left with a stale optional field or without its configured TTL (for example when the
        // connection drops or the token is cancelled between the HSET and the EXPIRE).
        if (fieldsToClear.Count == 0 && !timeToLive.HasValue)
        {
            await _database.HashSetAsync(key, entries.ToArray()).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var transaction = _database.CreateTransaction();
        _ = transaction.HashSetAsync(key, entries.ToArray());
        if (fieldsToClear.Count > 0)
        {
            _ = transaction.HashDeleteAsync(key, fieldsToClear.ToArray());
        }

        if (timeToLive.HasValue)
        {
            _ = transaction.KeyExpireAsync(key, timeToLive);
        }

        await transaction.ExecuteAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SearchSchema CreateSchema(SemanticCacheOptions options)
    {
        var fields = new List<FieldDefinition>
        {
            new TextFieldDefinition(options.PromptFieldName),
            new TextFieldDefinition(options.ResponseFieldName),
            new TextFieldDefinition(options.MetadataFieldName)
        };

        fields.AddRange(options.FilterableFields);
        fields.Add(new VectorFieldDefinition(options.EmbeddingFieldName, options.EmbeddingFieldAttributes));

        return new SearchSchema(
            new IndexDefinition(CreateIndexName(options), CreateKeyPrefix(options), StorageType.Hash),
            fields);
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

    private void ValidateFilterUsage(FilterExpression? filter)
    {
        if (filter is not null && Options.FilterableFields.Count == 0)
        {
            throw new ArgumentException("Semantic cache filters require configured filterable fields.", nameof(filter));
        }
    }

    private async Task ValidateExistingSchemaAsync(CancellationToken cancellationToken)
    {
        var existingIndex = await SearchIndex.FromExistingAsync(_database, _index.Schema.Index.Name, cancellationToken).ConfigureAwait(false);
        if (!SchemasAreCompatible(_index.Schema, existingIndex.Schema))
        {
            throw new InvalidOperationException("Existing semantic cache index schema is incompatible with the configured semantic cache options.");
        }
    }

    private static bool SchemasAreCompatible(SearchSchema expected, SearchSchema actual)
    {
        return IndexDefinitionsAreCompatible(expected.Index, actual.Index) &&
            expected.Fields.SequenceEqual(actual.Fields);
    }

    private static bool IndexDefinitionsAreCompatible(IndexDefinition expected, IndexDefinition actual)
    {
        return string.Equals(expected.Name, actual.Name, StringComparison.Ordinal) &&
            expected.StorageType == actual.StorageType &&
            expected.KeySeparator == actual.KeySeparator &&
            expected.MaxTextFields == actual.MaxTextFields &&
            expected.TemporarySeconds == actual.TemporarySeconds &&
            expected.NoOffsets == actual.NoOffsets &&
            expected.NoHighlight == actual.NoHighlight &&
            expected.NoFields == actual.NoFields &&
            expected.NoFrequencies == actual.NoFrequencies &&
            expected.SkipInitialScan == actual.SkipInitialScan &&
            expected.Prefixes.SequenceEqual(actual.Prefixes) &&
            StopwordsAreCompatible(expected.Stopwords, actual.Stopwords);
    }

    private static bool StopwordsAreCompatible(IReadOnlyList<string>? expected, IReadOnlyList<string>? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null && actual is null;
        }

        return expected.SequenceEqual(actual);
    }

    private IReadOnlyDictionary<string, RedisValue> NormalizeFilterValues(IReadOnlyDictionary<string, object?>? filterValues)
    {
        if (filterValues is null || filterValues.Count == 0)
        {
            return new Dictionary<string, RedisValue>(StringComparer.Ordinal);
        }

        if (Options.FilterableFields.Count == 0)
        {
            throw new ArgumentException("Semantic cache filter values require configured filterable fields.", nameof(filterValues));
        }

        var normalized = new SortedDictionary<string, RedisValue>(StringComparer.Ordinal);
        foreach (var entry in filterValues)
        {
            var fieldName = FilterExpression.NormalizeFieldName(entry.Key);
            if (normalized.ContainsKey(fieldName))
            {
                throw new ArgumentException($"Semantic cache filter field '{fieldName}' was provided more than once.", nameof(filterValues));
            }

            var fieldDefinition = Options.FilterableFields.FirstOrDefault(field => string.Equals(field.Name, fieldName, StringComparison.Ordinal));
            if (fieldDefinition is null)
            {
                throw new ArgumentException($"Semantic cache filter field '{fieldName}' is not defined in the cache schema.", nameof(filterValues));
            }

            normalized[fieldName] = NormalizeFilterValue(fieldDefinition, entry.Value, nameof(filterValues));
        }

        return normalized;
    }

    private static RedisValue NormalizeFilterValue(FieldDefinition fieldDefinition, object? value, string paramName)
    {
        return fieldDefinition switch
        {
            TagFieldDefinition tagField => NormalizeTagFilterValue(tagField, value, paramName),
            TextFieldDefinition => NormalizeTextFilterValue(value, paramName),
            NumericFieldDefinition => NormalizeNumericFilterValue(value, paramName),
            _ => throw new InvalidOperationException($"Unsupported semantic cache filter field type '{fieldDefinition.GetType().Name}'.")
        };
    }

    private static RedisValue NormalizeTagFilterValue(TagFieldDefinition fieldDefinition, object? value, string paramName)
    {
        if (value is string singleValue)
        {
            return NormalizeSingleTagValue(singleValue, fieldDefinition.Separator, fieldDefinition.Name, paramName);
        }

        if (value is IEnumerable<string> values)
        {
            var normalized = values
                .Select(tag => NormalizeSingleTagValue(tag, fieldDefinition.Separator, fieldDefinition.Name, paramName).ToString())
                .ToArray();

            if (normalized.Length == 0)
            {
                throw new ArgumentException($"Semantic cache tag filter field '{fieldDefinition.Name}' must contain at least one value.", paramName);
            }

            return string.Join(fieldDefinition.Separator, normalized);
        }

        throw new ArgumentException($"Semantic cache tag filter field '{fieldDefinition.Name}' requires a string or string collection value.", paramName);
    }

    private static RedisValue NormalizeSingleTagValue(string value, char separator, string fieldName, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Contains(separator, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Semantic cache tag filter field '{fieldName}' cannot contain the separator character '{separator}'.", paramName);
        }

        return normalized;
    }

    private static RedisValue NormalizeTextFilterValue(object? value, string paramName)
    {
        if (value is not string stringValue)
        {
            throw new ArgumentException("Semantic cache text filter fields require string values.", paramName);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(stringValue);
        return stringValue.Trim();
    }

    private static RedisValue NormalizeNumericFilterValue(object? value, string paramName)
    {
        var numericValue = value switch
        {
            byte number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            sbyte number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            short number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            ushort number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            int number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            uint number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            long number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            ulong number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            float number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            double number => number,
            decimal number => Convert.ToDouble(number, CultureInfo.InvariantCulture),
            _ => throw new ArgumentException("Semantic cache numeric filter fields require numeric values.", paramName)
        };

        if (double.IsNaN(numericValue) || double.IsInfinity(numericValue))
        {
            throw new ArgumentException("Semantic cache numeric filter fields require finite numeric values.", paramName);
        }

        return numericValue.ToString("G", CultureInfo.InvariantCulture);
    }

    private string CreateCacheIdentityPayload(string prompt, IReadOnlyDictionary<string, RedisValue>? filterValues)
    {
        if (filterValues is null || filterValues.Count == 0)
        {
            return prompt;
        }

        var payload = filterValues.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value.ToString(),
            StringComparer.Ordinal);
        return $"{prompt}\n{JsonSerializer.Serialize(payload, _serializerOptions)}";
    }

    private string? SerializeMetadata(object? metadata) =>
        metadata is null ? null : JsonSerializer.Serialize(metadata, _serializerOptions);

    private bool TryMapSearchHit(SearchDocument document, out SemanticCacheHit hit)
    {
        if (!document.TryGetValue(Options.PromptFieldName, out var promptValue) ||
            !document.TryGetValue(Options.ResponseFieldName, out var responseValue) ||
            !document.TryGetValue("distance", out var distanceValue) ||
            !double.TryParse(distanceValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
        {
            hit = default!;
            return false;
        }

        document.TryGetValue(Options.MetadataFieldName, out var metadataValue);
        hit = new SemanticCacheHit(
            promptValue.ToString()!,
            responseValue.ToString()!,
            distance,
            metadataValue.IsNull ? null : metadataValue.ToString());
        return true;
    }

    private sealed record SemanticCacheSearchDocument(string Prompt, string Response, double Distance, string? Metadata);
}

/// <summary>
/// A cached entry returned from a semantic cache lookup: the stored prompt and response together with the
/// vector <see cref="Distance" /> between the query embedding and the entry, and any stored metadata.
/// </summary>
public sealed record SemanticCacheHit(string Prompt, string Response, double Distance, string? Metadata = null);

/// <summary>A single entry for a batch <c>StoreMany</c> call.</summary>
/// <remarks>
/// <see cref="Embedding" /> is required for the precomputed-vector <c>StoreMany</c> overload and ignored by
/// the overload that accepts an <see cref="ITextVectorizer" />.
/// </remarks>
public sealed record SemanticCacheStoreRequest(
    string Prompt,
    string Response,
    float[]? Embedding = null,
    object? Metadata = null,
    IReadOnlyDictionary<string, object?>? FilterValues = null);

/// <summary>A single lookup for a batch <c>CheckMany</c> call.</summary>
/// <remarks>
/// <see cref="Embedding" /> is required for the precomputed-vector <c>CheckMany</c> overload and ignored by
/// the overload that accepts an <see cref="ITextVectorizer" />.
/// </remarks>
public sealed record SemanticCacheCheckRequest(
    string Prompt,
    float[]? Embedding = null,
    FilterExpression? Filter = null);

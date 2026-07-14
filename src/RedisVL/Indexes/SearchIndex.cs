using RedisVL.Internal;
using RedisVL.Schema;
using RedisVL.Queries;
using StackExchange.Redis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace RedisVL.Indexes;

/// <summary>
/// Represents a Redis search index and provides the operations to manage it and to load, fetch,
/// update, delete, search, and aggregate the documents it indexes.
/// </summary>
public sealed class SearchIndex : ISearchIndex
{
    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _serializerOptions;
    private const string ListIndexesCommand = "FT._LIST";
    private const string InfoCommand = "FT.INFO";
    private const string HybridCommand = "FT.HYBRID";

    /// <summary>Initializes a new instance of the <see cref="SearchIndex"/> class.</summary>
    /// <param name="database">The Redis database used to execute commands.</param>
    /// <param name="schema">The schema describing the index and its fields.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> or <paramref name="schema"/> is <see langword="null"/>.</exception>
    public SearchIndex(IDatabase database, SearchSchema schema)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(schema);

        _database = database;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Schema = schema;
    }

    /// <summary>Gets the schema describing this index and its fields.</summary>
    public SearchSchema Schema { get; }

    /// <summary>Creates a <see cref="SearchIndex"/> by reading the schema of an index that already exists in Redis.</summary>
    /// <param name="database">The Redis database used to execute commands.</param>
    /// <param name="indexName">The name of the existing index to reflect over.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to a <see cref="SearchIndex"/> whose schema was built from the index's <c>FT.INFO</c> response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="indexName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static async Task<SearchIndex> FromExistingAsync(
        IDatabase database,
        string indexName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

        var info = await LoadInfoAsync(database, indexName.Trim(), cancellationToken).ConfigureAwait(false);
        return new SearchIndex(database, SearchIndexSchemaBuilder.FromInfo(info));
    }

    /// <summary>Lists all search indexes defined on the server using the <c>FT._LIST</c> command.</summary>
    /// <param name="database">The Redis database used to execute commands.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the list of indexes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> is <see langword="null"/>.</exception>
    public static async Task<IReadOnlyList<SearchIndexListItem>> ListAsync(
        IDatabase database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await database.ExecuteAsync(ListIndexesCommand, []).WaitAsync(cancellationToken).ConfigureAwait(false);
        return SearchIndexListItem.FromRedisResult(result);
    }

    /// <summary>Creates the index in Redis via <c>FT.CREATE</c>, honoring the supplied existence-handling options.</summary>
    /// <param name="options">Options controlling behavior when the index already exists; defaults are used when <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> when the index was created, or <see langword="false"/> when creation was skipped because it already existed and <see cref="CreateIndexOptions.SkipIfExists"/> was set.</returns>
    public async Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new CreateIndexOptions();

        if (options.Overwrite)
        {
            if (await ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                await DropAsync(options.DropExistingDocuments, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (options.SkipIfExists && await ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await ExecuteAsync("FT.CREATE", SearchIndexCommandBuilder.BuildCreateArguments(Schema), cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Determines whether the index currently exists in Redis.</summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> if the index exists; otherwise <see langword="false"/>.</returns>
    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await InfoAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RedisServerException exception) when (IsUnknownIndexException(exception))
        {
            return false;
        }
    }

    /// <summary>Retrieves index metadata from Redis via <c>FT.INFO</c>.</summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the index information.</returns>
    public async Task<SearchIndexInfo> InfoAsync(CancellationToken cancellationToken = default)
    {
        return await LoadInfoAsync(_database, Schema.Index.Name, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Drops the index via <c>FT.DROPINDEX</c>, optionally deleting the documents it indexed.</summary>
    /// <param name="deleteDocuments">When <see langword="true"/>, the underlying documents are deleted along with the index.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when the index has been dropped.</returns>
    public async Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync("FT.DROPINDEX", SearchIndexCommandBuilder.BuildDropArguments(Schema, deleteDocuments), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Deletes all documents matching the index's key prefixes without dropping the index itself.</summary>
    /// <param name="batchSize">The number of keys to scan and delete per batch.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the number of documents deleted.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than or equal to zero.</exception>
    public async Task<long> ClearAsync(int batchSize = 1000, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        cancellationToken.ThrowIfCancellationRequested();

        var deletedCount = 0L;
        foreach (var prefix in Schema.Index.Prefixes)
        {
            deletedCount += await DeleteDocumentsByPrefixAsync(prefix, batchSize, cancellationToken).ConfigureAwait(false);
        }

        return deletedCount;
    }

    /// <summary>Serializes and stores a single document as JSON via <c>JSON.SET</c>.</summary>
    /// <typeparam name="TDocument">The document type to serialize.</typeparam>
    /// <param name="document">The document to store.</param>
    /// <param name="key">An explicit Redis key to use; when <see langword="null"/> the key is derived from <paramref name="id"/> or the document.</param>
    /// <param name="id">An explicit document id used to derive the key when <paramref name="key"/> is not supplied.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the Redis key the document was stored under.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for JSON storage.</exception>
    public async Task<string> LoadJsonAsync<TDocument>(
        TDocument document,
        string? key = null,
        string? id = null,
        CancellationToken cancellationToken = default)
    {
        EnsureJsonStorage();

        var resolvedKey = DocumentKeyResolver.ResolveKey(Schema, document, key, id);
        await SetJsonDocumentAsync(resolvedKey, document, cancellationToken).ConfigureAwait(false);
        return resolvedKey;
    }

    /// <summary>Serializes and stores multiple documents as JSON, pipelining the <c>JSON.SET</c> commands.</summary>
    /// <typeparam name="TDocument">The document type to serialize.</typeparam>
    /// <param name="documents">The documents to store.</param>
    /// <param name="keySelector">An optional selector that returns an explicit Redis key for each document.</param>
    /// <param name="idSelector">An optional selector that returns a document id used to derive the key when <paramref name="keySelector"/> is not supplied.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the Redis keys the documents were stored under, in input order.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for JSON storage.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="documents"/> is <see langword="null"/>.</exception>
    public async Task<IReadOnlyList<string>> LoadJsonAsync<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null,
        CancellationToken cancellationToken = default)
    {
        EnsureJsonStorage();
        ArgumentNullException.ThrowIfNull(documents);

        // Resolve every key up front (pure CPU, no I/O) so a bad key fails the whole call before any
        // document is written, then pipeline the JSON.SET commands instead of awaiting one per item.
        var materialized = documents as IReadOnlyList<TDocument> ?? documents.ToList();
        var keys = new string[materialized.Count];
        for (var index = 0; index < materialized.Count; index++)
        {
            keys[index] = DocumentKeyResolver.ResolveKeyForSelectors(Schema, materialized[index], keySelector, idSelector);
        }

        await RedisBatch.RunAsync(
            materialized,
            (document, index, token) => SetJsonDocumentAsync(keys[index], document, token),
            cancellationToken).ConfigureAwait(false);

        return keys;
    }

    /// <summary>Fetches and deserializes a JSON document by its Redis key via <c>JSON.GET</c>.</summary>
    /// <typeparam name="TDocument">The type to deserialize the document into.</typeparam>
    /// <param name="key">The Redis key of the document.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the deserialized document, or the type default when the key does not exist.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for JSON storage.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public async Task<TDocument?> FetchJsonByKeyAsync<TDocument>(string key, CancellationToken cancellationToken = default)
    {
        EnsureJsonStorage();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await ExecuteAsync("JSON.GET", [key.Trim()], cancellationToken).ConfigureAwait(false);
        if (result.IsNull)
        {
            return default;
        }

        return JsonSerializer.Deserialize<TDocument>(result.ToString()!, _serializerOptions);
    }

    /// <summary>Fetches and deserializes a JSON document by its document id, resolving the id to a Redis key first.</summary>
    /// <typeparam name="TDocument">The type to deserialize the document into.</typeparam>
    /// <param name="id">The document id.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the deserialized document, or the type default when it does not exist.</returns>
    public Task<TDocument?> FetchJsonByIdAsync<TDocument>(string id, CancellationToken cancellationToken = default) =>
        FetchJsonByKeyAsync<TDocument>(DocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

    /// <summary>Deletes a JSON document by its Redis key.</summary>
    /// <param name="key">The Redis key of the document to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> when a key was deleted; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for JSON storage.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public async Task<bool> DeleteJsonByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureJsonStorage();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var deleted = await _database.KeyDeleteAsync(key.Trim()).WaitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    /// <summary>Deletes a JSON document by its document id, resolving the id to a Redis key first.</summary>
    /// <param name="id">The document id.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> when a key was deleted; otherwise <see langword="false"/>.</returns>
    public Task<bool> DeleteJsonByIdAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteJsonByKeyAsync(DocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

    /// <summary>Applies one or more partial JSON updates to an existing document, each written with its own <c>JSON.SET</c>.</summary>
    /// <param name="key">The Redis key of the document to update.</param>
    /// <param name="updates">The partial updates to apply; paths are normalized and must be unique.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> when the document existed and all updates were applied; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for JSON storage.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is invalid, or when <paramref name="updates"/> is empty or contains duplicate or malformed paths.</exception>
    public async Task<bool> UpdateJsonByKeyAsync(
        string key,
        IEnumerable<JsonPartialUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        EnsureJsonStorage();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalizedKey = key.Trim();
        var normalizedUpdates = NormalizeJsonPartialUpdates(updates);
        if (!await JsonDocumentExistsAsync(normalizedKey, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        foreach (var update in normalizedUpdates)
        {
            var payload = JsonSerializer.Serialize(update.Value, _serializerOptions);
            var result = await ExecuteAsync("JSON.SET", [normalizedKey, update.Path, payload], cancellationToken).ConfigureAwait(false);
            if (result.IsNull)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Applies partial JSON updates to a document identified by its document id.</summary>
    /// <param name="id">The document id.</param>
    /// <param name="updates">The partial updates to apply.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> when the document existed and all updates were applied; otherwise <see langword="false"/>.</returns>
    public Task<bool> UpdateJsonByIdAsync(
        string id,
        IEnumerable<JsonPartialUpdate> updates,
        CancellationToken cancellationToken = default) =>
        UpdateJsonByKeyAsync(DocumentKeyResolver.ResolveKeyFromId(Schema, id), updates, cancellationToken);

    /// <summary>Maps and stores a single document as a Redis hash via <c>HSET</c>.</summary>
    /// <typeparam name="TDocument">The document type to map to hash fields.</typeparam>
    /// <param name="document">The document to store.</param>
    /// <param name="key">An explicit Redis key to use; when <see langword="null"/> the key is derived from <paramref name="id"/> or the document.</param>
    /// <param name="id">An explicit document id used to derive the key when <paramref name="key"/> is not supplied.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the Redis key the document was stored under.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for hash storage.</exception>
    public async Task<string> LoadHashAsync<TDocument>(
        TDocument document,
        string? key = null,
        string? id = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHashStorage();

        var resolvedKey = DocumentKeyResolver.ResolveKey(Schema, document, key, id);
        await SetHashDocumentAsync(resolvedKey, document, cancellationToken).ConfigureAwait(false);
        return resolvedKey;
    }

    /// <summary>Maps and stores multiple documents as Redis hashes, pipelining the <c>HSET</c> commands.</summary>
    /// <typeparam name="TDocument">The document type to map to hash fields.</typeparam>
    /// <param name="documents">The documents to store.</param>
    /// <param name="keySelector">An optional selector that returns an explicit Redis key for each document.</param>
    /// <param name="idSelector">An optional selector that returns a document id used to derive the key when <paramref name="keySelector"/> is not supplied.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the Redis keys the documents were stored under, in input order.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for hash storage.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="documents"/> is <see langword="null"/>.</exception>
    public async Task<IReadOnlyList<string>> LoadHashAsync<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHashStorage();
        ArgumentNullException.ThrowIfNull(documents);

        // Resolve every key up front (pure CPU, no I/O) so a bad key fails the whole call before any
        // document is written, then pipeline the HSET commands instead of awaiting one per item.
        var materialized = documents as IReadOnlyList<TDocument> ?? documents.ToList();
        var keys = new string[materialized.Count];
        for (var index = 0; index < materialized.Count; index++)
        {
            keys[index] = DocumentKeyResolver.ResolveKeyForSelectors(Schema, materialized[index], keySelector, idSelector);
        }

        await RedisBatch.RunAsync(
            materialized,
            (document, index, token) => SetHashDocumentAsync(keys[index], document, token),
            cancellationToken).ConfigureAwait(false);

        return keys;
    }

    /// <summary>Fetches and maps a hash document by its Redis key via <c>HGETALL</c>.</summary>
    /// <typeparam name="TDocument">The type to map the hash fields into.</typeparam>
    /// <param name="key">The Redis key of the document.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the mapped document, or the type default when the key does not exist.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for hash storage.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public async Task<TDocument?> FetchHashByKeyAsync<TDocument>(string key, CancellationToken cancellationToken = default)
    {
        EnsureHashStorage();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var entries = await _database.HashGetAllAsync(key.Trim()).WaitAsync(cancellationToken).ConfigureAwait(false);
        return entries.Length == 0
            ? default
            : HashDocumentMapper.FromHashEntries<TDocument>(entries, _serializerOptions);
    }

    /// <summary>Fetches and maps a hash document by its document id, resolving the id to a Redis key first.</summary>
    /// <typeparam name="TDocument">The type to map the hash fields into.</typeparam>
    /// <param name="id">The document id.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the mapped document, or the type default when it does not exist.</returns>
    public Task<TDocument?> FetchHashByIdAsync<TDocument>(string id, CancellationToken cancellationToken = default) =>
        FetchHashByKeyAsync<TDocument>(DocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

    /// <summary>Deletes a hash document by its Redis key.</summary>
    /// <param name="key">The Redis key of the document to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> when a key was deleted; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for hash storage.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public async Task<bool> DeleteHashByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureHashStorage();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var deleted = await _database.KeyDeleteAsync(key.Trim()).WaitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    /// <summary>Deletes a hash document by its document id, resolving the id to a Redis key first.</summary>
    /// <param name="id">The document id.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> when a key was deleted; otherwise <see langword="false"/>.</returns>
    public Task<bool> DeleteHashByIdAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteHashByKeyAsync(DocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

    /// <summary>Applies one or more partial field updates to an existing hash document via a single <c>HSET</c>.</summary>
    /// <param name="key">The Redis key of the document to update.</param>
    /// <param name="updates">The field updates to apply; fields are normalized, must be unique, and must have non-null values.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> when the document existed and the updates were applied; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema is not configured for hash storage.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is invalid, or when <paramref name="updates"/> is empty or contains duplicate fields or null values.</exception>
    public async Task<bool> UpdateHashByKeyAsync(
        string key,
        IEnumerable<HashPartialUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        EnsureHashStorage();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalizedKey = key.Trim();
        var normalizedUpdates = NormalizeHashPartialUpdates(updates);
        if (!await HashDocumentExistsAsync(normalizedKey, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var entries = normalizedUpdates
            .Select(update => HashDocumentMapper.ToHashEntry(update.Field, update.Value, _serializerOptions))
            .ToArray();

        await _database.HashSetAsync(normalizedKey, entries).WaitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Applies partial field updates to a hash document identified by its document id.</summary>
    /// <param name="id">The document id.</param>
    /// <param name="updates">The field updates to apply.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> when the document existed and the updates were applied; otherwise <see langword="false"/>.</returns>
    public Task<bool> UpdateHashByIdAsync(
        string id,
        IEnumerable<HashPartialUpdate> updates,
        CancellationToken cancellationToken = default) =>
        UpdateHashByKeyAsync(DocumentKeyResolver.ResolveKeyFromId(Schema, id), updates, cancellationToken);

    /// <summary>Runs a vector similarity search via <c>FT.SEARCH</c>.</summary>
    /// <param name="query">The vector query to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the raw search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults> SearchAsync(VectorQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildVectorSearchArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    /// <summary>Runs a multi-vector search by issuing one <c>FT.SEARCH</c> per sub-vector and combining the results by weighted score.</summary>
    /// <param name="query">The multi-vector query to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the combined, ranked, and paged search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults> SearchAsync(MultiVectorQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Each sub-vector is an independent FT.SEARCH; pipeline them instead of awaiting one before
        // issuing the next. Results stay aligned to the sub-vector order for CombineMultiVectorResults.
        var perVectorArguments = SearchQueryCommandBuilder.BuildMultiVectorSearchArguments(Schema, query);
        var perVectorResults = await RedisBatch.RunAsync(
            perVectorArguments,
            async (arguments, _, token) =>
                SearchResultsParser.Parse(await ExecuteAsync("FT.SEARCH", arguments, token).ConfigureAwait(false)),
            cancellationToken).ConfigureAwait(false);

        return CombineMultiVectorResults(query, perVectorResults);
    }

    /// <summary>Runs a vector similarity search and deserializes each result document to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The vector query to execute.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the typed search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        VectorQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    /// <summary>Runs a multi-vector search and deserializes each result document to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The multi-vector query to execute.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the typed search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        MultiVectorQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    /// <summary>Runs a client-side hybrid (text plus vector) search by translating the query to a single <c>FT.SEARCH</c>.</summary>
    /// <param name="query">The hybrid query to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the raw search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults> SearchAsync(HybridQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildHybridSearchArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    /// <summary>Runs a client-side hybrid search and deserializes each result document to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The hybrid query to execute.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the typed search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        HybridQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    /// <summary>Runs a server-side hybrid search using the native <c>FT.HYBRID</c> command.</summary>
    /// <param name="query">The native hybrid query to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the raw search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults> SearchAsync(HybridSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            HybridCommand,
            SearchQueryCommandBuilder.BuildNativeHybridArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return HybridSearchResultsParser.Parse(result);
    }

    /// <summary>Runs a native <c>FT.HYBRID</c> search and deserializes each result document to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The native hybrid query to execute.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the typed search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        HybridSearchQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    /// <summary>Runs a vector range search (matching documents within a distance threshold) via <c>FT.SEARCH</c>.</summary>
    /// <param name="query">The vector range query to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the raw search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults> SearchAsync(VectorRangeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildVectorRangeArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    /// <summary>Runs a vector range search and deserializes each result document to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The vector range query to execute.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the typed search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        VectorRangeQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    /// <summary>Runs a filter-only search (no vector or full-text scoring) via <c>FT.SEARCH</c>.</summary>
    /// <param name="query">The filter query to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the raw search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults> SearchAsync(FilterQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildFilterSearchArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    /// <summary>Runs a full-text search via <c>FT.SEARCH</c>.</summary>
    /// <param name="query">The text query to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the raw search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults> SearchAsync(TextQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildTextSearchArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    /// <summary>Runs an aggregation pipeline via <c>FT.AGGREGATE</c>.</summary>
    /// <param name="query">The aggregation query to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the aggregation results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<AggregationResults> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.AGGREGATE",
            SearchQueryCommandBuilder.BuildAggregateArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return AggregationResultsParser.Parse(result);
    }

    /// <summary>Runs an aggregation pipeline and maps each result row to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to map aggregation rows into.</typeparam>
    /// <param name="query">The aggregation query to execute.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the rows.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the typed aggregation results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<AggregationResults<TDocument>> AggregateAsync<TDocument>(
        AggregationQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await AggregateAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    /// <summary>Runs a hybrid aggregation pipeline (text plus vector) via <c>FT.AGGREGATE</c>.</summary>
    /// <param name="query">The hybrid aggregation query to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the aggregation results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<AggregationResults> AggregateAsync(AggregateHybridQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.AGGREGATE",
            SearchQueryCommandBuilder.BuildAggregateHybridArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return AggregationResultsParser.Parse(result);
    }

    /// <summary>Runs a hybrid aggregation pipeline and maps each result row to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to map aggregation rows into.</typeparam>
    /// <param name="query">The hybrid aggregation query to execute.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the rows.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the typed aggregation results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<AggregationResults<TDocument>> AggregateAsync<TDocument>(
        AggregateHybridQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await AggregateAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    /// <summary>Runs a filter-only search and deserializes each result document to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The filter query to execute.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the typed search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        FilterQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    /// <summary>Runs a full-text search and deserializes each result document to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The text query to execute.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the typed search results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        TextQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    /// <summary>Streams a filter-only search in successive pages, yielding one <see cref="SearchResults"/> per batch.</summary>
    /// <param name="query">The filter query to page over; its pagination sets the starting offset.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of result batches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the effective batch size is not greater than zero.</exception>
    public IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        FilterQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        SearchBatchesAsyncCore(
            query,
            batchSize,
            SearchAsync,
            static query => query.Pagination,
            static (query, pagination) => new FilterQuery(
                query.Filter,
                query.ReturnFields,
                pagination: pagination,
                sortBy: query.SortBy),
            static _ => null,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

    /// <summary>Streams a filter-only search in successive pages, mapping each batch's documents to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The filter query to page over.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of typed result batches.</returns>
    public async IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        FilterQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in SearchBatchesAsync(query, batchSize, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return batch.Map<TDocument>(serializerOptions);
        }
    }

    /// <summary>Streams a full-text search in successive pages, yielding one <see cref="SearchResults"/> per batch.</summary>
    /// <param name="query">The text query to page over; its pagination sets the starting offset.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of result batches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the effective batch size is not greater than zero.</exception>
    public IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        TextQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        SearchBatchesAsyncCore(
            query,
            batchSize,
            SearchAsync,
            static query => query.Pagination,
            static (query, pagination) => new TextQuery(
                query.Text,
                query.ReturnFields,
                pagination: pagination,
                fieldWeights: query.FieldWeights.ToDictionary(
                    static weight => weight.Key,
                    static weight => weight.Value)),
            static _ => null,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

    /// <summary>Streams a full-text search in successive pages, mapping each batch's documents to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The text query to page over.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of typed result batches.</returns>
    public async IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        TextQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in SearchBatchesAsync(query, batchSize, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return batch.Map<TDocument>(serializerOptions);
        }
    }

    /// <summary>Streams a vector similarity search in successive pages, yielding one <see cref="SearchResults"/> per batch.</summary>
    /// <param name="query">The vector query to page over; paging is capped at the query's <c>TopK</c>.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of result batches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the effective batch size is not greater than zero.</exception>
    public IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        VectorQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        SearchBatchesAsyncCore(
            query,
            batchSize,
            SearchAsync,
            static query => query.Pagination,
            static (query, pagination) => new VectorQuery(
                query.FieldName,
                query.Vector,
                query.TopK,
                query.Filter,
                // Preserve the "unspecified" state so the cloned page keeps omitting RETURN; passing the
                // non-null empty ReturnFields would re-add the score alias and make batched typed queries throw.
                query.HasExplicitReturnFields ? query.ReturnFields : null,
                query.ScoreAlias,
                query.RuntimeOptions,
                pagination),
            static query => query.TopK,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

    /// <summary>Streams a vector similarity search in successive pages, mapping each batch's documents to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The vector query to page over.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of typed result batches.</returns>
    public async IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        VectorQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in SearchBatchesAsync(query, batchSize, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return batch.Map<TDocument>(serializerOptions);
        }
    }

    /// <summary>Streams a client-side hybrid search in successive pages, yielding one <see cref="SearchResults"/> per batch.</summary>
    /// <param name="query">The hybrid query to page over; paging is capped at the query's <c>TopK</c>.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of result batches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the effective batch size is not greater than zero.</exception>
    public IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        HybridQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        SearchBatchesAsyncCore(
            query,
            batchSize,
            SearchAsync,
            static query => query.Pagination,
            static (query, pagination) => new HybridQuery(
                query.TextFilter,
                query.VectorFieldName,
                query.Vector,
                query.TopK,
                query.Filter,
                // Preserve the "unspecified" state so the cloned page keeps omitting RETURN; passing the
                // non-null empty ReturnFields would re-add the score alias and make batched typed queries throw.
                query.HasExplicitReturnFields ? query.ReturnFields : null,
                query.ScoreAlias,
                query.RuntimeOptions,
                pagination),
            static query => query.TopK,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

    /// <summary>Streams a client-side hybrid search in successive pages, mapping each batch's documents to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The hybrid query to page over.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of typed result batches.</returns>
    public async IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        HybridQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in SearchBatchesAsync(query, batchSize, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return batch.Map<TDocument>(serializerOptions);
        }
    }

    /// <summary>Streams a vector range search in successive pages, yielding one <see cref="SearchResults"/> per batch.</summary>
    /// <param name="query">The vector range query to page over; its pagination sets the starting offset.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of result batches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the effective batch size is not greater than zero.</exception>
    public IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        VectorRangeQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        SearchBatchesAsyncCore(
            query,
            batchSize,
            SearchAsync,
            static query => query.Pagination,
            static (query, pagination) => new VectorRangeQuery(
                query.FieldName,
                query.Vector,
                query.DistanceThreshold,
                query.Filter,
                // Preserve the "unspecified" state so the cloned page keeps omitting RETURN; passing the
                // non-null empty ReturnFields would re-add the score alias and make batched typed queries throw.
                query.HasExplicitReturnFields ? query.ReturnFields : null,
                query.ScoreAlias,
                runtimeOptions: query.RuntimeOptions,
                pagination: pagination),
            static _ => null,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

    /// <summary>Streams a vector range search in successive pages, mapping each batch's documents to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The vector range query to page over.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of typed result batches.</returns>
    public async IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        VectorRangeQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in SearchBatchesAsync(query, batchSize, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return batch.Map<TDocument>(serializerOptions);
        }
    }

    /// <summary>Streams a multi-vector search in successive pages, yielding one combined <see cref="SearchResults"/> per batch.</summary>
    /// <param name="query">The multi-vector query to page over; paging is capped at the query's <c>TopK</c>.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of result batches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the effective batch size is not greater than zero.</exception>
    public IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        MultiVectorQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        SearchBatchesAsyncCore(
            query,
            batchSize,
            SearchAsync,
            static query => query.Pagination,
            static (query, pagination) => new MultiVectorQuery(
                query.Vectors,
                query.TopK,
                query.Filter,
                // Preserve the "unspecified" state so the cloned page keeps omitting RETURN in its fan-out and
                // the combiner keeps copying all stored fields; passing the non-null empty ProjectedFields
                // would narrow combined documents to just the score and make batched typed queries throw.
                query.HasExplicitReturnFields ? query.ProjectedFields : null,
                query.ScoreAlias,
                query.RuntimeOptions,
                pagination),
            static query => query.TopK,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

    /// <summary>Streams a multi-vector search in successive pages, mapping each batch's documents to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to deserialize result documents into.</typeparam>
    /// <param name="query">The multi-vector query to page over.</param>
    /// <param name="batchSize">The number of documents to request per batch; defaults to the query's page size.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the documents.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of typed result batches.</returns>
    public async IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        MultiVectorQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in SearchBatchesAsync(query, batchSize, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return batch.Map<TDocument>(serializerOptions);
        }
    }

    /// <summary>Streams an aggregation pipeline in successive pages, yielding one <see cref="AggregationResults"/> per batch until a short page is returned.</summary>
    /// <param name="query">The aggregation query to page over; its pagination sets the starting offset.</param>
    /// <param name="batchSize">The number of rows to request per batch; defaults to the query's page size.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of aggregation result batches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the effective batch size is not greater than zero.</exception>
    public IAsyncEnumerable<AggregationResults> AggregateBatchesAsync(
        AggregationQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        AggregateBatchesAsyncCore(
            query,
            batchSize,
            AggregateAsync,
            static query => query.Pagination,
            static (query, pagination) => new AggregationQuery(
                query.QueryString,
                query.LoadFields,
                query.ApplyClauses,
                query.GroupBy,
                query.SortBy,
                pagination: pagination),
            cancellationToken);

    /// <summary>Streams an aggregation pipeline in successive pages, mapping each batch's rows to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to map aggregation rows into.</typeparam>
    /// <param name="query">The aggregation query to page over.</param>
    /// <param name="batchSize">The number of rows to request per batch; defaults to the query's page size.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the rows.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of typed aggregation result batches.</returns>
    public async IAsyncEnumerable<AggregationResults<TDocument>> AggregateBatchesAsync<TDocument>(
        AggregationQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in AggregateBatchesAsync(query, batchSize, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return batch.Map<TDocument>(serializerOptions);
        }
    }

    /// <summary>Streams a hybrid aggregation pipeline in successive pages, yielding one <see cref="AggregationResults"/> per batch until a short page is returned.</summary>
    /// <param name="query">The hybrid aggregation query to page over; its pagination sets the starting offset.</param>
    /// <param name="batchSize">The number of rows to request per batch; defaults to the query's page size.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of aggregation result batches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the effective batch size is not greater than zero.</exception>
    public IAsyncEnumerable<AggregationResults> AggregateBatchesAsync(
        AggregateHybridQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        AggregateBatchesAsyncCore(
            query,
            batchSize,
            AggregateAsync,
            static query => query.Pagination,
            static (query, pagination) => new AggregateHybridQuery(
                query.TextFilter,
                query.VectorFieldName,
                query.Vector,
                query.TopK,
                query.Filter,
                query.LoadFields,
                query.ApplyClauses,
                query.GroupBy,
                query.SortBy,
                scoreAlias: query.ScoreAlias,
                runtimeOptions: query.RuntimeOptions,
                pagination: pagination),
            cancellationToken);

    /// <summary>Streams a hybrid aggregation pipeline in successive pages, mapping each batch's rows to <typeparamref name="TDocument"/>.</summary>
    /// <typeparam name="TDocument">The type to map aggregation rows into.</typeparam>
    /// <param name="query">The hybrid aggregation query to page over.</param>
    /// <param name="batchSize">The number of rows to request per batch; defaults to the query's page size.</param>
    /// <param name="serializerOptions">Optional serializer options used when mapping the rows.</param>
    /// <param name="cancellationToken">A token used to cancel the enumeration.</param>
    /// <returns>An async sequence of typed aggregation result batches.</returns>
    public async IAsyncEnumerable<AggregationResults<TDocument>> AggregateBatchesAsync<TDocument>(
        AggregateHybridQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in AggregateBatchesAsync(query, batchSize, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return batch.Map<TDocument>(serializerOptions);
        }
    }

    /// <summary>Counts the documents matching a query by issuing an <c>FT.SEARCH</c> that returns only the total count.</summary>
    /// <param name="query">The count query describing the filter to match.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that resolves to the number of matching documents.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public async Task<long> CountAsync(CountQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildCountArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result).TotalCount;
    }

    private async Task<RedisResult> ExecuteAsync(string command, object[] arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.ExecuteAsync(command, arguments).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<SearchResults> SearchBatchesAsyncCore<TQuery>(
        TQuery query,
        int? batchSize,
        Func<TQuery, CancellationToken, Task<SearchResults>> executeAsync,
        Func<TQuery, QueryPagination> getPagination,
        Func<TQuery, QueryPagination, TQuery> cloneWithPagination,
        Func<TQuery, int?> getMaxWindow,
        Func<SearchResults, long> getTotalCount,
        Func<SearchResults, int> getBatchCount,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var initialPagination = getPagination(query);
        var effectiveBatchSize = GetBatchSize(batchSize, initialPagination);
        var maxWindow = getMaxWindow(query);
        if (maxWindow is not null && initialPagination.Offset >= maxWindow.Value)
        {
            yield break;
        }

        var offset = initialPagination.Offset;
        long? totalCount = null;

        while (!totalCount.HasValue || offset < totalCount.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentBatchSize = ResolveBatchSize(offset, effectiveBatchSize, maxWindow);
            if (currentBatchSize == 0)
            {
                yield break;
            }

            var currentQuery = cloneWithPagination(query, new QueryPagination(offset, currentBatchSize));
            var batch = await executeAsync(currentQuery, cancellationToken).ConfigureAwait(false);
            yield return batch;

            var count = getBatchCount(batch);
            if (count == 0)
            {
                yield break;
            }

            totalCount = getTotalCount(batch);
            offset += effectiveBatchSize;
        }
    }

    private static async IAsyncEnumerable<AggregationResults> AggregateBatchesAsyncCore<TQuery>(
        TQuery query,
        int? batchSize,
        Func<TQuery, CancellationToken, Task<AggregationResults>> executeAsync,
        Func<TQuery, QueryPagination> getPagination,
        Func<TQuery, QueryPagination, TQuery> cloneWithPagination,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var initialPagination = getPagination(query);
        var effectiveBatchSize = GetBatchSize(batchSize, initialPagination);
        var offset = initialPagination.Offset;
        var isFirstBatch = true;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentQuery = cloneWithPagination(query, new QueryPagination(offset, effectiveBatchSize));
            var batch = await executeAsync(currentQuery, cancellationToken).ConfigureAwait(false);

            // FT.AGGREGATE's leading reply element (surfaced as AggregationResults.TotalCount)
            // is not a reliable count of matching rows: for non-GROUPBY (LOAD/APPLY-only)
            // pipelines Redis returns 1 rather than the true row count, so it cannot be used to
            // terminate paging. Instead page until a batch comes back short of the requested
            // size. An empty batch after the first page just means we landed exactly on the
            // end of the result set, so it is not surfaced to the caller.
            if (batch.Rows.Count == 0 && !isFirstBatch)
            {
                yield break;
            }

            yield return batch;
            isFirstBatch = false;

            if (batch.Rows.Count < effectiveBatchSize)
            {
                yield break;
            }

            offset += effectiveBatchSize;
        }
    }

    private static int GetBatchSize(int? batchSize, QueryPagination pagination)
    {
        var effectiveBatchSize = batchSize ?? pagination.Limit;
        if (effectiveBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                batchSize.HasValue ? nameof(batchSize) : nameof(pagination),
                effectiveBatchSize,
                "Batch size must be greater than zero.");
        }

        return effectiveBatchSize;
    }

    private static int ResolveBatchSize(int offset, int batchSize, int? maxWindow)
    {
        if (maxWindow is null)
        {
            return batchSize;
        }

        var remaining = maxWindow.Value - offset;
        return remaining <= 0 ? 0 : Math.Min(batchSize, remaining);
    }

    private static async Task<SearchIndexInfo> LoadInfoAsync(
        IDatabase database,
        string indexName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await database.ExecuteAsync(InfoCommand, [indexName]).WaitAsync(cancellationToken).ConfigureAwait(false);
        return SearchIndexInfo.FromRedisResult(result);
    }

    private static SearchResults CombineMultiVectorResults(MultiVectorQuery query, IReadOnlyList<SearchResults> perVectorResults)
    {
        if (perVectorResults.Count == 0)
        {
            return new SearchResults(0, []);
        }

        var scoreLookups = new List<Dictionary<string, double>>(perVectorResults.Count);
        var documentLookups = new List<Dictionary<string, SearchDocument>>(perVectorResults.Count);

        for (var index = 0; index < perVectorResults.Count; index++)
        {
            var scoreAlias = SearchQueryCommandBuilder.GetMultiVectorScoreAlias(index);
            var scores = new Dictionary<string, double>(StringComparer.Ordinal);
            var documents = new Dictionary<string, SearchDocument>(StringComparer.Ordinal);

            foreach (var document in perVectorResults[index].Documents)
            {
                if (!document.TryGetValue(scoreAlias, out var value))
                {
                    continue;
                }

                scores[document.Id] = ParseScore(value, scoreAlias, document.Id);
                documents[document.Id] = document;
            }

            scoreLookups.Add(scores);
            documentLookups.Add(documents);
        }

        var candidateIds = scoreLookups[0].Keys
            .Where(id => scoreLookups.All(scores => scores.ContainsKey(id)))
            .ToArray();

        var combinedDocuments = candidateIds
            .Select(id => CreateCombinedSearchDocument(id, query, scoreLookups, documentLookups))
            .OrderBy(static item => item.CombinedScore)
            .ThenBy(item => item.PerVectorScores, ScoreSequenceComparer.Instance)
            .ThenBy(item => item.Document.Id, StringComparer.Ordinal)
            .ToArray();

        var totalCount = combinedDocuments.LongLength;
        var topDocuments = combinedDocuments
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(static item => item.Document)
            .ToArray();

        return new SearchResults(totalCount, topDocuments);
    }

    private static CombinedSearchDocument CreateCombinedSearchDocument(
        string documentId,
        MultiVectorQuery query,
        IReadOnlyList<Dictionary<string, double>> scoreLookups,
        IReadOnlyList<Dictionary<string, SearchDocument>> documentLookups)
    {
        var values = new Dictionary<string, RedisValue>(StringComparer.Ordinal);
        var perVectorScores = new double[scoreLookups.Count];
        var combinedScore = 0d;

        for (var index = 0; index < scoreLookups.Count; index++)
        {
            var score = scoreLookups[index][documentId];
            perVectorScores[index] = score;
            combinedScore += query.Vectors[index].Weight * score;

            var sourceDocument = documentLookups[index][documentId];

            if (query.HasExplicitReturnFields)
            {
                // The caller narrowed the projection, so copy exactly those fields from the sub-results.
                foreach (var fieldName in query.ProjectedFields)
                {
                    if (!values.ContainsKey(fieldName) &&
                        sourceDocument.TryGetValue(fieldName, out var fieldValue))
                    {
                        values[fieldName] = fieldValue;
                    }
                }
            }
            else
            {
                // Projected fields were left unspecified, so each sub-query omitted RETURN and the server
                // returned every stored field alongside this vector's internal score alias. Copy all stored
                // fields, skipping the internal per-vector score alias so it never leaks into combined results.
                var internalScoreAlias = SearchQueryCommandBuilder.GetMultiVectorScoreAlias(index);
                foreach (var (fieldName, fieldValue) in sourceDocument.Values)
                {
                    if (values.ContainsKey(fieldName) ||
                        string.Equals(fieldName, internalScoreAlias, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    values[fieldName] = fieldValue;
                }
            }
        }

        values[query.ScoreAlias] = combinedScore.ToString("G17", CultureInfo.InvariantCulture);
        return new CombinedSearchDocument(new SearchDocument(documentId, values), combinedScore, perVectorScores);
    }

    private static double ParseScore(RedisValue value, string scoreAlias, string documentId)
    {
        if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
        {
            return score;
        }

        throw new InvalidOperationException(
            $"Document '{documentId}' returned a non-numeric score for field '{scoreAlias}'.");
    }

    private void EnsureJsonStorage()
    {
        if (Schema.Index.StorageType != StorageType.Json)
        {
            throw new InvalidOperationException("JSON document operations require a schema configured with JSON storage.");
        }
    }

    private void EnsureHashStorage()
    {
        if (Schema.Index.StorageType != StorageType.Hash)
        {
            throw new InvalidOperationException("Hash document operations require a schema configured with HASH storage.");
        }
    }

    private async Task SetJsonDocumentAsync<TDocument>(string key, TDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JsonSerializer.Serialize(document, _serializerOptions);
        await ExecuteAsync("JSON.SET", [key, "$", payload], cancellationToken).ConfigureAwait(false);
    }

    private async Task SetHashDocumentAsync<TDocument>(string key, TDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = HashDocumentMapper.ToHashEntries(document, _serializerOptions);
        await _database.HashSetAsync(key, entries).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> DeleteDocumentsByPrefixAsync(string prefix, int batchSize, CancellationToken cancellationToken)
    {
        // The prefix is a literal, but SCAN MATCH takes a glob pattern, so any
        // glob metacharacter in the prefix must be escaped or it would match
        // (and delete) unrelated keys.
        var pattern = (RedisValue)(GlobEscape(prefix) + "*");
        var deletedCount = 0L;
        var batch = new List<RedisKey>(batchSize);

        // Enumerate every keyspace-owning endpoint. On a cluster each master
        // owns a distinct set of slots, so a single-node SCAN would miss keys
        // on other shards; IServer.KeysAsync scans one node at a time.
        foreach (var server in GetKeyspaceServers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            await foreach (var key in server
                .KeysAsync(_database.Database, pattern, pageSize: batchSize)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                batch.Add(key);
                if (batch.Count >= batchSize)
                {
                    deletedCount += await DeleteKeysAsync(batch, cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                deletedCount += await DeleteKeysAsync(batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        return deletedCount;
    }

    private IEnumerable<IServer> GetKeyspaceServers()
    {
        var multiplexer = _database.Multiplexer;
        foreach (var endpoint in multiplexer.GetEndPoints())
        {
            var server = multiplexer.GetServer(endpoint);

            // Skip replicas (their keyspace mirrors a master, so scanning them
            // double-counts) and endpoints that hold no keyspace or are down.
            if (!server.IsConnected || server.IsReplica || server.ServerType == ServerType.Sentinel)
            {
                continue;
            }

            yield return server;
        }
    }

    private async Task<long> DeleteKeysAsync(IReadOnlyList<RedisKey> keys, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Delete one key per command rather than a single multi-key DEL: on a
        // cluster a batch spanning multiple hash slots would raise CROSSSLOT.
        // The commands are pipelined over the multiplexer, so this remains a
        // single round trip per batch.
        var deletions = new Task<bool>[keys.Count];
        for (var i = 0; i < keys.Count; i++)
        {
            deletions[i] = _database.KeyDeleteAsync(keys[i]);
        }

        var results = await Task.WhenAll(deletions).WaitAsync(cancellationToken).ConfigureAwait(false);
        return results.Count(static deleted => deleted);
    }

    private static string GlobEscape(string value)
    {
        // Escape Redis glob-pattern metacharacters (\ * ? [ ]) so the value is
        // matched literally by SCAN MATCH.
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '\\' or '*' or '?' or '[' or ']')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static bool IsUnknownIndexException(RedisServerException exception) =>
        exception.Message.Contains("Unknown Index name", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Unknown index name", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("no such index", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("SEARCH_INDEX_NOT_FOUND", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Index not found", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> JsonDocumentExistsAsync(string key, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync("JSON.GET", [key, "$"], cancellationToken).ConfigureAwait(false);
        return result is not null && !result.IsNull;
    }

    private async Task<bool> HashDocumentExistsAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await _database.HashGetAllAsync(key).WaitAsync(cancellationToken).ConfigureAwait(false);
        return entries.Length > 0;
    }

    private static IReadOnlyList<JsonPartialUpdate> NormalizeJsonPartialUpdates(IEnumerable<JsonPartialUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);

        var normalizedUpdates = new List<JsonPartialUpdate>();
        var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var update in updates)
        {
            var normalizedPath = NormalizeJsonPath(update.Path);
            if (!uniquePaths.Add(normalizedPath))
            {
                throw new ArgumentException($"Duplicate JSON update path '{normalizedPath}' is not allowed.", nameof(updates));
            }

            normalizedUpdates.Add(update with { Path = normalizedPath });
        }

        if (normalizedUpdates.Count == 0)
        {
            throw new ArgumentException("At least one JSON partial update is required.", nameof(updates));
        }

        return normalizedUpdates;
    }

    private static string NormalizeJsonPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = path.Trim();
        if (normalizedPath == "$" ||
            (!normalizedPath.StartsWith("$.", StringComparison.Ordinal) &&
             !normalizedPath.StartsWith("$[", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "JSON partial update paths must be absolute JSONPath expressions like '$.title' or '$.items[0]'.",
                nameof(path));
        }

        return normalizedPath;
    }

    private static IReadOnlyList<HashPartialUpdate> NormalizeHashPartialUpdates(IEnumerable<HashPartialUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);

        var normalizedUpdates = new List<HashPartialUpdate>();
        var uniqueFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var update in updates)
        {
            var normalizedField = NormalizeHashField(update.Field);
            if (!uniqueFields.Add(normalizedField))
            {
                throw new ArgumentException($"Duplicate hash update field '{normalizedField}' is not allowed.", nameof(updates));
            }

            if (update.Value is null)
            {
                throw new ArgumentException(
                    $"Hash partial update field '{normalizedField}' cannot be null. HASH updates only support setting concrete top-level values.",
                    nameof(updates));
            }

            normalizedUpdates.Add(update with { Field = normalizedField });
        }

        if (normalizedUpdates.Count == 0)
        {
            throw new ArgumentException("At least one hash partial update is required.", nameof(updates));
        }

        return normalizedUpdates;
    }

    private static string NormalizeHashField(string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return field.Trim();
    }

    private sealed record CombinedSearchDocument(SearchDocument Document, double CombinedScore, double[] PerVectorScores);

    private sealed class ScoreSequenceComparer : IComparer<double[]>
    {
        public static ScoreSequenceComparer Instance { get; } = new();

        public int Compare(double[]? left, double[]? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var length = Math.Min(left.Length, right.Length);
            for (var index = 0; index < length; index++)
            {
                var comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return left.Length.CompareTo(right.Length);
        }
    }
}

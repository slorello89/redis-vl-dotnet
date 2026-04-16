using RedisVL.Schema;
using RedisVL.Queries;
using StackExchange.Redis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace RedisVL.Indexes;

public sealed class SearchIndex
{
    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _serializerOptions;
    private const string ListIndexesCommand = "FT._LIST";
    private const string InfoCommand = "FT.INFO";

    public SearchIndex(IDatabase database, SearchSchema schema)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(schema);

        _database = database;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Schema = schema;
    }

    public SearchSchema Schema { get; }

    public static IReadOnlyList<SearchIndexListItem> List(IDatabase database) =>
        ListAsync(database).GetAwaiter().GetResult();

    public static SearchIndex FromExisting(IDatabase database, string indexName) =>
        FromExistingAsync(database, indexName).GetAwaiter().GetResult();

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

    public static async Task<IReadOnlyList<SearchIndexListItem>> ListAsync(
        IDatabase database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await database.ExecuteAsync(ListIndexesCommand, []).WaitAsync(cancellationToken).ConfigureAwait(false);
        return SearchIndexListItem.FromRedisResult(result);
    }

    public bool Create(CreateIndexOptions? options = null) =>
        CreateAsync(options).GetAwaiter().GetResult();

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

    public bool Exists() =>
        ExistsAsync().GetAwaiter().GetResult();

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

    public SearchIndexInfo Info() =>
        InfoAsync().GetAwaiter().GetResult();

    public async Task<SearchIndexInfo> InfoAsync(CancellationToken cancellationToken = default)
    {
        return await LoadInfoAsync(_database, Schema.Index.Name, cancellationToken).ConfigureAwait(false);
    }

    public void Drop(bool deleteDocuments = false) =>
        DropAsync(deleteDocuments).GetAwaiter().GetResult();

    public async Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync("FT.DROPINDEX", SearchIndexCommandBuilder.BuildDropArguments(Schema, deleteDocuments), cancellationToken)
            .ConfigureAwait(false);
    }

    public long Clear(int batchSize = 1000) =>
        ClearAsync(batchSize).GetAwaiter().GetResult();

    public async Task<long> ClearAsync(int batchSize = 1000, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var deletedCount = 0L;
        foreach (var prefix in Schema.Index.Prefixes)
        {
            deletedCount += await DeleteDocumentsByPrefixAsync(prefix, batchSize, cancellationToken).ConfigureAwait(false);
        }

        return deletedCount;
    }

    public string LoadJson<TDocument>(TDocument document, string? key = null, string? id = null) =>
        LoadJsonAsync(document, key, id).GetAwaiter().GetResult();

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

    public IReadOnlyList<string> LoadJson<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null) =>
        LoadJsonAsync(documents, keySelector, idSelector).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<string>> LoadJsonAsync<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null,
        CancellationToken cancellationToken = default)
    {
        EnsureJsonStorage();
        ArgumentNullException.ThrowIfNull(documents);

        var loadedKeys = new List<string>();
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolvedKey = DocumentKeyResolver.ResolveKeyForSelectors(Schema, document, keySelector, idSelector);
            await SetJsonDocumentAsync(resolvedKey, document, cancellationToken).ConfigureAwait(false);
            loadedKeys.Add(resolvedKey);
        }

        return loadedKeys;
    }

    public TDocument? FetchJsonByKey<TDocument>(string key) =>
        FetchJsonByKeyAsync<TDocument>(key).GetAwaiter().GetResult();

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

    public TDocument? FetchJsonById<TDocument>(string id) =>
        FetchJsonByIdAsync<TDocument>(id).GetAwaiter().GetResult();

    public Task<TDocument?> FetchJsonByIdAsync<TDocument>(string id, CancellationToken cancellationToken = default) =>
        FetchJsonByKeyAsync<TDocument>(DocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

    public bool DeleteJsonByKey(string key) =>
        DeleteJsonByKeyAsync(key).GetAwaiter().GetResult();

    public async Task<bool> DeleteJsonByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureJsonStorage();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var deleted = await _database.KeyDeleteAsync(key.Trim()).WaitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    public bool DeleteJsonById(string id) =>
        DeleteJsonByIdAsync(id).GetAwaiter().GetResult();

    public Task<bool> DeleteJsonByIdAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteJsonByKeyAsync(DocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

    public bool UpdateJsonByKey(string key, params JsonPartialUpdate[] updates) =>
        UpdateJsonByKeyAsync(key, updates).GetAwaiter().GetResult();

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

    public bool UpdateJsonById(string id, params JsonPartialUpdate[] updates) =>
        UpdateJsonByIdAsync(id, updates).GetAwaiter().GetResult();

    public Task<bool> UpdateJsonByIdAsync(
        string id,
        IEnumerable<JsonPartialUpdate> updates,
        CancellationToken cancellationToken = default) =>
        UpdateJsonByKeyAsync(DocumentKeyResolver.ResolveKeyFromId(Schema, id), updates, cancellationToken);

    public string LoadHash<TDocument>(TDocument document, string? key = null, string? id = null) =>
        LoadHashAsync(document, key, id).GetAwaiter().GetResult();

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

    public IReadOnlyList<string> LoadHash<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null) =>
        LoadHashAsync(documents, keySelector, idSelector).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<string>> LoadHashAsync<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHashStorage();
        ArgumentNullException.ThrowIfNull(documents);

        var loadedKeys = new List<string>();
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolvedKey = DocumentKeyResolver.ResolveKeyForSelectors(Schema, document, keySelector, idSelector);
            await SetHashDocumentAsync(resolvedKey, document, cancellationToken).ConfigureAwait(false);
            loadedKeys.Add(resolvedKey);
        }

        return loadedKeys;
    }

    public TDocument? FetchHashByKey<TDocument>(string key) =>
        FetchHashByKeyAsync<TDocument>(key).GetAwaiter().GetResult();

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

    public TDocument? FetchHashById<TDocument>(string id) =>
        FetchHashByIdAsync<TDocument>(id).GetAwaiter().GetResult();

    public Task<TDocument?> FetchHashByIdAsync<TDocument>(string id, CancellationToken cancellationToken = default) =>
        FetchHashByKeyAsync<TDocument>(DocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

    public bool DeleteHashByKey(string key) =>
        DeleteHashByKeyAsync(key).GetAwaiter().GetResult();

    public async Task<bool> DeleteHashByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureHashStorage();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var deleted = await _database.KeyDeleteAsync(key.Trim()).WaitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    public bool DeleteHashById(string id) =>
        DeleteHashByIdAsync(id).GetAwaiter().GetResult();

    public Task<bool> DeleteHashByIdAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteHashByKeyAsync(DocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

    public bool UpdateHashByKey(string key, params HashPartialUpdate[] updates) =>
        UpdateHashByKeyAsync(key, updates).GetAwaiter().GetResult();

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

    public bool UpdateHashById(string id, params HashPartialUpdate[] updates) =>
        UpdateHashByIdAsync(id, updates).GetAwaiter().GetResult();

    public Task<bool> UpdateHashByIdAsync(
        string id,
        IEnumerable<HashPartialUpdate> updates,
        CancellationToken cancellationToken = default) =>
        UpdateHashByKeyAsync(DocumentKeyResolver.ResolveKeyFromId(Schema, id), updates, cancellationToken);

    public SearchResults Search(VectorQuery query) =>
        SearchAsync(query).GetAwaiter().GetResult();

    public SearchResults<TDocument> Search<TDocument>(VectorQuery query, JsonSerializerOptions? serializerOptions = null) =>
        SearchAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

    public SearchResults Search(MultiVectorQuery query) =>
        SearchAsync(query).GetAwaiter().GetResult();

    public SearchResults<TDocument> Search<TDocument>(MultiVectorQuery query, JsonSerializerOptions? serializerOptions = null) =>
        SearchAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

    public SearchResults Search(FilterQuery query) =>
        SearchAsync(query).GetAwaiter().GetResult();

    public SearchResults<TDocument> Search<TDocument>(FilterQuery query, JsonSerializerOptions? serializerOptions = null) =>
        SearchAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

    public SearchResults Search(TextQuery query) =>
        SearchAsync(query).GetAwaiter().GetResult();

    public SearchResults<TDocument> Search<TDocument>(TextQuery query, JsonSerializerOptions? serializerOptions = null) =>
        SearchAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

    public AggregationResults Aggregate(AggregationQuery query) =>
        AggregateAsync(query).GetAwaiter().GetResult();

    public AggregationResults<TDocument> Aggregate<TDocument>(AggregationQuery query, JsonSerializerOptions? serializerOptions = null) =>
        AggregateAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

    public AggregationResults Aggregate(AggregateHybridQuery query) =>
        AggregateAsync(query).GetAwaiter().GetResult();

    public AggregationResults<TDocument> Aggregate<TDocument>(AggregateHybridQuery query, JsonSerializerOptions? serializerOptions = null) =>
        AggregateAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

    public SearchResults Search(HybridQuery query) =>
        SearchAsync(query).GetAwaiter().GetResult();

    public SearchResults<TDocument> Search<TDocument>(HybridQuery query, JsonSerializerOptions? serializerOptions = null) =>
        SearchAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

    public SearchResults Search(VectorRangeQuery query) =>
        SearchAsync(query).GetAwaiter().GetResult();

    public SearchResults<TDocument> Search<TDocument>(VectorRangeQuery query, JsonSerializerOptions? serializerOptions = null) =>
        SearchAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

    public async Task<SearchResults> SearchAsync(VectorQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildVectorSearchArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    public async Task<SearchResults> SearchAsync(MultiVectorQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var perVectorResults = new List<SearchResults>(query.Vectors.Count);
        foreach (var arguments in SearchQueryCommandBuilder.BuildMultiVectorSearchArguments(Schema, query))
        {
            var result = await ExecuteAsync("FT.SEARCH", arguments, cancellationToken).ConfigureAwait(false);
            perVectorResults.Add(SearchResultsParser.Parse(result));
        }

        return CombineMultiVectorResults(query, perVectorResults);
    }

    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        VectorQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        MultiVectorQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    public async Task<SearchResults> SearchAsync(HybridQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildHybridSearchArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        HybridQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    public async Task<SearchResults> SearchAsync(VectorRangeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildVectorRangeArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        VectorRangeQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    public async Task<SearchResults> SearchAsync(FilterQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildFilterSearchArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    public async Task<SearchResults> SearchAsync(TextQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildTextSearchArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    public async Task<AggregationResults> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.AGGREGATE",
            SearchQueryCommandBuilder.BuildAggregateArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return AggregationResultsParser.Parse(result);
    }

    public async Task<AggregationResults<TDocument>> AggregateAsync<TDocument>(
        AggregationQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await AggregateAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    public async Task<AggregationResults> AggregateAsync(AggregateHybridQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.AGGREGATE",
            SearchQueryCommandBuilder.BuildAggregateHybridArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return AggregationResultsParser.Parse(result);
    }

    public async Task<AggregationResults<TDocument>> AggregateAsync<TDocument>(
        AggregateHybridQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await AggregateAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        FilterQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        TextQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return results.Map<TDocument>(serializerOptions);
    }

    public IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        FilterQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        SearchBatchesAsyncCore(
            query,
            batchSize,
            SearchAsync,
            static query => query.Pagination,
            static (query, pagination) => new FilterQuery(query.Filter, query.ReturnFields, pagination: pagination),
            static _ => null,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

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

    public IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        TextQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default) =>
        SearchBatchesAsyncCore(
            query,
            batchSize,
            SearchAsync,
            static query => query.Pagination,
            static (query, pagination) => new TextQuery(query.Text, query.ReturnFields, pagination: pagination),
            static _ => null,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

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
                query.ReturnFields,
                query.ScoreAlias,
                query.RuntimeOptions,
                pagination),
            static query => query.TopK,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

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
                query.ReturnFields,
                query.ScoreAlias,
                query.RuntimeOptions,
                pagination),
            static query => query.TopK,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

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
                query.ReturnFields,
                query.ScoreAlias,
                runtimeOptions: query.RuntimeOptions,
                pagination: pagination),
            static _ => null,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

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
                query.ProjectedFields,
                query.ScoreAlias,
                query.RuntimeOptions,
                pagination),
            static query => query.TopK,
            static result => result.TotalCount,
            static result => result.Documents.Count,
            cancellationToken);

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

    public long Count(CountQuery query) =>
        CountAsync(query).GetAwaiter().GetResult();

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
        long? totalCount = null;

        while (!totalCount.HasValue || offset < totalCount.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentQuery = cloneWithPagination(query, new QueryPagination(offset, effectiveBatchSize));
            var batch = await executeAsync(currentQuery, cancellationToken).ConfigureAwait(false);
            yield return batch;

            if (batch.Rows.Count == 0)
            {
                yield break;
            }

            totalCount = batch.TotalCount;
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

            foreach (var fieldName in query.ProjectedFields)
            {
                if (!values.ContainsKey(fieldName) &&
                    documentLookups[index][documentId].TryGetValue(fieldName, out var fieldValue))
                {
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
        var deletedCount = 0L;
        var cursor = 0L;
        var pattern = $"{prefix}*";

        do
        {
            var (nextCursor, keys) = await ScanKeysAsync(cursor, pattern, batchSize, cancellationToken).ConfigureAwait(false);
            if (keys.Length > 0)
            {
                deletedCount += await _database.KeyDeleteAsync(keys).WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            cursor = nextCursor;
        }
        while (cursor != 0);

        return deletedCount;
    }

    private async Task<(long Cursor, RedisKey[] Keys)> ScanKeysAsync(
        long cursor,
        string pattern,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync("SCAN", [cursor, "MATCH", pattern, "COUNT", batchSize], cancellationToken).ConfigureAwait(false);
        var parts = (RedisResult[])result!;
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("Redis SCAN response must contain a cursor and key list.");
        }

        if (!long.TryParse(parts[0].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var nextCursor))
        {
            throw new InvalidOperationException("Redis SCAN response cursor was not a valid integer.");
        }

        var keys = ((RedisResult[])parts[1]!)
            .Select(static entry => entry.ToString())
            .Where(static entry => !string.IsNullOrWhiteSpace(entry))
            .Select(static entry => (RedisKey)entry!)
            .ToArray();

        return (nextCursor, keys);
    }

    private static bool IsUnknownIndexException(RedisServerException exception) =>
        exception.Message.Contains("Unknown Index name", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Unknown index name", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("no such index", StringComparison.OrdinalIgnoreCase);

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

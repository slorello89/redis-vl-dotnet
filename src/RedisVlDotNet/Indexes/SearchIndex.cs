using RedisVlDotNet.Schema;
using RedisVlDotNet.Queries;
using StackExchange.Redis;
using System.Text.Json;

namespace RedisVlDotNet.Indexes;

public sealed class SearchIndex
{
    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _serializerOptions;

    public SearchIndex(IDatabase database, SearchSchema schema)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(schema);

        _database = database;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Schema = schema;
    }

    public SearchSchema Schema { get; }

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
        var result = await ExecuteAsync("FT.INFO", [Schema.Index.Name], cancellationToken).ConfigureAwait(false);
        return SearchIndexInfo.FromRedisResult(result);
    }

    public void Drop(bool deleteDocuments = false) =>
        DropAsync(deleteDocuments).GetAwaiter().GetResult();

    public async Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync("FT.DROPINDEX", SearchIndexCommandBuilder.BuildDropArguments(Schema, deleteDocuments), cancellationToken)
            .ConfigureAwait(false);
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

        var deleted = await _database.KeyDeleteAsync(key.Trim()).WaitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    public bool DeleteHashById(string id) =>
        DeleteHashByIdAsync(id).GetAwaiter().GetResult();

    public Task<bool> DeleteHashByIdAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteHashByKeyAsync(DocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

    public SearchResults Search(VectorQuery query) =>
        SearchAsync(query).GetAwaiter().GetResult();

    public SearchResults<TDocument> Search<TDocument>(VectorQuery query, JsonSerializerOptions? serializerOptions = null) =>
        SearchAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

    public SearchResults Search(FilterQuery query) =>
        SearchAsync(query).GetAwaiter().GetResult();

    public SearchResults<TDocument> Search<TDocument>(FilterQuery query, JsonSerializerOptions? serializerOptions = null) =>
        SearchAsync<TDocument>(query, serializerOptions).GetAwaiter().GetResult();

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

    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        VectorQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default) =>
        (await SearchAsync(query, cancellationToken).ConfigureAwait(false)).Map<TDocument>(serializerOptions);

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
        CancellationToken cancellationToken = default) =>
        (await SearchAsync(query, cancellationToken).ConfigureAwait(false)).Map<TDocument>(serializerOptions);

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
        CancellationToken cancellationToken = default) =>
        (await SearchAsync(query, cancellationToken).ConfigureAwait(false)).Map<TDocument>(serializerOptions);

    public async Task<SearchResults> SearchAsync(FilterQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await ExecuteAsync(
            "FT.SEARCH",
            SearchQueryCommandBuilder.BuildFilterSearchArguments(Schema, query),
            cancellationToken).ConfigureAwait(false);

        return SearchResultsParser.Parse(result);
    }

    public async Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        FilterQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default) =>
        (await SearchAsync(query, cancellationToken).ConfigureAwait(false)).Map<TDocument>(serializerOptions);

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
        var payload = JsonSerializer.Serialize(document, _serializerOptions);
        await ExecuteAsync("JSON.SET", [key, "$", payload], cancellationToken).ConfigureAwait(false);
    }

    private async Task SetHashDocumentAsync<TDocument>(string key, TDocument document, CancellationToken cancellationToken)
    {
        var entries = HashDocumentMapper.ToHashEntries(document, _serializerOptions);
        await _database.HashSetAsync(key, entries).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsUnknownIndexException(RedisServerException exception) =>
        exception.Message.Contains("Unknown Index name", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Unknown index name", StringComparison.OrdinalIgnoreCase);
}

using RedisVlDotNet.Schema;
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

        var resolvedKey = JsonDocumentKeyResolver.ResolveKey(Schema, document, key, id);
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

            var resolvedKey = JsonDocumentKeyResolver.ResolveKeyForSelectors(Schema, document, keySelector, idSelector);
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
        FetchJsonByKeyAsync<TDocument>(JsonDocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

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
        DeleteJsonByKeyAsync(JsonDocumentKeyResolver.ResolveKeyFromId(Schema, id), cancellationToken);

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

    private async Task SetJsonDocumentAsync<TDocument>(string key, TDocument document, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(document, _serializerOptions);
        await ExecuteAsync("JSON.SET", [key, "$", payload], cancellationToken).ConfigureAwait(false);
    }

    private static bool IsUnknownIndexException(RedisServerException exception) =>
        exception.Message.Contains("Unknown Index name", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Unknown index name", StringComparison.OrdinalIgnoreCase);
}

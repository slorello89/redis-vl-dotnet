using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using RedisVL.Vectorizers;
using StackExchange.Redis;

namespace RedisVL.Caches;

public sealed class SemanticCache
{
    private readonly IDatabase _database;
    private readonly SearchIndex _index;
    private readonly JsonSerializerOptions _serializerOptions;

    public SemanticCache(IDatabase database, SemanticCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _database = database;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Options = options;
        _index = new SearchIndex(database, CreateSchema(options));
    }

    public SemanticCacheOptions Options { get; }

    public string Name => Options.Name;

    public string? KeyNamespace => Options.KeyNamespace;

    public TimeSpan? TimeToLive => Options.TimeToLive;

    public double DistanceThreshold => Options.DistanceThreshold;

    public bool Create(CreateIndexOptions? options = null) =>
        CreateAsync(options).GetAwaiter().GetResult();

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

    public bool Exists() =>
        ExistsAsync().GetAwaiter().GetResult();

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        _index.ExistsAsync(cancellationToken);

    public void Drop(bool deleteDocuments = false) =>
        DropAsync(deleteDocuments).GetAwaiter().GetResult();

    public Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default) =>
        _index.DropAsync(deleteDocuments, cancellationToken);

    public SemanticCacheHit? Check(string prompt, float[] embedding, FilterExpression? filter = null) =>
        CheckAsync(prompt, embedding, filter).GetAwaiter().GetResult();

    public SemanticCacheHit? Check(string prompt, ITextVectorizer vectorizer, FilterExpression? filter = null) =>
        CheckAsync(prompt, vectorizer, filter).GetAwaiter().GetResult();

    public async Task<SemanticCacheHit?> CheckAsync(
        string prompt,
        float[] embedding,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default)
    {
        NormalizePrompt(prompt);
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
                limit: 1),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var document in results.Documents)
        {
            if (TryMapSearchHit(document, out var hit))
            {
                return hit;
            }
        }

        return null;
    }

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

    public string Store(
        string prompt,
        string response,
        float[] embedding,
        object? metadata = null,
        IReadOnlyDictionary<string, object?>? filterValues = null) =>
        StoreAsync(prompt, response, embedding, metadata, filterValues).GetAwaiter().GetResult();

    public string Store(
        string prompt,
        string response,
        ITextVectorizer vectorizer,
        object? metadata = null,
        IReadOnlyDictionary<string, object?>? filterValues = null) =>
        StoreAsync(prompt, response, vectorizer, metadata, filterValues).GetAwaiter().GetResult();

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
        ArgumentNullException.ThrowIfNull(embedding);
        var normalizedFilterValues = NormalizeFilterValues(filterValues);

        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(normalizedPrompt, normalizedFilterValues);
        var entries = new List<HashEntry>
        {
            new(Options.PromptFieldName, normalizedPrompt),
            new(Options.ResponseFieldName, normalizedResponse),
            new(Options.EmbeddingFieldName, EmbeddingsCache.EncodeFloat32(embedding))
        };

        var metadataPayload = SerializeMetadata(metadata);
        if (metadataPayload is not null)
        {
            entries.Add(new HashEntry(Options.MetadataFieldName, metadataPayload));
        }

        foreach (var filterValue in normalizedFilterValues)
        {
            entries.Add(new HashEntry(filterValue.Key, filterValue.Value));
        }

        await _database.HashSetAsync(key, entries.ToArray()).WaitAsync(cancellationToken).ConfigureAwait(false);

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

    internal RedisKey CreateKey(string prompt, IReadOnlyDictionary<string, RedisValue>? filterValues = null)
    {
        var hashInput = CreateCacheIdentityPayload(prompt, filterValues);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)));
        return $"{CreateKeyPrefix(Options)}{hash}";
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

public sealed record SemanticCacheHit(string Prompt, string Response, double Distance, string? Metadata = null);

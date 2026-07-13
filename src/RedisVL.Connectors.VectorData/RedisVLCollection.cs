using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using RedisVL.Connectors.VectorData.Mapping;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using StackExchange.Redis;

namespace RedisVL.Connectors.VectorData;

/// <summary>
/// A Microsoft.Extensions.VectorData <see cref="VectorStoreCollection{TKey, TRecord}"/> backed by a
/// RedisVL <see cref="SearchIndex"/> using JSON document storage.
/// </summary>
/// <typeparam name="TKey">The record key type. Only <see cref="string"/> is currently supported.</typeparam>
/// <typeparam name="TRecord">The record (POCO) type.</typeparam>
public sealed class RedisVLCollection<TKey, TRecord> : VectorStoreCollection<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
{
    internal const string ScoreAlias = "vector_distance";
    private const string JsonRootField = "$";

    private readonly IDatabase _database;
    private readonly SearchIndex _index;
    private readonly RedisVLRecordModel _model;
    private readonly RedisVLFilterTranslator _filterTranslator;
    private readonly string _keyPrefix;
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVLCollection{TKey, TRecord}"/> class.
    /// </summary>
    /// <param name="database">The StackExchange.Redis database backing the collection.</param>
    /// <param name="name">The collection name, also used as the default key prefix and index name.</param>
    /// <param name="options">Optional collection options such as key prefix, index name, and record definition.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    /// <exception cref="NotSupportedException">Thrown when <typeparamref name="TKey"/> is not <see cref="string"/>.</exception>
    public RedisVLCollection(IDatabase database, string name, RedisVLCollectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (typeof(TKey) != typeof(string) && typeof(TKey) != typeof(object))
        {
            throw new NotSupportedException("The RedisVL vector-data connector currently supports string keys only.");
        }

        _database = database;
        Name = name;
        options ??= new RedisVLCollectionOptions();
        _keyPrefix = string.IsNullOrEmpty(options.KeyPrefix) ? $"{name}:" : options.KeyPrefix;
        var indexName = string.IsNullOrWhiteSpace(options.IndexName) ? name : options.IndexName;

        _model = RedisVLRecordModel.Build(typeof(TRecord), options.Definition);
        _filterTranslator = new RedisVLFilterTranslator(_model);
        _index = new SearchIndex(_database, _model.BuildSchema(indexName, _keyPrefix));
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    /// <inheritdoc/>
    public override string Name { get; }

    /// <inheritdoc/>
    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default) =>
        _index.ExistsAsync(cancellationToken);

    /// <inheritdoc/>
    public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default) =>
        _index.CreateAsync(new CreateIndexOptions(skipIfExists: true), cancellationToken);

    /// <inheritdoc/>
    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        if (await _index.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await _index.DropAsync(deleteDocuments: true, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public override async Task<TRecord?> GetAsync(
        TKey key,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var record = await _index.FetchJsonByKeyAsync<TRecord>(ToRedisKey(key), cancellationToken).ConfigureAwait(false);
        if (record is not null && !(options?.IncludeVectors ?? false))
        {
            ClearVectors(record);
        }

        return record;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);

        var sortBy = ResolveSortBy(options);
        var includeVectors = options?.IncludeVectors ?? false;
        var skip = options?.Skip ?? 0;
        var query = new FilterQuery(
            _filterTranslator.Translate(filter),
            returnFields: [JsonRootField],
            pagination: new QueryPagination(offset: skip, limit: top),
            sortBy: sortBy);

        var results = await _index.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        foreach (var document in results.Documents)
        {
            var record = await MaterializeAsync(document, cancellationToken).ConfigureAwait(false);
            if (record is not null)
            {
                if (!includeVectors)
                {
                    ClearVectors(record);
                }

                yield return record;
            }
        }
    }

    /// <inheritdoc/>
    public override async Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _index.LoadJsonAsync(record, key: ToRedisKey(GetRecordKey(record)), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        // Delegate to the pipelined batch loader so records are dispatched concurrently over the
        // multiplexer rather than awaited one round trip at a time.
        await _index.LoadJsonAsync(
            records,
            keySelector: record => ToRedisKey(GetRecordKey(record)),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _index.DeleteJsonByKeyAsync(ToRedisKey(key), cancellationToken);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);

        options ??= new VectorSearchOptions<TRecord>();
        ValidateSearchOptions(options);

        var vectorProperty = _model.ResolveVector(GetVectorPropertyName(options.VectorProperty));
        var skip = options.Skip;
        var filter = options.Filter is null ? null : _filterTranslator.Translate(options.Filter);

        // A ScoreThreshold becomes an FT.SEARCH VECTOR_RANGE query (return everything within a
        // distance radius, nearest first); otherwise it is a plain KNN over skip + top candidates.
        var results = options.ScoreThreshold is double threshold
            ? await _index.SearchAsync(
                BuildVectorRangeQuery(searchValue, vectorProperty, VectorScoreTranslation.ToRangeRadius(vectorProperty, threshold), skip, top, filter),
                cancellationToken).ConfigureAwait(false)
            : await _index.SearchAsync(
                BuildVectorQuery(searchValue, vectorProperty, skip + top, skip, top, filter),
                cancellationToken).ConfigureAwait(false);

        foreach (var document in results.Documents)
        {
            var record = await MaterializeAsync(document, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                continue;
            }

            if (!options.IncludeVectors)
            {
                ClearVectors(record);
            }

            double? score = document.TryGetValue(ScoreAlias, out var rawScore)
                && double.TryParse(rawScore.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedScore)
                ? VectorScoreTranslation.ToScore(vectorProperty, parsedScore)
                : null;

            yield return new VectorSearchResult<TRecord>(record, score);
        }
    }

    /// <inheritdoc/>
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceKey is null)
        {
            if (serviceType.IsInstanceOfType(this))
            {
                return this;
            }

            if (serviceType == typeof(SearchIndex))
            {
                return _index;
            }

            if (serviceType == typeof(IDatabase))
            {
                return _database;
            }
        }

        return null;
    }

    private VectorQuery BuildVectorQuery(
        object searchValue,
        RedisVLProperty vectorProperty,
        int window,
        int skip,
        int top,
        RedisVL.Filters.FilterExpression? filter)
    {
        var pagination = new QueryPagination(offset: skip, limit: top);
        var returnFields = new[] { JsonRootField };

        return vectorProperty.DataType == VectorDataType.Float64
            ? VectorQuery.FromFloat64(vectorProperty.JsonName, ToDoubleArray(searchValue), window, filter, returnFields, ScoreAlias, pagination: pagination)
            : VectorQuery.FromFloat32(vectorProperty.JsonName, ToFloatArray(searchValue), window, filter, returnFields, ScoreAlias, pagination: pagination);
    }

    private VectorRangeQuery BuildVectorRangeQuery(
        object searchValue,
        RedisVLProperty vectorProperty,
        double distanceRadius,
        int skip,
        int top,
        RedisVL.Filters.FilterExpression? filter)
    {
        var pagination = new QueryPagination(offset: skip, limit: top);
        var returnFields = new[] { JsonRootField };

        return vectorProperty.DataType == VectorDataType.Float64
            ? VectorRangeQuery.FromFloat64(vectorProperty.JsonName, ToDoubleArray(searchValue), distanceRadius, filter, returnFields, ScoreAlias, pagination: pagination)
            : VectorRangeQuery.FromFloat32(vectorProperty.JsonName, ToFloatArray(searchValue), distanceRadius, filter, returnFields, ScoreAlias, pagination: pagination);
    }

    private async Task<TRecord?> MaterializeAsync(SearchDocument document, CancellationToken cancellationToken)
    {
        if (document.TryGetValue(JsonRootField, out var rawJson) && !rawJson.IsNullOrEmpty)
        {
            return JsonSerializer.Deserialize<TRecord>(rawJson.ToString()!, _serializerOptions);
        }

        return await _index.FetchJsonByKeyAsync<TRecord>(document.Id, cancellationToken).ConfigureAwait(false);
    }

    // Rejects options the RedisVL connector cannot honor. Per MEVD connector convention this throws
    // rather than silently dropping the option — silently ignoring OldFilter, for example, would run
    // an unfiltered search and leak records the caller intended to exclude.
    private static void ValidateSearchOptions(VectorSearchOptions<TRecord> options)
    {
#pragma warning disable CS0618 // OldFilter is obsolete; we still must detect and reject it.
        if (options.OldFilter is not null)
        {
            throw new NotSupportedException(
                "VectorSearchOptions.OldFilter is not supported by the RedisVL connector; use Filter instead.");
        }
#pragma warning restore CS0618
    }

    // Maps FilteredRecordRetrievalOptions.OrderBy onto an FT.SEARCH SORTBY. FT.SEARCH sorts by a
    // single field, so a multi-key OrderBy is rejected (aggregation would be required to honor it).
    private SearchSortBy? ResolveSortBy(FilteredRecordRetrievalOptions<TRecord>? options)
    {
        var orderBy = options?.OrderBy;
        if (orderBy is null)
        {
            return null;
        }

        var sortKeys = orderBy(new()).Values;
        if (sortKeys.Count == 0)
        {
            return null;
        }

        if (sortKeys.Count > 1)
        {
            throw new NotSupportedException(
                "The RedisVL connector supports ordering by a single property; FT.SEARCH accepts one SORTBY field.");
        }

        var sortKey = sortKeys[0];
        var property = ResolveSortProperty(sortKey.PropertySelector);
        return new SearchSortBy(property.JsonName, descending: !sortKey.Ascending);
    }

    private RedisVLProperty ResolveSortProperty(Expression<Func<TRecord, object?>> selector)
    {
        var body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
        {
            body = convert.Operand;
        }

        if (body is MemberExpression { Member: PropertyInfo propertyInfo }
            && _model.ByClrName.TryGetValue(propertyInfo.Name, out var property))
        {
            if (property.Kind is RedisVLFieldKind.Tag or RedisVLFieldKind.Text or RedisVLFieldKind.Numeric)
            {
                return property;
            }

            throw new NotSupportedException(
                $"Ordering by property '{propertyInfo.Name}' is not supported; only indexed data fields can be sorted.");
        }

        throw new NotSupportedException("OrderBy must reference an indexed record property.");
    }

    // Resets vector properties to their default so a record materialized from the full JSON document
    // does not carry vectors the caller asked to omit (IncludeVectors == false, the MEVD default).
    private void ClearVectors(TRecord record)
    {
        foreach (var vector in _model.Vectors)
        {
            var property = vector.Property;
            if (!property.CanWrite)
            {
                continue;
            }

            var reset = property.PropertyType.IsValueType
                ? Activator.CreateInstance(property.PropertyType)
                : null;
            property.SetValue(record, reset);
        }
    }

    private string ToRedisKey(TKey key) => $"{_keyPrefix}{key}";

    private TKey GetRecordKey(TRecord record)
    {
        var value = _model.Key.Property.GetValue(record)
            ?? throw new InvalidOperationException("The record key property value cannot be null.");
        return (TKey)value;
    }

    private string? GetVectorPropertyName(Expression<Func<TRecord, object?>>? selector)
    {
        if (selector is null)
        {
            return null;
        }

        var body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } convert)
        {
            body = convert.Operand;
        }

        return body is MemberExpression member ? member.Member.Name : null;
    }

    private static float[] ToFloatArray(object value) =>
        value switch
        {
            float[] array => array,
            ReadOnlyMemory<float> memory => memory.ToArray(),
            Memory<float> memory => memory.ToArray(),
            Embedding<float> embedding => embedding.Vector.ToArray(),
            double[] array => Array.ConvertAll(array, static v => (float)v),
            ReadOnlyMemory<double> memory => MemoryMarshalToFloat(memory.Span),
            _ => throw new NotSupportedException(
                $"Search input type '{value.GetType().Name}' is not supported. Use ReadOnlyMemory<float>, float[], or Embedding<float>."),
        };

    private static double[] ToDoubleArray(object value) =>
        value switch
        {
            double[] array => array,
            ReadOnlyMemory<double> memory => memory.ToArray(),
            Memory<double> memory => memory.ToArray(),
            float[] array => Array.ConvertAll(array, static v => (double)v),
            ReadOnlyMemory<float> memory => MemoryMarshalToDouble(memory.Span),
            Embedding<float> embedding => MemoryMarshalToDouble(embedding.Vector.Span),
            _ => throw new NotSupportedException(
                $"Search input type '{value.GetType().Name}' is not supported. Use ReadOnlyMemory<double> or double[]."),
        };

    private static float[] MemoryMarshalToFloat(ReadOnlySpan<double> span)
    {
        var result = new float[span.Length];
        for (var i = 0; i < span.Length; i++)
        {
            result[i] = (float)span[i];
        }

        return result;
    }

    private static double[] MemoryMarshalToDouble(ReadOnlySpan<float> span)
    {
        var result = new double[span.Length];
        for (var i = 0; i < span.Length; i++)
        {
            result[i] = span[i];
        }

        return result;
    }
}

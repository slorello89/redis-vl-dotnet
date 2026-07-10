using System.Linq.Expressions;
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

    public override string Name { get; }

    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default) =>
        _index.ExistsAsync(cancellationToken);

    public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default) =>
        _index.CreateAsync(new CreateIndexOptions(skipIfExists: true), cancellationToken);

    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        if (await _index.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await _index.DropAsync(deleteDocuments: true, cancellationToken).ConfigureAwait(false);
        }
    }

    public override Task<TRecord?> GetAsync(
        TKey key,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _index.FetchJsonByKeyAsync<TRecord>(ToRedisKey(key), cancellationToken);
    }

    public override async IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);

        var skip = options?.Skip ?? 0;
        var query = new FilterQuery(
            _filterTranslator.Translate(filter),
            returnFields: [JsonRootField],
            pagination: new QueryPagination(offset: skip, limit: top));

        var results = await _index.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        foreach (var document in results.Documents)
        {
            var record = await MaterializeAsync(document, cancellationToken).ConfigureAwait(false);
            if (record is not null)
            {
                yield return record;
            }
        }
    }

    public override async Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _index.LoadJsonAsync(record, key: ToRedisKey(GetRecordKey(record)), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

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

    public override Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _index.DeleteJsonByKeyAsync(ToRedisKey(key), cancellationToken);
    }

    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);

        options ??= new VectorSearchOptions<TRecord>();
        var vectorProperty = _model.ResolveVector(GetVectorPropertyName(options.VectorProperty));
        var skip = options.Skip;
        var window = skip + top;

        var filter = options.Filter is null ? null : _filterTranslator.Translate(options.Filter);
        var query = BuildVectorQuery(searchValue, vectorProperty, window, skip, top, filter);

        var results = await _index.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        foreach (var document in results.Documents)
        {
            var record = await MaterializeAsync(document, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                continue;
            }

            double? score = document.TryGetValue(ScoreAlias, out var rawScore)
                && double.TryParse(rawScore.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedScore)
                ? parsedScore
                : null;

            yield return new VectorSearchResult<TRecord>(record, score);
        }
    }

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

    private async Task<TRecord?> MaterializeAsync(SearchDocument document, CancellationToken cancellationToken)
    {
        if (document.TryGetValue(JsonRootField, out var rawJson) && !rawJson.IsNullOrEmpty)
        {
            return JsonSerializer.Deserialize<TRecord>(rawJson.ToString()!, _serializerOptions);
        }

        return await _index.FetchJsonByKeyAsync<TRecord>(document.Id, cancellationToken).ConfigureAwait(false);
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

using System.Collections.ObjectModel;
using System.Globalization;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Cli;

public sealed class RedisVlCliService : IRedisVlCliService
{
    public async Task<bool> CreateIndexAsync(
        string redisConnectionString,
        CreateIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString).ConfigureAwait(false);
        var database = connection.GetDatabase();
        var index = new SearchIndex(database, request.Schema);

        return await index.CreateAsync(
            new CreateIndexOptions(
                overwrite: request.Overwrite,
                dropExistingDocuments: request.DropDocuments,
                skipIfExists: request.SkipIfExists),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListIndexesAsync(
        string redisConnectionString,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString).ConfigureAwait(false);
        var database = connection.GetDatabase();
        var indexes = await SearchIndex.ListAsync(database, cancellationToken).ConfigureAwait(false);
        return new ReadOnlyCollection<string>(indexes.Select(static item => item.Name).ToArray());
    }

    public async Task<IndexInfoView> GetIndexInfoAsync(
        string redisConnectionString,
        string indexName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString).ConfigureAwait(false);
        var database = connection.GetDatabase();
        var index = await SearchIndex.FromExistingAsync(database, indexName, cancellationToken).ConfigureAwait(false);
        var info = await index.InfoAsync(cancellationToken).ConfigureAwait(false);

        return new IndexInfoView(
            index.Schema.Index.Name,
            index.Schema.Index.StorageType.ToString(),
            index.Schema.Index.Prefixes.ToArray(),
            index.Schema.Index.KeySeparator.ToString(CultureInfo.InvariantCulture),
            index.Schema.Index.Stopwords?.ToArray(),
            TryReadDocumentCount(info),
            index.Schema.Fields.Select(ToFieldView).ToArray(),
            BuildScalarAttributes(info));
    }

    public async Task<long> ClearIndexAsync(
        string redisConnectionString,
        string indexName,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString).ConfigureAwait(false);
        var database = connection.GetDatabase();
        var index = await SearchIndex.FromExistingAsync(database, indexName, cancellationToken).ConfigureAwait(false);
        return await index.ClearAsync(batchSize, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteIndexAsync(
        string redisConnectionString,
        string indexName,
        bool dropDocuments,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString).ConfigureAwait(false);
        var database = connection.GetDatabase();
        var index = await SearchIndex.FromExistingAsync(database, indexName, cancellationToken).ConfigureAwait(false);
        await index.DropAsync(dropDocuments, cancellationToken).ConfigureAwait(false);
    }

    public Task<SearchSchema> LoadSchemaAsync(string schemaFilePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SearchSchema.FromYamlFile(schemaFilePath));
    }

    private static IndexFieldView ToFieldView(FieldDefinition field) =>
        new(
            field switch
            {
                TextFieldDefinition => "text",
                TagFieldDefinition => "tag",
                NumericFieldDefinition => "numeric",
                GeoFieldDefinition => "geo",
                VectorFieldDefinition => "vector",
                _ => field.GetType().Name
            },
            field.Name,
            field.Alias,
            field.Sortable);

    private static IReadOnlyDictionary<string, string> BuildScalarAttributes(SearchIndexInfo info)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in info.Attributes)
        {
            if (attribute.Value.IsNull || attribute.Value.Resp2Type == ResultType.Array)
            {
                continue;
            }

            var value = attribute.Value.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                attributes[attribute.Key] = value!;
            }
        }

        return new ReadOnlyDictionary<string, string>(attributes);
    }

    private static long? TryReadDocumentCount(SearchIndexInfo info)
    {
        var value = info.GetString("num_docs");
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return Convert.ToInt64(Math.Truncate(parsed), CultureInfo.InvariantCulture);
        }

        return null;
    }
}

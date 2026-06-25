using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using RedisVL.Indexes;
using StackExchange.Redis;

namespace RedisVL.Connectors.VectorData;

/// <summary>
/// A Microsoft.Extensions.VectorData <see cref="VectorStore"/> backed by Redis (via RedisVL).
/// Hand it a StackExchange.Redis <see cref="IDatabase"/> and request strongly-typed collections.
/// </summary>
public sealed class RedisVLVectorStore : VectorStore
{
    private readonly IDatabase _database;

    public RedisVLVectorStore(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string name,
        VectorStoreCollectionDefinition? definition = null) =>
        new RedisVLCollection<TKey, TRecord>(
            _database,
            name,
            new RedisVLCollectionOptions { Definition = definition });

    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string name,
        VectorStoreCollectionDefinition definition) =>
        throw new NotSupportedException(
            "The RedisVL vector-data connector does not support dynamic (Dictionary-based) collections. Use GetCollection<TKey, TRecord>.");

    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var indexes = await SearchIndex.ListAsync(_database, cancellationToken).ConfigureAwait(false);
        foreach (var index in indexes)
        {
            yield return index.Name;
        }
    }

    public override async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var indexes = await SearchIndex.ListAsync(_database, cancellationToken).ConfigureAwait(false);
        return indexes.Any(index => string.Equals(index.Name, name, StringComparison.Ordinal));
    }

    public override async Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!await CollectionExistsAsync(name, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var index = await SearchIndex.FromExistingAsync(_database, name, cancellationToken).ConfigureAwait(false);
        await index.DropAsync(deleteDocuments: true, cancellationToken).ConfigureAwait(false);
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

            if (serviceType == typeof(IDatabase))
            {
                return _database;
            }
        }

        return null;
    }
}

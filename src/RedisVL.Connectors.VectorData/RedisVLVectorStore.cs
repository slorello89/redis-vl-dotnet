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

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVLVectorStore"/> class.
    /// </summary>
    /// <param name="database">The StackExchange.Redis database backing the store.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> is <c>null</c>.</exception>
    public RedisVLVectorStore(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <inheritdoc/>
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string name,
        VectorStoreCollectionDefinition? definition = null) =>
        new RedisVLCollection<TKey, TRecord>(
            _database,
            name,
            new RedisVLCollectionOptions { Definition = definition });

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always thrown; dynamic collections are not supported.</exception>
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string name,
        VectorStoreCollectionDefinition definition) =>
        throw new NotSupportedException(
            "The RedisVL vector-data connector does not support dynamic (Dictionary-based) collections. Use GetCollection<TKey, TRecord>.");

    /// <inheritdoc/>
    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var indexes = await SearchIndex.ListAsync(_database, cancellationToken).ConfigureAwait(false);
        foreach (var index in indexes)
        {
            yield return index.Name;
        }
    }

    /// <inheritdoc/>
    public override async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var indexes = await SearchIndex.ListAsync(_database, cancellationToken).ConfigureAwait(false);
        return indexes.Any(index => string.Equals(index.Name, name, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
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

            if (serviceType == typeof(IDatabase))
            {
                return _database;
            }
        }

        return null;
    }
}

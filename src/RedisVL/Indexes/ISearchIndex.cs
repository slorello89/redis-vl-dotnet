using RedisVL.Queries;
using RedisVL.Schema;
using System.Text.Json;

namespace RedisVL.Indexes;

/// <summary>
/// Abstraction over a Redis search index, mirroring the public surface of <see cref="SearchIndex" />.
/// Depend on this interface (rather than the concrete <see cref="SearchIndex" />) where a type needs to
/// be substituted in unit tests. The static factory methods <see cref="SearchIndex.FromExistingAsync" />
/// and <see cref="SearchIndex.ListAsync" /> are not part of the instance contract and remain on
/// <see cref="SearchIndex" />.
/// </summary>
public interface ISearchIndex
{
    /// <summary>Gets the schema this index was created from.</summary>
    SearchSchema Schema { get; }

    /// <summary>Creates the index in Redis, honoring the supplied creation options.</summary>
    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Determines whether the index already exists in Redis.</summary>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>Retrieves metadata about the index from Redis.</summary>
    Task<SearchIndexInfo> InfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the index, optionally deleting the documents it indexes.</summary>
    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    /// <summary>Deletes every document under the index's key prefixes, in batches.</summary>
    Task<long> ClearAsync(int batchSize = 1000, CancellationToken cancellationToken = default);

    /// <summary>Stores a single document as JSON and returns the key it was written to.</summary>
    Task<string> LoadJsonAsync<TDocument>(
        TDocument document,
        string? key = null,
        string? id = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stores multiple documents as JSON and returns the keys they were written to, aligned to input order.</summary>
    Task<IReadOnlyList<string>> LoadJsonAsync<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches a JSON document by its full Redis key, or <see langword="null" /> when absent.</summary>
    Task<TDocument?> FetchJsonByKeyAsync<TDocument>(string key, CancellationToken cancellationToken = default);

    /// <summary>Fetches a JSON document by its id (resolved to a key via the schema), or <see langword="null" /> when absent.</summary>
    Task<TDocument?> FetchJsonByIdAsync<TDocument>(string id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a JSON document by its full Redis key.</summary>
    Task<bool> DeleteJsonByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes a JSON document by its id (resolved to a key via the schema).</summary>
    Task<bool> DeleteJsonByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Applies partial JSON updates to an existing document identified by its full Redis key.</summary>
    Task<bool> UpdateJsonByKeyAsync(
        string key,
        IEnumerable<JsonPartialUpdate> updates,
        CancellationToken cancellationToken = default);

    /// <summary>Applies partial JSON updates to an existing document identified by its id.</summary>
    Task<bool> UpdateJsonByIdAsync(
        string id,
        IEnumerable<JsonPartialUpdate> updates,
        CancellationToken cancellationToken = default);

    /// <summary>Stores a single document as a hash and returns the key it was written to.</summary>
    Task<string> LoadHashAsync<TDocument>(
        TDocument document,
        string? key = null,
        string? id = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stores multiple documents as hashes and returns the keys they were written to, aligned to input order.</summary>
    Task<IReadOnlyList<string>> LoadHashAsync<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches a hash document by its full Redis key, or <see langword="null" /> when absent.</summary>
    Task<TDocument?> FetchHashByKeyAsync<TDocument>(string key, CancellationToken cancellationToken = default);

    /// <summary>Fetches a hash document by its id (resolved to a key via the schema), or <see langword="null" /> when absent.</summary>
    Task<TDocument?> FetchHashByIdAsync<TDocument>(string id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a hash document by its full Redis key.</summary>
    Task<bool> DeleteHashByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes a hash document by its id (resolved to a key via the schema).</summary>
    Task<bool> DeleteHashByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Applies partial hash-field updates to an existing document identified by its full Redis key.</summary>
    Task<bool> UpdateHashByKeyAsync(
        string key,
        IEnumerable<HashPartialUpdate> updates,
        CancellationToken cancellationToken = default);

    /// <summary>Applies partial hash-field updates to an existing document identified by its id.</summary>
    Task<bool> UpdateHashByIdAsync(
        string id,
        IEnumerable<HashPartialUpdate> updates,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a KNN vector search and returns the raw results.</summary>
    Task<SearchResults> SearchAsync(VectorQuery query, CancellationToken cancellationToken = default);

    /// <summary>Runs a multi-vector search and returns the combined raw results.</summary>
    Task<SearchResults> SearchAsync(MultiVectorQuery query, CancellationToken cancellationToken = default);

    /// <summary>Runs a KNN vector search and maps each result document to <typeparamref name="TDocument" />.</summary>
    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        VectorQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a multi-vector search and maps each combined result document to <typeparamref name="TDocument" />.</summary>
    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        MultiVectorQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a client-combined hybrid (text + vector) search and returns the raw results.</summary>
    Task<SearchResults> SearchAsync(HybridQuery query, CancellationToken cancellationToken = default);

    /// <summary>Runs a client-combined hybrid search and maps each result document to <typeparamref name="TDocument" />.</summary>
    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        HybridQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a server-side native hybrid (<c>FT.HYBRID</c>) search and returns the raw results.</summary>
    Task<SearchResults> SearchAsync(HybridSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>Runs a server-side native hybrid search and maps each result document to <typeparamref name="TDocument" />.</summary>
    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        HybridSearchQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a vector range search and returns the raw results.</summary>
    Task<SearchResults> SearchAsync(VectorRangeQuery query, CancellationToken cancellationToken = default);

    /// <summary>Runs a vector range search and maps each result document to <typeparamref name="TDocument" />.</summary>
    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        VectorRangeQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a filter-only search and returns the raw results.</summary>
    Task<SearchResults> SearchAsync(FilterQuery query, CancellationToken cancellationToken = default);

    /// <summary>Runs a full-text search and returns the raw results.</summary>
    Task<SearchResults> SearchAsync(TextQuery query, CancellationToken cancellationToken = default);

    /// <summary>Runs a filter-only search and maps each result document to <typeparamref name="TDocument" />.</summary>
    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        FilterQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a full-text search and maps each result document to <typeparamref name="TDocument" />.</summary>
    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        TextQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs an aggregation pipeline and returns the raw results.</summary>
    Task<AggregationResults> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken = default);

    /// <summary>Runs an aggregation pipeline and maps each result row to <typeparamref name="TDocument" />.</summary>
    Task<AggregationResults<TDocument>> AggregateAsync<TDocument>(
        AggregationQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a native hybrid aggregation pipeline and returns the raw results.</summary>
    Task<AggregationResults> AggregateAsync(AggregateHybridQuery query, CancellationToken cancellationToken = default);

    /// <summary>Runs a native hybrid aggregation pipeline and maps each result row to <typeparamref name="TDocument" />.</summary>
    Task<AggregationResults<TDocument>> AggregateAsync<TDocument>(
        AggregateHybridQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a filter-only search page by page.</summary>
    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        FilterQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a filter-only search page by page, mapping each document to <typeparamref name="TDocument" />.</summary>
    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        FilterQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a full-text search page by page.</summary>
    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        TextQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a full-text search page by page, mapping each document to <typeparamref name="TDocument" />.</summary>
    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        TextQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a KNN vector search page by page.</summary>
    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        VectorQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a KNN vector search page by page, mapping each document to <typeparamref name="TDocument" />.</summary>
    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        VectorQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a client-combined hybrid search page by page.</summary>
    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        HybridQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a client-combined hybrid search page by page, mapping each document to <typeparamref name="TDocument" />.</summary>
    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        HybridQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a vector range search page by page.</summary>
    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        VectorRangeQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a vector range search page by page, mapping each document to <typeparamref name="TDocument" />.</summary>
    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        VectorRangeQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a multi-vector search page by page.</summary>
    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        MultiVectorQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a multi-vector search page by page, mapping each document to <typeparamref name="TDocument" />.</summary>
    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        MultiVectorQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams an aggregation pipeline page by page.</summary>
    IAsyncEnumerable<AggregationResults> AggregateBatchesAsync(
        AggregationQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams an aggregation pipeline page by page, mapping each row to <typeparamref name="TDocument" />.</summary>
    IAsyncEnumerable<AggregationResults<TDocument>> AggregateBatchesAsync<TDocument>(
        AggregationQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a native hybrid aggregation pipeline page by page.</summary>
    IAsyncEnumerable<AggregationResults> AggregateBatchesAsync(
        AggregateHybridQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a native hybrid aggregation pipeline page by page, mapping each row to <typeparamref name="TDocument" />.</summary>
    IAsyncEnumerable<AggregationResults<TDocument>> AggregateBatchesAsync<TDocument>(
        AggregateHybridQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the number of documents matching the query without returning the documents themselves.</summary>
    Task<long> CountAsync(CountQuery query, CancellationToken cancellationToken = default);
}

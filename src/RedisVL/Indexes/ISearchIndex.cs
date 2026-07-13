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

    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    Task<SearchIndexInfo> InfoAsync(CancellationToken cancellationToken = default);

    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    Task<long> ClearAsync(int batchSize = 1000, CancellationToken cancellationToken = default);

    Task<string> LoadJsonAsync<TDocument>(
        TDocument document,
        string? key = null,
        string? id = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> LoadJsonAsync<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null,
        CancellationToken cancellationToken = default);

    Task<TDocument?> FetchJsonByKeyAsync<TDocument>(string key, CancellationToken cancellationToken = default);

    Task<TDocument?> FetchJsonByIdAsync<TDocument>(string id, CancellationToken cancellationToken = default);

    Task<bool> DeleteJsonByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> DeleteJsonByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> UpdateJsonByKeyAsync(
        string key,
        IEnumerable<JsonPartialUpdate> updates,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateJsonByIdAsync(
        string id,
        IEnumerable<JsonPartialUpdate> updates,
        CancellationToken cancellationToken = default);

    Task<string> LoadHashAsync<TDocument>(
        TDocument document,
        string? key = null,
        string? id = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> LoadHashAsync<TDocument>(
        IEnumerable<TDocument> documents,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null,
        CancellationToken cancellationToken = default);

    Task<TDocument?> FetchHashByKeyAsync<TDocument>(string key, CancellationToken cancellationToken = default);

    Task<TDocument?> FetchHashByIdAsync<TDocument>(string id, CancellationToken cancellationToken = default);

    Task<bool> DeleteHashByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> DeleteHashByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> UpdateHashByKeyAsync(
        string key,
        IEnumerable<HashPartialUpdate> updates,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateHashByIdAsync(
        string id,
        IEnumerable<HashPartialUpdate> updates,
        CancellationToken cancellationToken = default);

    Task<SearchResults> SearchAsync(VectorQuery query, CancellationToken cancellationToken = default);

    Task<SearchResults> SearchAsync(MultiVectorQuery query, CancellationToken cancellationToken = default);

    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        VectorQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        MultiVectorQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    Task<SearchResults> SearchAsync(HybridQuery query, CancellationToken cancellationToken = default);

    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        HybridQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    Task<SearchResults> SearchAsync(HybridSearchQuery query, CancellationToken cancellationToken = default);

    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        HybridSearchQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    Task<SearchResults> SearchAsync(VectorRangeQuery query, CancellationToken cancellationToken = default);

    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        VectorRangeQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    Task<SearchResults> SearchAsync(FilterQuery query, CancellationToken cancellationToken = default);

    Task<SearchResults> SearchAsync(TextQuery query, CancellationToken cancellationToken = default);

    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        FilterQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    Task<SearchResults<TDocument>> SearchAsync<TDocument>(
        TextQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    Task<AggregationResults> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken = default);

    Task<AggregationResults<TDocument>> AggregateAsync<TDocument>(
        AggregationQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    Task<AggregationResults> AggregateAsync(AggregateHybridQuery query, CancellationToken cancellationToken = default);

    Task<AggregationResults<TDocument>> AggregateAsync<TDocument>(
        AggregateHybridQuery query,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        FilterQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        FilterQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        TextQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        TextQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        VectorQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        VectorQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        HybridQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        HybridQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        VectorRangeQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        VectorRangeQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults> SearchBatchesAsync(
        MultiVectorQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchResults<TDocument>> SearchBatchesAsync<TDocument>(
        MultiVectorQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AggregationResults> AggregateBatchesAsync(
        AggregationQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AggregationResults<TDocument>> AggregateBatchesAsync<TDocument>(
        AggregationQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AggregationResults> AggregateBatchesAsync(
        AggregateHybridQuery query,
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AggregationResults<TDocument>> AggregateBatchesAsync<TDocument>(
        AggregateHybridQuery query,
        int? batchSize = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default);

    Task<long> CountAsync(CountQuery query, CancellationToken cancellationToken = default);
}

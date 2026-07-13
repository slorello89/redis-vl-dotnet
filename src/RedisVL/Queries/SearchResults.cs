using StackExchange.Redis;
using System.Text.Json;

namespace RedisVL.Queries;

/// <summary>
/// The untyped result of a search query: the total number of matching documents together with the page of
/// <see cref="SearchDocument"/> instances that were returned.
/// </summary>
public sealed class SearchResults
{
    /// <summary>
    /// Initializes a new <see cref="SearchResults"/>.
    /// </summary>
    /// <param name="totalCount">The total number of documents matching the query; cannot be negative.</param>
    /// <param name="documents">The documents returned for the current page.</param>
    /// <param name="warnings">Server-emitted warnings for this query, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="totalCount"/> is negative.</exception>
    public SearchResults(long totalCount, IReadOnlyList<SearchDocument> documents, IReadOnlyList<string>? warnings = null)
    {
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "Total count cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(documents);
        TotalCount = totalCount;
        Documents = documents;
        Warnings = warnings ?? [];
    }

    /// <summary>The total number of documents matching the query, which may exceed the number returned.</summary>
    public long TotalCount { get; }

    /// <summary>The documents returned for the current page.</summary>
    public IReadOnlyList<SearchDocument> Documents { get; }

    /// <summary>
    /// Warnings the server emitted for this query (for example when <c>FT.HYBRID</c> degrades a branch);
    /// empty when the server reported none.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Projects each returned document onto <typeparamref name="TDocument"/>.
    /// </summary>
    /// <typeparam name="TDocument">The target type each document is materialized into.</typeparam>
    /// <param name="serializerOptions">Optional JSON serializer options controlling the mapping.</param>
    /// <returns>A strongly typed <see cref="SearchResults{TDocument}"/> preserving the total count.</returns>
    public SearchResults<TDocument> Map<TDocument>(JsonSerializerOptions? serializerOptions = null)
    {
        var mappedDocuments = Documents.Select(document => document.Map<TDocument>(serializerOptions)).ToArray();
        return new SearchResults<TDocument>(TotalCount, mappedDocuments, Warnings);
    }
}

/// <summary>
/// A single document returned by a search query, exposing its key and the raw field values as
/// <see cref="RedisValue"/> instances.
/// </summary>
public sealed class SearchDocument
{
    /// <summary>
    /// Initializes a new <see cref="SearchDocument"/>.
    /// </summary>
    /// <param name="id">The document key; must be non-empty.</param>
    /// <param name="values">The raw field values keyed by field name.</param>
    public SearchDocument(string id, IReadOnlyDictionary<string, RedisValue> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(values);

        Id = id.Trim();
        Values = values;
    }

    /// <summary>The document key.</summary>
    public string Id { get; }

    /// <summary>The raw field values keyed by field name.</summary>
    public IReadOnlyDictionary<string, RedisValue> Values { get; }

    /// <summary>
    /// Materializes this document into <typeparamref name="TDocument"/>.
    /// </summary>
    /// <typeparam name="TDocument">The target type to materialize into.</typeparam>
    /// <param name="serializerOptions">Optional JSON serializer options controlling the mapping.</param>
    /// <returns>The mapped document instance.</returns>
    public TDocument Map<TDocument>(JsonSerializerOptions? serializerOptions = null) =>
        SearchResultMapper.Map<TDocument>(this, serializerOptions);

    /// <summary>
    /// Attempts to retrieve the raw value of a field by name.
    /// </summary>
    /// <param name="fieldName">The field name to look up; must be non-empty.</param>
    /// <param name="value">When this method returns, the field value if found; otherwise the default.</param>
    /// <returns><see langword="true"/> if the field was present; otherwise <see langword="false"/>.</returns>
    public bool TryGetValue(string fieldName, out RedisValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        return Values.TryGetValue(fieldName.Trim(), out value);
    }
}

/// <summary>
/// The strongly typed result of a search query: the total number of matching documents together with the
/// page of <typeparamref name="TDocument"/> instances that were returned.
/// </summary>
/// <typeparam name="TDocument">The document type each result was materialized into.</typeparam>
public sealed class SearchResults<TDocument>
{
    /// <summary>
    /// Initializes a new <see cref="SearchResults{TDocument}"/>.
    /// </summary>
    /// <param name="totalCount">The total number of documents matching the query; cannot be negative.</param>
    /// <param name="documents">The mapped documents returned for the current page.</param>
    /// <param name="warnings">Server-emitted warnings for this query, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="totalCount"/> is negative.</exception>
    public SearchResults(long totalCount, IReadOnlyList<TDocument> documents, IReadOnlyList<string>? warnings = null)
    {
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "Total count cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(documents);
        TotalCount = totalCount;
        Documents = documents;
        Warnings = warnings ?? [];
    }

    /// <summary>The total number of documents matching the query, which may exceed the number returned.</summary>
    public long TotalCount { get; }

    /// <summary>The mapped documents returned for the current page.</summary>
    public IReadOnlyList<TDocument> Documents { get; }

    /// <summary>
    /// Warnings the server emitted for this query (for example when <c>FT.HYBRID</c> degrades a branch);
    /// empty when the server reported none.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }
}

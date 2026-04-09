using StackExchange.Redis;
using System.Text.Json;

namespace RedisVlDotNet.Queries;

public sealed class SearchResults
{
    public SearchResults(long totalCount, IReadOnlyList<SearchDocument> documents)
    {
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "Total count cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(documents);
        TotalCount = totalCount;
        Documents = documents;
    }

    public long TotalCount { get; }

    public IReadOnlyList<SearchDocument> Documents { get; }

    public SearchResults<TDocument> Map<TDocument>(JsonSerializerOptions? serializerOptions = null)
    {
        var mappedDocuments = Documents.Select(document => document.Map<TDocument>(serializerOptions)).ToArray();
        return new SearchResults<TDocument>(TotalCount, mappedDocuments);
    }
}

public sealed class SearchDocument
{
    public SearchDocument(string id, IReadOnlyDictionary<string, RedisValue> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(values);

        Id = id.Trim();
        Values = values;
    }

    public string Id { get; }

    public IReadOnlyDictionary<string, RedisValue> Values { get; }

    public TDocument Map<TDocument>(JsonSerializerOptions? serializerOptions = null) =>
        SearchResultMapper.Map<TDocument>(this, serializerOptions);

    public bool TryGetValue(string fieldName, out RedisValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        return Values.TryGetValue(fieldName.Trim(), out value);
    }
}

public sealed class SearchResults<TDocument>
{
    public SearchResults(long totalCount, IReadOnlyList<TDocument> documents)
    {
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "Total count cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(documents);
        TotalCount = totalCount;
        Documents = documents;
    }

    public long TotalCount { get; }

    public IReadOnlyList<TDocument> Documents { get; }
}

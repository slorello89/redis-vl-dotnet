using System.Runtime.InteropServices;
using RedisVL.Filters;

namespace RedisVL.Queries;

public sealed class HybridQuery
{
    public HybridQuery(
        FilterExpression textFilter,
        string vectorFieldName,
        byte[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null)
    {
        ArgumentNullException.ThrowIfNull(textFilter);
        ArgumentException.ThrowIfNullOrWhiteSpace(vectorFieldName);
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentException.ThrowIfNullOrWhiteSpace(scoreAlias);

        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector input must contain at least one byte.", nameof(vector));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "TopK must be greater than zero.");
        }

        if (!QueryFilterInspector.ContainsTextExpression(textFilter))
        {
            throw new ArgumentException("Hybrid queries require at least one text predicate in the text filter.", nameof(textFilter));
        }

        TextFilter = textFilter;
        VectorFieldName = FilterExpression.NormalizeFieldName(vectorFieldName);
        Vector = vector.ToArray();
        TopK = topK;
        Filter = filter;
        ScoreAlias = FilterExpression.NormalizeFieldName(scoreAlias);
        ReturnFields = QueryReturnFieldHelper.NormalizeReturnFields(returnFields, ScoreAlias);
        RuntimeOptions = runtimeOptions;
        Pagination = pagination ?? new QueryPagination(limit: topK);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ValidatePaginationWindow(TopK, Pagination, nameof(topK));
    }

    public FilterExpression TextFilter { get; }

    public string VectorFieldName { get; }

    public byte[] Vector { get; }

    public int TopK { get; }

    public int Offset { get; }

    public int Limit { get; }

    public FilterExpression? Filter { get; }

    public string ScoreAlias { get; }

    public IReadOnlyList<string> ReturnFields { get; }

    public VectorKnnRuntimeOptions? RuntimeOptions { get; }

    public QueryPagination Pagination { get; }

    internal FilterExpression CombinedFilter => Filter is null ? TextFilter : TextFilter & Filter;

    public static HybridQuery FromFloat32(
        FilterExpression textFilter,
        string vectorFieldName,
        float[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(textFilter, vectorFieldName, MemoryMarshal.AsBytes<float>(vector.AsSpan()).ToArray(), topK, filter, returnFields, scoreAlias, runtimeOptions, pagination);

    public static HybridQuery FromFloat64(
        FilterExpression textFilter,
        string vectorFieldName,
        double[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(textFilter, vectorFieldName, MemoryMarshal.AsBytes<double>(vector.AsSpan()).ToArray(), topK, filter, returnFields, scoreAlias, runtimeOptions, pagination);

    private static void ValidatePaginationWindow(int topK, QueryPagination pagination, string parameterName)
    {
        if (pagination.Offset + pagination.Limit > topK)
        {
            throw new ArgumentException(
                "Offset plus limit cannot exceed the vector retrieval window defined by topK.",
                parameterName);
        }
    }
}

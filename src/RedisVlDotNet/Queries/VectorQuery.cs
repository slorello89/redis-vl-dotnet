using System.Runtime.InteropServices;
using RedisVlDotNet.Filters;

namespace RedisVlDotNet.Queries;

public sealed class VectorQuery
{
    public VectorQuery(
        string fieldName,
        byte[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
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

        FieldName = FilterExpression.NormalizeFieldName(fieldName);
        Vector = vector.ToArray();
        TopK = topK;
        Filter = filter;
        ScoreAlias = FilterExpression.NormalizeFieldName(scoreAlias);
        ReturnFields = NormalizeReturnFields(returnFields, ScoreAlias);
        RuntimeOptions = runtimeOptions;
        Pagination = pagination ?? new QueryPagination(limit: topK);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ValidatePaginationWindow(TopK, Pagination, nameof(topK));
    }

    public string FieldName { get; }

    public byte[] Vector { get; }

    public int TopK { get; }

    public int Offset { get; }

    public int Limit { get; }

    public FilterExpression? Filter { get; }

    public string ScoreAlias { get; }

    public IReadOnlyList<string> ReturnFields { get; }

    public VectorKnnRuntimeOptions? RuntimeOptions { get; }

    public QueryPagination Pagination { get; }

    public static VectorQuery FromFloat32(
        string fieldName,
        float[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(fieldName, MemoryMarshal.AsBytes<float>(vector.AsSpan()).ToArray(), topK, filter, returnFields, scoreAlias, runtimeOptions, pagination);

    public static VectorQuery FromFloat64(
        string fieldName,
        double[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(fieldName, MemoryMarshal.AsBytes<double>(vector.AsSpan()).ToArray(), topK, filter, returnFields, scoreAlias, runtimeOptions, pagination);

    private static IReadOnlyList<string> NormalizeReturnFields(IEnumerable<string>? returnFields, string scoreAlias) =>
        QueryReturnFieldHelper.NormalizeReturnFields(returnFields, scoreAlias);

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

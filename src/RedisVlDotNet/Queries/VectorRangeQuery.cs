using System.Runtime.InteropServices;
using RedisVl.Filters;

namespace RedisVl.Queries;

public sealed class VectorRangeQuery
{
    public VectorRangeQuery(
        string fieldName,
        byte[] vector,
        double distanceThreshold,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        int offset = 0,
        int limit = 10,
        VectorRangeRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentException.ThrowIfNullOrWhiteSpace(scoreAlias);

        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector input must contain at least one byte.", nameof(vector));
        }

        if (distanceThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceThreshold), distanceThreshold, "Distance threshold must be greater than zero.");
        }

        FieldName = FilterExpression.NormalizeFieldName(fieldName);
        Vector = vector.ToArray();
        DistanceThreshold = distanceThreshold;
        Filter = filter;
        ScoreAlias = FilterExpression.NormalizeFieldName(scoreAlias);
        Pagination = pagination ?? new QueryPagination(offset, limit);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ReturnFields = QueryReturnFieldHelper.NormalizeReturnFields(returnFields, ScoreAlias);
        RuntimeOptions = runtimeOptions;
    }

    public string FieldName { get; }

    public byte[] Vector { get; }

    public double DistanceThreshold { get; }

    public FilterExpression? Filter { get; }

    public string ScoreAlias { get; }

    public int Offset { get; }

    public int Limit { get; }

    public QueryPagination Pagination { get; }

    public IReadOnlyList<string> ReturnFields { get; }

    public VectorRangeRuntimeOptions? RuntimeOptions { get; }

    public static VectorRangeQuery FromFloat32(
        string fieldName,
        float[] vector,
        double distanceThreshold,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        int offset = 0,
        int limit = 10,
        VectorRangeRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(fieldName, MemoryMarshal.AsBytes<float>(vector.AsSpan()).ToArray(), distanceThreshold, filter, returnFields, scoreAlias, offset, limit, runtimeOptions, pagination);

    public static VectorRangeQuery FromFloat64(
        string fieldName,
        double[] vector,
        double distanceThreshold,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        int offset = 0,
        int limit = 10,
        VectorRangeRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(fieldName, MemoryMarshal.AsBytes<double>(vector.AsSpan()).ToArray(), distanceThreshold, filter, returnFields, scoreAlias, offset, limit, runtimeOptions, pagination);
}

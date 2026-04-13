using System.Runtime.InteropServices;
using RedisVlDotNet.Filters;

namespace RedisVlDotNet.Queries;

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
        VectorKnnRuntimeOptions? runtimeOptions = null)
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
    }

    public FilterExpression TextFilter { get; }

    public string VectorFieldName { get; }

    public byte[] Vector { get; }

    public int TopK { get; }

    public FilterExpression? Filter { get; }

    public string ScoreAlias { get; }

    public IReadOnlyList<string> ReturnFields { get; }

    public VectorKnnRuntimeOptions? RuntimeOptions { get; }

    internal FilterExpression CombinedFilter => Filter is null ? TextFilter : TextFilter & Filter;

    public static HybridQuery FromFloat32(
        FilterExpression textFilter,
        string vectorFieldName,
        float[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null) =>
        new(textFilter, vectorFieldName, MemoryMarshal.AsBytes<float>(vector.AsSpan()).ToArray(), topK, filter, returnFields, scoreAlias, runtimeOptions);

    public static HybridQuery FromFloat64(
        FilterExpression textFilter,
        string vectorFieldName,
        double[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null) =>
        new(textFilter, vectorFieldName, MemoryMarshal.AsBytes<double>(vector.AsSpan()).ToArray(), topK, filter, returnFields, scoreAlias, runtimeOptions);
}

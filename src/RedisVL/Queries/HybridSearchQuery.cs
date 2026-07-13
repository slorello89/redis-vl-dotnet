using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A native hybrid search powered by the Redis <c>FT.HYBRID</c> command (Redis 8.4+).
/// </summary>
/// <remarks>
/// Unlike <see cref="HybridQuery" /> (which combines a text predicate and a KNN clause in a single
/// <c>FT.SEARCH</c> expression) and <see cref="AggregateHybridQuery" /> (which uses <c>FT.AGGREGATE</c>),
/// this query issues a real <c>FT.HYBRID</c> command. The text branch (<c>SEARCH</c>) and the vector
/// branch (<c>VSIM</c>) are scored independently and fused server-side via the configured
/// <see cref="Combination" /> (linear weighting or reciprocal rank fusion).
/// </remarks>
public sealed class HybridSearchQuery
{
    /// <summary>The result field that carries the source document key (the document id).</summary>
    public const string KeyField = "__key";

    /// <summary>The result field that carries the fused hybrid score.</summary>
    public const string ScoreField = "__score";

    /// <summary>
    /// Initializes a new instance of the <see cref="HybridSearchQuery" /> class.
    /// </summary>
    /// <param name="textQuery">The text predicate evaluated by the <c>SEARCH</c> branch.</param>
    /// <param name="vectorFieldName">The vector field evaluated by the <c>VSIM</c> branch.</param>
    /// <param name="vector">The query vector, encoded as bytes.</param>
    /// <param name="topK">The number of nearest neighbours requested from the vector branch (<c>KNN K</c>).</param>
    /// <param name="combination">The fusion strategy; <see langword="null" /> uses the server default (RRF).</param>
    /// <param name="vectorFilter">An optional pre-filter applied to the vector branch (<c>VSIM ... FILTER</c>).</param>
    /// <param name="returnFields">The fields to return in addition to the key and score.</param>
    /// <param name="runtimeOptions">Optional runtime tuning (e.g. <c>EF_RUNTIME</c> for HNSW fields).</param>
    /// <param name="pagination">The result window (<c>LIMIT</c>); defaults to the first <paramref name="topK" /> results.</param>
    public HybridSearchQuery(
        FilterExpression textQuery,
        string vectorFieldName,
        byte[] vector,
        int topK,
        HybridCombinationMethod? combination = null,
        FilterExpression? vectorFilter = null,
        IEnumerable<string>? returnFields = null,
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null)
    {
        ArgumentNullException.ThrowIfNull(textQuery);
        ArgumentException.ThrowIfNullOrWhiteSpace(vectorFieldName);
        ArgumentNullException.ThrowIfNull(vector);

        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector input must contain at least one byte.", nameof(vector));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "TopK must be greater than zero.");
        }

        if (!QueryFilterInspector.ContainsTextExpression(textQuery))
        {
            throw new ArgumentException("Hybrid search queries require at least one text predicate in the text query.", nameof(textQuery));
        }

        TextQuery = textQuery;
        VectorFieldName = FilterExpression.NormalizeFieldName(vectorFieldName);
        _vector = vector.ToArray();
        TopK = topK;
        Combination = combination;
        VectorFilter = vectorFilter;
        ReturnFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
        RuntimeOptions = runtimeOptions;
        Pagination = pagination ?? new QueryPagination(limit: topK);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
    }

    /// <summary>Gets the text predicate evaluated by the <c>SEARCH</c> branch.</summary>
    public FilterExpression TextQuery { get; }

    private readonly byte[] _vector;

    /// <summary>Gets the vector field evaluated by the <c>VSIM</c> branch.</summary>
    public string VectorFieldName { get; }

    /// <summary>Gets the query vector, encoded as bytes. Each read returns a fresh copy so the query's state cannot be mutated.</summary>
    public byte[] Vector => _vector.ToArray();

    /// <summary>Gets the number of nearest neighbours requested from the vector branch.</summary>
    public int TopK { get; }

    /// <summary>Gets the fusion strategy, or <see langword="null" /> to use the server default (RRF).</summary>
    public HybridCombinationMethod? Combination { get; }

    /// <summary>Gets the optional pre-filter applied to the vector branch.</summary>
    public FilterExpression? VectorFilter { get; }

    /// <summary>Gets the additional fields to return (the key and score are always returned).</summary>
    public IReadOnlyList<string> ReturnFields { get; }

    /// <summary>Gets the optional runtime tuning options.</summary>
    public VectorKnnRuntimeOptions? RuntimeOptions { get; }

    /// <summary>Gets the result window.</summary>
    public QueryPagination Pagination { get; }

    /// <summary>Gets the result window offset.</summary>
    public int Offset { get; }

    /// <summary>Gets the result window limit.</summary>
    public int Limit { get; }

    /// <summary>Creates a <see cref="HybridSearchQuery" /> from a 32-bit floating point vector.</summary>
    public static HybridSearchQuery FromFloat32(
        FilterExpression textQuery,
        string vectorFieldName,
        float[] vector,
        int topK,
        HybridCombinationMethod? combination = null,
        FilterExpression? vectorFilter = null,
        IEnumerable<string>? returnFields = null,
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(
            textQuery,
            vectorFieldName,
            VectorEncoding.ToBytes(vector),
            topK,
            combination,
            vectorFilter,
            returnFields,
            runtimeOptions,
            pagination);

    /// <summary>Creates a <see cref="HybridSearchQuery" /> from a 64-bit floating point vector.</summary>
    public static HybridSearchQuery FromFloat64(
        FilterExpression textQuery,
        string vectorFieldName,
        double[] vector,
        int topK,
        HybridCombinationMethod? combination = null,
        FilterExpression? vectorFilter = null,
        IEnumerable<string>? returnFields = null,
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(
            textQuery,
            vectorFieldName,
            VectorEncoding.ToBytes(vector),
            topK,
            combination,
            vectorFilter,
            returnFields,
            runtimeOptions,
            pagination);
}

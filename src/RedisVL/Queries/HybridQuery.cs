using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A hybrid query executed with <c>FT.SEARCH</c> that combines a required text predicate with a <c>KNN</c>
/// vector search, ranking documents that satisfy the text filter by their vector distance.
/// </summary>
public sealed class HybridQuery
{
    /// <summary>
    /// Initializes a new <see cref="HybridQuery"/>.
    /// </summary>
    /// <param name="textFilter">The text filter; it must contain at least one text predicate.</param>
    /// <param name="vectorFieldName">The name of the vector field searched with <c>KNN</c>.</param>
    /// <param name="vector">The raw query vector bytes; must be non-empty.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve; must be greater than zero.</param>
    /// <param name="filter">An optional additional filter combined with <paramref name="textFilter"/>.</param>
    /// <param name="returnFields">The fields to return for each match; when <see langword="null"/> all fields are returned.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window; defaults to a limit of <paramref name="topK"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="vector"/> is empty, <paramref name="textFilter"/> contains no text predicate, or the pagination window exceeds <paramref name="topK"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topK"/> is not greater than zero.</exception>
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
        _vector = vector.ToArray();
        TopK = topK;
        Filter = filter;
        ScoreAlias = FilterExpression.NormalizeFieldName(scoreAlias);

        // When the caller does not specify return fields, leave the set empty so the builder omits RETURN
        // entirely and the server returns every stored field (plus the yielded score alias produced by the
        // KNN `AS` clause, which SORTBY still targets). Emitting `RETURN 1 <scoreAlias>` here would make the
        // typed happy path throw, because the mapper treats every non-nullable property as required.
        HasExplicitReturnFields = returnFields is not null;
        ReturnFields = returnFields is null ? [] : QueryReturnFieldHelper.NormalizeReturnFields(returnFields, ScoreAlias);
        RuntimeOptions = runtimeOptions;
        Pagination = pagination ?? new QueryPagination(limit: topK);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ValidatePaginationWindow(TopK, Pagination, nameof(topK));
    }

    /// <summary>The text filter that supplies the required text predicate.</summary>
    public FilterExpression TextFilter { get; }

    private readonly byte[] _vector;

    /// <summary>The name of the vector field searched with <c>KNN</c>.</summary>
    public string VectorFieldName { get; }

    /// <summary>The raw query vector bytes. Each read returns a fresh copy so the query's state cannot be mutated.</summary>
    public byte[] Vector => _vector.ToArray();

    /// <summary>The number of nearest neighbors to retrieve.</summary>
    public int TopK { get; }

    /// <summary>The number of leading results to skip.</summary>
    public int Offset { get; }

    /// <summary>The maximum number of results to return.</summary>
    public int Limit { get; }

    /// <summary>An optional additional filter combined with <see cref="TextFilter"/>, or <see langword="null"/> for none.</summary>
    public FilterExpression? Filter { get; }

    /// <summary>The alias under which the vector distance is projected.</summary>
    public string ScoreAlias { get; }

    /// <summary>The fields returned for each matching document.</summary>
    public IReadOnlyList<string> ReturnFields { get; }

    /// <summary>
    /// Whether the caller explicitly supplied return fields. When <see langword="false"/> the return set was
    /// left unspecified and <see cref="ReturnFields"/> is empty, signalling the builder to omit RETURN so the
    /// server returns every stored field. Preserved through cloning so batched queries keep the same behavior.
    /// </summary>
    internal bool HasExplicitReturnFields { get; }

    /// <summary>Optional query-time index tuning parameters, or <see langword="null"/> to use index defaults.</summary>
    public VectorKnnRuntimeOptions? RuntimeOptions { get; }

    /// <summary>The pagination window applied to the results.</summary>
    public QueryPagination Pagination { get; }

    internal FilterExpression CombinedFilter => Filter is null ? TextFilter : TextFilter & Filter;

    /// <summary>Creates a hybrid query from a single-precision (<c>FLOAT32</c>) vector.</summary>
    /// <param name="textFilter">The text filter; it must contain at least one text predicate.</param>
    /// <param name="vectorFieldName">The name of the vector field searched with <c>KNN</c>.</param>
    /// <param name="vector">The query vector as 32-bit floats.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve.</param>
    /// <param name="filter">An optional additional filter combined with <paramref name="textFilter"/>.</param>
    /// <param name="returnFields">The fields to return for each match.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window.</param>
    /// <returns>A new <see cref="HybridQuery"/>.</returns>
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
        new(textFilter, vectorFieldName, VectorEncoding.ToBytes(vector), topK, filter, returnFields, scoreAlias, runtimeOptions, pagination);

    /// <summary>Creates a hybrid query from a double-precision (<c>FLOAT64</c>) vector.</summary>
    /// <param name="textFilter">The text filter; it must contain at least one text predicate.</param>
    /// <param name="vectorFieldName">The name of the vector field searched with <c>KNN</c>.</param>
    /// <param name="vector">The query vector as 64-bit floats.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve.</param>
    /// <param name="filter">An optional additional filter combined with <paramref name="textFilter"/>.</param>
    /// <param name="returnFields">The fields to return for each match.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window.</param>
    /// <returns>A new <see cref="HybridQuery"/>.</returns>
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
        new(textFilter, vectorFieldName, VectorEncoding.ToBytes(vector), topK, filter, returnFields, scoreAlias, runtimeOptions, pagination);

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

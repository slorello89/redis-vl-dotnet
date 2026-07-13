using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A hybrid text-and-vector query executed through the RediSearch aggregation pipeline (<c>FT.AGGREGATE</c>),
/// pairing a required text predicate with a <c>KNN</c> vector search and supporting <c>APPLY</c>,
/// <c>GROUPBY</c>, and <c>SORTBY</c> post-processing stages.
/// </summary>
public sealed class AggregateHybridQuery
{
    /// <summary>
    /// Initializes a new <see cref="AggregateHybridQuery"/>.
    /// </summary>
    /// <param name="textFilter">The text filter; it must contain at least one text predicate.</param>
    /// <param name="vectorFieldName">The name of the vector field searched with <c>KNN</c>.</param>
    /// <param name="vector">The raw query vector bytes; must be non-empty.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve; must be greater than zero.</param>
    /// <param name="filter">An optional additional filter combined with <paramref name="textFilter"/>.</param>
    /// <param name="loadFields">The fields to load into the aggregation pipeline.</param>
    /// <param name="applyClauses">Optional <c>APPLY</c> expressions evaluated per record.</param>
    /// <param name="groupBy">An optional <c>GROUPBY</c> stage with reducers.</param>
    /// <param name="sortBy">An optional <c>SORTBY</c> stage.</param>
    /// <param name="offset">The number of leading results to skip.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window; overrides <paramref name="offset"/> and <paramref name="limit"/> when supplied.</param>
    /// <exception cref="ArgumentException"><paramref name="vector"/> is empty, or <paramref name="textFilter"/> contains no text predicate.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topK"/> is not greater than zero.</exception>
    public AggregateHybridQuery(
        FilterExpression textFilter,
        string vectorFieldName,
        byte[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? loadFields = null,
        IEnumerable<AggregationApply>? applyClauses = null,
        AggregationGroupBy? groupBy = null,
        AggregationSortBy? sortBy = null,
        int offset = 0,
        int limit = 10,
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
            throw new ArgumentException("Aggregate hybrid queries require at least one text predicate in the text filter.", nameof(textFilter));
        }

        TextFilter = textFilter;
        VectorFieldName = FilterExpression.NormalizeFieldName(vectorFieldName);
        _vector = vector.ToArray();
        TopK = topK;
        Filter = filter;
        LoadFields = NormalizeFields(loadFields);
        ApplyClauses = applyClauses?.ToArray() ?? [];
        GroupBy = groupBy;
        SortBy = sortBy;
        Pagination = pagination ?? new QueryPagination(offset, limit);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ScoreAlias = FilterExpression.NormalizeFieldName(scoreAlias);
        RuntimeOptions = runtimeOptions;
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

    /// <summary>An optional additional filter combined with <see cref="TextFilter"/>, or <see langword="null"/> for none.</summary>
    public FilterExpression? Filter { get; }

    /// <summary>The fields loaded into the aggregation pipeline.</summary>
    public IReadOnlyList<string> LoadFields { get; }

    /// <summary>The <c>APPLY</c> expressions evaluated per record.</summary>
    public IReadOnlyList<AggregationApply> ApplyClauses { get; }

    /// <summary>The optional <c>GROUPBY</c> stage, or <see langword="null"/> when results are not grouped.</summary>
    public AggregationGroupBy? GroupBy { get; }

    /// <summary>The optional <c>SORTBY</c> stage, or <see langword="null"/> when results are unsorted.</summary>
    public AggregationSortBy? SortBy { get; }

    /// <summary>The number of leading results to skip.</summary>
    public int Offset { get; }

    /// <summary>The maximum number of results to return.</summary>
    public int Limit { get; }

    /// <summary>The pagination window applied to the results.</summary>
    public QueryPagination Pagination { get; }

    /// <summary>The alias under which the vector distance is projected.</summary>
    public string ScoreAlias { get; }

    /// <summary>Optional query-time index tuning parameters, or <see langword="null"/> to use index defaults.</summary>
    public VectorKnnRuntimeOptions? RuntimeOptions { get; }

    internal FilterExpression CombinedFilter => Filter is null ? TextFilter : TextFilter & Filter;

    /// <summary>Creates an aggregate hybrid query from a single-precision (<c>FLOAT32</c>) vector.</summary>
    /// <param name="textFilter">The text filter; it must contain at least one text predicate.</param>
    /// <param name="vectorFieldName">The name of the vector field searched with <c>KNN</c>.</param>
    /// <param name="vector">The query vector as 32-bit floats.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve.</param>
    /// <param name="filter">An optional additional filter combined with <paramref name="textFilter"/>.</param>
    /// <param name="loadFields">The fields to load into the aggregation pipeline.</param>
    /// <param name="applyClauses">Optional <c>APPLY</c> expressions evaluated per record.</param>
    /// <param name="groupBy">An optional <c>GROUPBY</c> stage with reducers.</param>
    /// <param name="sortBy">An optional <c>SORTBY</c> stage.</param>
    /// <param name="offset">The number of leading results to skip.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window.</param>
    /// <returns>A new <see cref="AggregateHybridQuery"/>.</returns>
    public static AggregateHybridQuery FromFloat32(
        FilterExpression textFilter,
        string vectorFieldName,
        float[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? loadFields = null,
        IEnumerable<AggregationApply>? applyClauses = null,
        AggregationGroupBy? groupBy = null,
        AggregationSortBy? sortBy = null,
        int offset = 0,
        int limit = 10,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(
            textFilter,
            vectorFieldName,
            VectorEncoding.ToBytes(vector),
            topK,
            filter,
            loadFields,
            applyClauses,
            groupBy,
            sortBy,
            offset,
            limit,
            scoreAlias,
            runtimeOptions,
            pagination);

    /// <summary>Creates an aggregate hybrid query from a double-precision (<c>FLOAT64</c>) vector.</summary>
    /// <param name="textFilter">The text filter; it must contain at least one text predicate.</param>
    /// <param name="vectorFieldName">The name of the vector field searched with <c>KNN</c>.</param>
    /// <param name="vector">The query vector as 64-bit floats.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve.</param>
    /// <param name="filter">An optional additional filter combined with <paramref name="textFilter"/>.</param>
    /// <param name="loadFields">The fields to load into the aggregation pipeline.</param>
    /// <param name="applyClauses">Optional <c>APPLY</c> expressions evaluated per record.</param>
    /// <param name="groupBy">An optional <c>GROUPBY</c> stage with reducers.</param>
    /// <param name="sortBy">An optional <c>SORTBY</c> stage.</param>
    /// <param name="offset">The number of leading results to skip.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window.</param>
    /// <returns>A new <see cref="AggregateHybridQuery"/>.</returns>
    public static AggregateHybridQuery FromFloat64(
        FilterExpression textFilter,
        string vectorFieldName,
        double[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? loadFields = null,
        IEnumerable<AggregationApply>? applyClauses = null,
        AggregationGroupBy? groupBy = null,
        AggregationSortBy? sortBy = null,
        int offset = 0,
        int limit = 10,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(
            textFilter,
            vectorFieldName,
            VectorEncoding.ToBytes(vector),
            topK,
            filter,
            loadFields,
            applyClauses,
            groupBy,
            sortBy,
            offset,
            limit,
            scoreAlias,
            runtimeOptions,
            pagination);

    private static IReadOnlyList<string> NormalizeFields(IEnumerable<string>? fields)
    {
        if (fields is null)
        {
            return [];
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field);
            var trimmed = field.Trim();
            var canonical = trimmed.TrimStart('@');
            if (seen.Add(canonical))
            {
                normalized.Add(trimmed);
            }
        }

        return normalized;
    }
}

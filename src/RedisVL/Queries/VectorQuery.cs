using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A K-nearest-neighbors query that ranks documents by their distance to the query vector on a single
/// vector field, optionally narrowed by a pre-filter (<c>KNN</c> with <c>FT.SEARCH</c>).
/// </summary>
public sealed class VectorQuery
{
    /// <summary>
    /// Initializes a new <see cref="VectorQuery"/>.
    /// </summary>
    /// <param name="fieldName">The name of the vector field to search.</param>
    /// <param name="vector">The raw query vector bytes; must be non-empty.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve; must be greater than zero.</param>
    /// <param name="filter">An optional pre-filter that narrows the candidate set before scoring.</param>
    /// <param name="returnFields">The fields to return for each match; when <see langword="null"/> all fields are returned.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window; defaults to a limit of <paramref name="topK"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="vector"/> is empty, or the pagination window exceeds <paramref name="topK"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topK"/> is not greater than zero.</exception>
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
        _vector = vector.ToArray();
        TopK = topK;
        Filter = filter;
        ScoreAlias = FilterExpression.NormalizeFieldName(scoreAlias);

        // When the caller does not specify return fields, leave the set empty so the builder omits
        // RETURN entirely and the server returns every stored field (plus the yielded score). Emitting
        // `RETURN 1 <scoreAlias>` here would make the obvious Search<T>(new VectorQuery(...)) happy
        // path throw, because the mapper treats every non-nullable property as required.
        HasExplicitReturnFields = returnFields is not null;
        ReturnFields = returnFields is null ? [] : NormalizeReturnFields(returnFields, ScoreAlias);
        RuntimeOptions = runtimeOptions;
        Pagination = pagination ?? new QueryPagination(limit: topK);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ValidatePaginationWindow(TopK, Pagination, nameof(topK));
    }

    private readonly byte[] _vector;

    /// <summary>The name of the vector field being searched.</summary>
    public string FieldName { get; }

    /// <summary>The raw query vector bytes. Each read returns a fresh copy so the query's state cannot be mutated.</summary>
    public byte[] Vector => _vector.ToArray();

    /// <summary>The number of nearest neighbors to retrieve.</summary>
    public int TopK { get; }

    /// <summary>The number of leading results to skip.</summary>
    public int Offset { get; }

    /// <summary>The maximum number of results to return.</summary>
    public int Limit { get; }

    /// <summary>An optional pre-filter applied before vector scoring, or <see langword="null"/> for none.</summary>
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

    /// <summary>Creates a vector query from a single-precision (<c>FLOAT32</c>) vector.</summary>
    /// <param name="fieldName">The name of the vector field to search.</param>
    /// <param name="vector">The query vector as 32-bit floats.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve.</param>
    /// <param name="filter">An optional pre-filter that narrows the candidate set.</param>
    /// <param name="returnFields">The fields to return for each match.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window.</param>
    /// <returns>A new <see cref="VectorQuery"/>.</returns>
    public static VectorQuery FromFloat32(
        string fieldName,
        float[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(fieldName, VectorEncoding.ToBytes(vector), topK, filter, returnFields, scoreAlias, runtimeOptions, pagination);

    /// <summary>Creates a vector query from a double-precision (<c>FLOAT64</c>) vector.</summary>
    /// <param name="fieldName">The name of the vector field to search.</param>
    /// <param name="vector">The query vector as 64-bit floats.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve.</param>
    /// <param name="filter">An optional pre-filter that narrows the candidate set.</param>
    /// <param name="returnFields">The fields to return for each match.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window.</param>
    /// <returns>A new <see cref="VectorQuery"/>.</returns>
    public static VectorQuery FromFloat64(
        string fieldName,
        double[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null) =>
        new(fieldName, VectorEncoding.ToBytes(vector), topK, filter, returnFields, scoreAlias, runtimeOptions, pagination);

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

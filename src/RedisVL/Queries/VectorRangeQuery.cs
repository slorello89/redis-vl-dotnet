using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A vector range query that returns every document whose distance to the query vector falls within a
/// given threshold, rather than a fixed number of nearest neighbors.
/// </summary>
public sealed class VectorRangeQuery
{
    /// <summary>
    /// Initializes a new <see cref="VectorRangeQuery"/>.
    /// </summary>
    /// <param name="fieldName">The name of the vector field to search.</param>
    /// <param name="vector">The raw query vector bytes; must be non-empty.</param>
    /// <param name="distanceThreshold">The maximum vector distance a document may have to be returned; must be zero or greater. A value of zero matches only exact duplicates.</param>
    /// <param name="filter">An optional pre-filter that narrows the candidate set.</param>
    /// <param name="returnFields">The fields to return for each match; when <see langword="null"/> all fields are returned.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="offset">The number of leading results to skip.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window; overrides <paramref name="offset"/> and <paramref name="limit"/> when supplied.</param>
    /// <exception cref="ArgumentException"><paramref name="vector"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="distanceThreshold"/> is negative or <see cref="double.NaN"/>.</exception>
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

        if (double.IsNaN(distanceThreshold) || distanceThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceThreshold), distanceThreshold, "Distance threshold must be zero or greater.");
        }

        FieldName = FilterExpression.NormalizeFieldName(fieldName);
        _vector = vector.ToArray();
        DistanceThreshold = distanceThreshold;
        Filter = filter;
        ScoreAlias = FilterExpression.NormalizeFieldName(scoreAlias);
        Pagination = pagination ?? new QueryPagination(offset, limit);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;

        // When the caller does not specify return fields, leave the set empty so the builder omits RETURN
        // entirely and the server returns every stored field (plus the yielded distance alias, which the
        // VECTOR_RANGE `$YIELD_DISTANCE_AS` clause produces independently of RETURN and which SORTBY still
        // targets). Emitting `RETURN 1 <scoreAlias>` here would make the typed happy path throw, because the
        // mapper treats every non-nullable property as required.
        HasExplicitReturnFields = returnFields is not null;
        ReturnFields = returnFields is null ? [] : QueryReturnFieldHelper.NormalizeReturnFields(returnFields, ScoreAlias);
        RuntimeOptions = runtimeOptions;
    }

    private readonly byte[] _vector;

    /// <summary>The name of the vector field being searched.</summary>
    public string FieldName { get; }

    /// <summary>The raw query vector bytes. Each read returns a fresh copy so the query's state cannot be mutated.</summary>
    public byte[] Vector => _vector.ToArray();

    /// <summary>The maximum vector distance a document may have to be included in the results.</summary>
    public double DistanceThreshold { get; }

    /// <summary>An optional pre-filter applied to the candidate set, or <see langword="null"/> for none.</summary>
    public FilterExpression? Filter { get; }

    /// <summary>The alias under which the vector distance is projected.</summary>
    public string ScoreAlias { get; }

    /// <summary>The number of leading results to skip.</summary>
    public int Offset { get; }

    /// <summary>The maximum number of results to return.</summary>
    public int Limit { get; }

    /// <summary>The pagination window applied to the results.</summary>
    public QueryPagination Pagination { get; }

    /// <summary>The fields returned for each matching document.</summary>
    public IReadOnlyList<string> ReturnFields { get; }

    /// <summary>
    /// Whether the caller explicitly supplied return fields. When <see langword="false"/> the return set was
    /// left unspecified and <see cref="ReturnFields"/> is empty, signalling the builder to omit RETURN so the
    /// server returns every stored field. Preserved through cloning so batched queries keep the same behavior.
    /// </summary>
    internal bool HasExplicitReturnFields { get; }

    /// <summary>Optional query-time index tuning parameters, or <see langword="null"/> to use index defaults.</summary>
    public VectorRangeRuntimeOptions? RuntimeOptions { get; }

    /// <summary>Creates a vector range query from a single-precision (<c>FLOAT32</c>) vector.</summary>
    /// <param name="fieldName">The name of the vector field to search.</param>
    /// <param name="vector">The query vector as 32-bit floats.</param>
    /// <param name="distanceThreshold">The maximum vector distance a document may have to be returned.</param>
    /// <param name="filter">An optional pre-filter that narrows the candidate set.</param>
    /// <param name="returnFields">The fields to return for each match.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="offset">The number of leading results to skip.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window.</param>
    /// <returns>A new <see cref="VectorRangeQuery"/>.</returns>
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
        new(fieldName, VectorEncoding.ToBytes(vector), distanceThreshold, filter, returnFields, scoreAlias, offset, limit, runtimeOptions, pagination);

    /// <summary>Creates a vector range query from a double-precision (<c>FLOAT64</c>) vector.</summary>
    /// <param name="fieldName">The name of the vector field to search.</param>
    /// <param name="vector">The query vector as 64-bit floats.</param>
    /// <param name="distanceThreshold">The maximum vector distance a document may have to be returned.</param>
    /// <param name="filter">An optional pre-filter that narrows the candidate set.</param>
    /// <param name="returnFields">The fields to return for each match.</param>
    /// <param name="scoreAlias">The alias under which the vector distance is projected.</param>
    /// <param name="offset">The number of leading results to skip.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window.</param>
    /// <returns>A new <see cref="VectorRangeQuery"/>.</returns>
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
        new(fieldName, VectorEncoding.ToBytes(vector), distanceThreshold, filter, returnFields, scoreAlias, offset, limit, runtimeOptions, pagination);
}

using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A K-nearest-neighbors query that scores documents against several vector fields at once, combining
/// each field's weighted <c>KNN</c> distance into a single ranking on the search index.
/// </summary>
public sealed class MultiVectorQuery
{
    /// <summary>
    /// Initializes a new <see cref="MultiVectorQuery"/>.
    /// </summary>
    /// <param name="vectors">The per-field vector inputs to score against; at least one is required.</param>
    /// <param name="topK">The number of nearest neighbors to retrieve; must be greater than zero.</param>
    /// <param name="filter">An optional pre-filter that narrows the candidate set before scoring.</param>
    /// <param name="returnFields">The fields to return for each match; when <see langword="null"/> all fields are returned.</param>
    /// <param name="scoreAlias">The alias under which the combined vector distance is projected.</param>
    /// <param name="runtimeOptions">Optional query-time index tuning parameters.</param>
    /// <param name="pagination">Optional pagination window; defaults to a limit of <paramref name="topK"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topK"/> is not greater than zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="vectors"/> is empty, or the pagination window exceeds <paramref name="topK"/>.</exception>
    public MultiVectorQuery(
        IEnumerable<MultiVectorInput> vectors,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null,
        QueryPagination? pagination = null)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentException.ThrowIfNullOrWhiteSpace(scoreAlias);

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "TopK must be greater than zero.");
        }

        var normalizedVectors = vectors.ToArray();
        if (normalizedVectors.Length == 0)
        {
            throw new ArgumentException("Multi-vector queries require at least one vector input.", nameof(vectors));
        }

        Vectors = normalizedVectors;
        TopK = topK;
        Filter = filter;
        ScoreAlias = FilterExpression.NormalizeFieldName(scoreAlias);

        // When the caller does not specify projected fields, leave the set empty so each fan-out sub-query
        // omits RETURN and CreateCombinedSearchDocument copies every stored field (minus the internal
        // per-vector score aliases) into the combined document. Otherwise the combined documents would carry
        // only the score and the typed happy path would throw. When fields are specified, behavior is
        // unchanged: sub-queries RETURN exactly those fields and only they are copied.
        HasExplicitReturnFields = returnFields is not null;
        ProjectedFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
        ReturnFields = QueryReturnFieldHelper.NormalizeReturnFields(returnFields, ScoreAlias);
        RuntimeOptions = runtimeOptions;
        Pagination = pagination ?? new QueryPagination(limit: topK);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ValidatePaginationWindow(TopK, Pagination, nameof(topK));
    }

    /// <summary>The per-field vector inputs scored by this query.</summary>
    public IReadOnlyList<MultiVectorInput> Vectors { get; }

    /// <summary>The number of nearest neighbors to retrieve.</summary>
    public int TopK { get; }

    /// <summary>The number of leading results to skip.</summary>
    public int Offset { get; }

    /// <summary>The maximum number of results to return.</summary>
    public int Limit { get; }

    /// <summary>An optional pre-filter applied before vector scoring, or <see langword="null"/> for no filter.</summary>
    public FilterExpression? Filter { get; }

    /// <summary>The alias under which the combined vector distance is projected.</summary>
    public string ScoreAlias { get; }

    /// <summary>The fields returned for each matching document.</summary>
    public IReadOnlyList<string> ReturnFields { get; }

    /// <summary>Optional query-time index tuning parameters, or <see langword="null"/> to use index defaults.</summary>
    public VectorKnnRuntimeOptions? RuntimeOptions { get; }

    /// <summary>The pagination window applied to the results.</summary>
    public QueryPagination Pagination { get; }

    /// <summary>
    /// Whether the caller explicitly supplied projected fields. When <see langword="false"/> the set was left
    /// unspecified and <see cref="ProjectedFields"/> is empty, signalling the fan-out to omit RETURN and the
    /// combiner to copy every stored field. Preserved through cloning so batched queries keep the same behavior.
    /// </summary>
    internal bool HasExplicitReturnFields { get; }

    internal IReadOnlyList<string> ProjectedFields { get; }

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

/// <summary>
/// A single weighted vector input for a <see cref="MultiVectorQuery"/>, pairing a vector field with the
/// query vector and the weight applied to its distance contribution.
/// </summary>
public sealed class MultiVectorInput
{
    /// <summary>
    /// Initializes a new <see cref="MultiVectorInput"/>.
    /// </summary>
    /// <param name="fieldName">The name of the vector field to score against.</param>
    /// <param name="vector">The raw query vector bytes; must be non-empty.</param>
    /// <param name="weight">The positive, finite weight applied to this field's distance contribution.</param>
    /// <exception cref="ArgumentException"><paramref name="vector"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is not a finite value greater than zero.</exception>
    public MultiVectorInput(string fieldName, byte[] vector, double weight = 1d)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(vector);

        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector input must contain at least one byte.", nameof(vector));
        }

        if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be a finite value greater than zero.");
        }

        FieldName = FilterExpression.NormalizeFieldName(fieldName);
        _vector = vector.ToArray();
        Weight = weight;
    }

    private readonly byte[] _vector;

    /// <summary>The name of the vector field this input is scored against.</summary>
    public string FieldName { get; }

    /// <summary>The raw query vector bytes. Each read returns a fresh copy so the input's state cannot be mutated.</summary>
    public byte[] Vector => _vector.ToArray();

    /// <summary>The weight applied to this field's distance contribution.</summary>
    public double Weight { get; }

    /// <summary>Creates an input from a single-precision (<c>FLOAT32</c>) vector.</summary>
    /// <param name="fieldName">The name of the vector field to score against.</param>
    /// <param name="vector">The query vector as 32-bit floats.</param>
    /// <param name="weight">The positive, finite weight applied to this field's distance contribution.</param>
    /// <returns>A new <see cref="MultiVectorInput"/>.</returns>
    public static MultiVectorInput FromFloat32(string fieldName, float[] vector, double weight = 1d) =>
        new(fieldName, VectorEncoding.ToBytes(vector), weight);

    /// <summary>Creates an input from a double-precision (<c>FLOAT64</c>) vector.</summary>
    /// <param name="fieldName">The name of the vector field to score against.</param>
    /// <param name="vector">The query vector as 64-bit floats.</param>
    /// <param name="weight">The positive, finite weight applied to this field's distance contribution.</param>
    /// <returns>A new <see cref="MultiVectorInput"/>.</returns>
    public static MultiVectorInput FromFloat64(string fieldName, double[] vector, double weight = 1d) =>
        new(fieldName, VectorEncoding.ToBytes(vector), weight);
}

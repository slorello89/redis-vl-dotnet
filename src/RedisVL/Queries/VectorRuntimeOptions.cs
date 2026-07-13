namespace RedisVL.Queries;

/// <summary>
/// Query-time tuning parameters for a <c>KNN</c> vector search, mapping to the runtime attributes accepted
/// by HNSW and SVS-VAMANA indexes.
/// </summary>
public sealed record VectorKnnRuntimeOptions
{
    /// <summary>
    /// Initializes a new <see cref="VectorKnnRuntimeOptions"/>.
    /// </summary>
    /// <param name="efRuntime">The HNSW <c>EF_RUNTIME</c> value; must be greater than zero when specified.</param>
    /// <param name="searchWindowSize">The SVS-VAMANA <c>SEARCH_WINDOW_SIZE</c> value; must be greater than zero when specified.</param>
    /// <param name="useSearchHistory">The SVS-VAMANA <c>USE_SEARCH_HISTORY</c> value.</param>
    /// <param name="searchBufferCapacity">The SVS-VAMANA <c>SEARCH_BUFFER_CAPACITY</c> value; must be greater than zero when specified.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied numeric option is not greater than zero, or <paramref name="useSearchHistory"/> is not a defined value.</exception>
    public VectorKnnRuntimeOptions(
        int? efRuntime = null,
        int? searchWindowSize = null,
        SvsSearchHistory? useSearchHistory = null,
        int? searchBufferCapacity = null)
    {
        if (efRuntime is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(efRuntime), efRuntime, "EF runtime must be greater than zero.");
        }

        if (searchWindowSize is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(searchWindowSize), searchWindowSize, "Search window size must be greater than zero.");
        }

        if (searchBufferCapacity is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(searchBufferCapacity), searchBufferCapacity, "Search buffer capacity must be greater than zero.");
        }

        if (useSearchHistory is not null && !Enum.IsDefined(useSearchHistory.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(useSearchHistory), useSearchHistory, "Unsupported search history value.");
        }

        EfRuntime = efRuntime;
        SearchWindowSize = searchWindowSize;
        UseSearchHistory = useSearchHistory;
        SearchBufferCapacity = searchBufferCapacity;
    }

    /// <summary>HNSW <c>EF_RUNTIME</c> query-time parameter.</summary>
    public int? EfRuntime { get; }

    /// <summary>SVS-VAMANA <c>SEARCH_WINDOW_SIZE</c> query-time parameter.</summary>
    public int? SearchWindowSize { get; }

    /// <summary>SVS-VAMANA <c>USE_SEARCH_HISTORY</c> query-time parameter.</summary>
    public SvsSearchHistory? UseSearchHistory { get; }

    /// <summary>SVS-VAMANA <c>SEARCH_BUFFER_CAPACITY</c> query-time parameter.</summary>
    public int? SearchBufferCapacity { get; }
}

/// <summary>
/// Query-time tuning parameters for a vector range search, controlling the boundary relaxation applied
/// around the distance threshold.
/// </summary>
public sealed record VectorRangeRuntimeOptions
{
    /// <summary>
    /// Initializes a new <see cref="VectorRangeRuntimeOptions"/>.
    /// </summary>
    /// <param name="epsilon">The relative boundary relaxation factor; must be a finite, non-negative value when specified.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epsilon"/> is negative or not finite.</exception>
    public VectorRangeRuntimeOptions(double? epsilon = null)
    {
        if (epsilon is < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "Epsilon cannot be negative.");
        }

        if (epsilon is not null && (double.IsNaN(epsilon.Value) || double.IsInfinity(epsilon.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "Epsilon must be a finite value.");
        }

        Epsilon = epsilon;
    }

    /// <summary>The <c>EPSILON</c> boundary relaxation factor, or <see langword="null"/> to use the index default.</summary>
    public double? Epsilon { get; }
}

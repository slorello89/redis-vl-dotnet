namespace RedisVL.Queries;

public sealed record VectorKnnRuntimeOptions
{
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

public sealed record VectorRangeRuntimeOptions
{
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

    public double? Epsilon { get; }
}

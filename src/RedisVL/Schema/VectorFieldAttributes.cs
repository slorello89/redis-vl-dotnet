namespace RedisVL.Schema;

public sealed record VectorFieldAttributes
{
    public VectorFieldAttributes(
        VectorAlgorithm algorithm,
        VectorDataType dataType,
        VectorDistanceMetric distanceMetric,
        int dimensions,
        int initialCapacity = 0,
        int blockSize = 0,
        int m = 0,
        int efConstruction = 0,
        int efRuntime = 0,
        VectorCompression compression = VectorCompression.None,
        int constructionWindowSize = 0,
        int graphMaxDegree = 0,
        int searchWindowSize = 0,
        double epsilon = 0d,
        int trainingThreshold = 0,
        int reduce = 0)
    {
        ValidateEnum(algorithm, nameof(algorithm));
        ValidateEnum(dataType, nameof(dataType));
        ValidateEnum(distanceMetric, nameof(distanceMetric));
        ValidateEnum(compression, nameof(compression));

        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), dimensions, "Vector dimensions must be greater than zero.");
        }

        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "Vector initial capacity cannot be negative.");
        }

        if (blockSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), blockSize, "Vector block size cannot be negative.");
        }

        if (m < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(m), m, "Vector HNSW M cannot be negative.");
        }

        if (efConstruction < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(efConstruction), efConstruction, "Vector HNSW EF construction cannot be negative.");
        }

        if (efRuntime < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(efRuntime), efRuntime, "Vector HNSW EF runtime cannot be negative.");
        }

        if (constructionWindowSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(constructionWindowSize), constructionWindowSize, "Vector SVS-VAMANA construction window size cannot be negative.");
        }

        if (graphMaxDegree < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(graphMaxDegree), graphMaxDegree, "Vector SVS-VAMANA graph max degree cannot be negative.");
        }

        if (searchWindowSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(searchWindowSize), searchWindowSize, "Vector SVS-VAMANA search window size cannot be negative.");
        }

        if (epsilon < 0d || double.IsNaN(epsilon) || double.IsInfinity(epsilon))
        {
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "Vector SVS-VAMANA epsilon must be a finite, non-negative value.");
        }

        if (trainingThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trainingThreshold), trainingThreshold, "Vector SVS-VAMANA training threshold cannot be negative.");
        }

        if (reduce < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reduce), reduce, "Vector SVS-VAMANA reduce dimensions cannot be negative.");
        }

        if (algorithm != VectorAlgorithm.Hnsw && (m > 0 || efConstruction > 0 || efRuntime > 0))
        {
            throw new ArgumentException(
                $"{DescribeAlgorithm(algorithm)} vector fields do not support HNSW-specific settings such as M, EF construction, or EF runtime.",
                nameof(algorithm));
        }

        if (algorithm != VectorAlgorithm.SvsVamana
            && (compression != VectorCompression.None
                || constructionWindowSize > 0
                || graphMaxDegree > 0
                || searchWindowSize > 0
                || epsilon > 0d
                || trainingThreshold > 0
                || reduce > 0))
        {
            throw new ArgumentException(
                $"{DescribeAlgorithm(algorithm)} vector fields do not support SVS-VAMANA-specific settings such as compression, graph max degree, or construction window size.",
                nameof(algorithm));
        }

        Algorithm = algorithm;
        DataType = dataType;
        DistanceMetric = distanceMetric;
        Dimensions = dimensions;
        InitialCapacity = initialCapacity;
        BlockSize = blockSize;
        M = m;
        EfConstruction = efConstruction;
        EfRuntime = efRuntime;
        Compression = compression;
        ConstructionWindowSize = constructionWindowSize;
        GraphMaxDegree = graphMaxDegree;
        SearchWindowSize = searchWindowSize;
        Epsilon = epsilon;
        TrainingThreshold = trainingThreshold;
        Reduce = reduce;
    }

    public VectorAlgorithm Algorithm { get; }

    public VectorDataType DataType { get; }

    public VectorDistanceMetric DistanceMetric { get; }

    public int Dimensions { get; }

    public int InitialCapacity { get; }

    public int BlockSize { get; }

    public int M { get; }

    public int EfConstruction { get; }

    public int EfRuntime { get; }

    /// <summary>SVS-VAMANA vector compression algorithm. Defaults to <see cref="VectorCompression.None"/>.</summary>
    public VectorCompression Compression { get; }

    /// <summary>SVS-VAMANA search window size used while building the graph (<c>CONSTRUCTION_WINDOW_SIZE</c>).</summary>
    public int ConstructionWindowSize { get; }

    /// <summary>SVS-VAMANA maximum number of edges per graph node (<c>GRAPH_MAX_DEGREE</c>).</summary>
    public int GraphMaxDegree { get; }

    /// <summary>SVS-VAMANA default search window size used at query time (<c>SEARCH_WINDOW_SIZE</c>).</summary>
    public int SearchWindowSize { get; }

    /// <summary>SVS-VAMANA range-search approximation factor (<c>EPSILON</c>).</summary>
    public double Epsilon { get; }

    /// <summary>SVS-VAMANA number of vectors required before compression parameters are learned (<c>TRAINING_THRESHOLD</c>).</summary>
    public int TrainingThreshold { get; }

    /// <summary>SVS-VAMANA reduced dimension count for LeanVec compression (<c>REDUCE</c>).</summary>
    public int Reduce { get; }

    private static string DescribeAlgorithm(VectorAlgorithm algorithm) =>
        algorithm switch
        {
            VectorAlgorithm.Flat => "FLAT",
            VectorAlgorithm.Hnsw => "HNSW",
            VectorAlgorithm.SvsVamana => "SVS-VAMANA",
            _ => algorithm.ToString()
        };

    private static void ValidateEnum<TEnum>(TEnum value, string paramName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Unsupported {typeof(TEnum).Name} value.");
        }
    }
}

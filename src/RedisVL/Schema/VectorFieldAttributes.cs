namespace RedisVL.Schema;

/// <summary>
/// Holds the algorithm and tuning parameters of a vector field, such as the indexing algorithm,
/// element data type, distance metric, and dimensionality emitted in the <c>FT.CREATE</c> <c>VECTOR</c> block.
/// </summary>
public sealed record VectorFieldAttributes
{
    /// <summary>
    /// Initializes a new <see cref="VectorFieldAttributes"/> and validates that the supplied parameters
    /// are consistent with the chosen <paramref name="algorithm"/>.
    /// </summary>
    /// <param name="algorithm">The vector indexing algorithm (<c>FLAT</c>, <c>HNSW</c>, or <c>SVS-VAMANA</c>).</param>
    /// <param name="dataType">The numeric type of each vector element.</param>
    /// <param name="distanceMetric">The distance metric used to compare vectors.</param>
    /// <param name="dimensions">The number of dimensions per vector; must be greater than zero.</param>
    /// <param name="initialCapacity">The initial index capacity (<c>INITIAL_CAP</c>), or zero for the default.</param>
    /// <param name="blockSize">The FLAT block size (<c>BLOCK_SIZE</c>), or zero for the default.</param>
    /// <param name="m">The HNSW maximum edges per node (<c>M</c>), or zero for the default.</param>
    /// <param name="efConstruction">The HNSW build-time candidate list size (<c>EF_CONSTRUCTION</c>), or zero for the default.</param>
    /// <param name="efRuntime">The HNSW query-time candidate list size (<c>EF_RUNTIME</c>), or zero for the default.</param>
    /// <param name="compression">The SVS-VAMANA vector compression algorithm.</param>
    /// <param name="constructionWindowSize">The SVS-VAMANA build-time search window size (<c>CONSTRUCTION_WINDOW_SIZE</c>).</param>
    /// <param name="graphMaxDegree">The SVS-VAMANA maximum edges per graph node (<c>GRAPH_MAX_DEGREE</c>).</param>
    /// <param name="searchWindowSize">The SVS-VAMANA query-time search window size (<c>SEARCH_WINDOW_SIZE</c>).</param>
    /// <param name="epsilon">The SVS-VAMANA range-search approximation factor (<c>EPSILON</c>).</param>
    /// <param name="trainingThreshold">The SVS-VAMANA vector count before compression is trained (<c>TRAINING_THRESHOLD</c>).</param>
    /// <param name="reduce">The SVS-VAMANA reduced dimension count for LeanVec compression (<c>REDUCE</c>).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an enum value is undefined or a numeric parameter is out of range.</exception>
    /// <exception cref="ArgumentException">Thrown when a parameter is not supported by the selected <paramref name="algorithm"/>.</exception>
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
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "Vector epsilon must be a finite, non-negative value.");
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

        // EPSILON is the range-query approximation factor, valid for HNSW and SVS-VAMANA
        // but rejected by Redis for FLAT fields.
        if (algorithm != VectorAlgorithm.Hnsw && algorithm != VectorAlgorithm.SvsVamana && epsilon > 0d)
        {
            throw new ArgumentException(
                $"{DescribeAlgorithm(algorithm)} vector fields do not support the EPSILON range-query approximation factor.",
                nameof(algorithm));
        }

        if (algorithm != VectorAlgorithm.SvsVamana
            && (compression != VectorCompression.None
                || constructionWindowSize > 0
                || graphMaxDegree > 0
                || searchWindowSize > 0
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

    /// <summary>The vector indexing algorithm (<c>FLAT</c>, <c>HNSW</c>, or <c>SVS-VAMANA</c>).</summary>
    public VectorAlgorithm Algorithm { get; }

    /// <summary>The numeric type of each vector element.</summary>
    public VectorDataType DataType { get; }

    /// <summary>The distance metric used to compare vectors.</summary>
    public VectorDistanceMetric DistanceMetric { get; }

    /// <summary>The number of dimensions per vector.</summary>
    public int Dimensions { get; }

    /// <summary>The initial index capacity (<c>INITIAL_CAP</c>); zero for the server default.</summary>
    public int InitialCapacity { get; }

    /// <summary>The FLAT block size (<c>BLOCK_SIZE</c>); zero for the server default.</summary>
    public int BlockSize { get; }

    /// <summary>The HNSW maximum number of edges per node (<c>M</c>); zero for the server default.</summary>
    public int M { get; }

    /// <summary>The HNSW build-time candidate list size (<c>EF_CONSTRUCTION</c>); zero for the server default.</summary>
    public int EfConstruction { get; }

    /// <summary>The HNSW query-time candidate list size (<c>EF_RUNTIME</c>); zero for the server default.</summary>
    public int EfRuntime { get; }

    /// <summary>SVS-VAMANA vector compression algorithm. Defaults to <see cref="VectorCompression.None"/>.</summary>
    public VectorCompression Compression { get; }

    /// <summary>SVS-VAMANA search window size used while building the graph (<c>CONSTRUCTION_WINDOW_SIZE</c>).</summary>
    public int ConstructionWindowSize { get; }

    /// <summary>SVS-VAMANA maximum number of edges per graph node (<c>GRAPH_MAX_DEGREE</c>).</summary>
    public int GraphMaxDegree { get; }

    /// <summary>SVS-VAMANA default search window size used at query time (<c>SEARCH_WINDOW_SIZE</c>).</summary>
    public int SearchWindowSize { get; }

    /// <summary>HNSW/SVS-VAMANA range-query approximation factor (<c>EPSILON</c>).</summary>
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

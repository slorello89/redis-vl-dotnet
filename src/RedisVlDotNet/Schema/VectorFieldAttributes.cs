namespace RedisVl.Schema;

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
        int efRuntime = 0)
    {
        ValidateEnum(algorithm, nameof(algorithm));
        ValidateEnum(dataType, nameof(dataType));
        ValidateEnum(distanceMetric, nameof(distanceMetric));

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

        if (algorithm == VectorAlgorithm.Flat && (m > 0 || efConstruction > 0 || efRuntime > 0))
        {
            throw new ArgumentException(
                "FLAT vector fields do not support HNSW-specific settings such as M, EF construction, or EF runtime.",
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

    private static void ValidateEnum<TEnum>(TEnum value, string paramName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Unsupported {typeof(TEnum).Name} value.");
        }
    }
}

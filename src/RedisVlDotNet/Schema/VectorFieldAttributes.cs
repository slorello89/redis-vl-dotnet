namespace RedisVlDotNet.Schema;

public sealed record VectorFieldAttributes(
    VectorAlgorithm Algorithm,
    VectorDataType DataType,
    VectorDistanceMetric DistanceMetric,
    int Dimensions,
    int InitialCapacity = 0,
    int BlockSize = 0,
    int M = 0,
    int EfConstruction = 0,
    int EfRuntime = 0);

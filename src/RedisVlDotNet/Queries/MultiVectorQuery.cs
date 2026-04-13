using System.Runtime.InteropServices;
using RedisVlDotNet.Filters;

namespace RedisVlDotNet.Queries;

public sealed class MultiVectorQuery
{
    public MultiVectorQuery(
        IEnumerable<MultiVectorInput> vectors,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        string scoreAlias = "vector_distance")
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
        ProjectedFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
        ReturnFields = QueryReturnFieldHelper.NormalizeReturnFields(returnFields, ScoreAlias);
    }

    public IReadOnlyList<MultiVectorInput> Vectors { get; }

    public int TopK { get; }

    public FilterExpression? Filter { get; }

    public string ScoreAlias { get; }

    public IReadOnlyList<string> ReturnFields { get; }

    internal IReadOnlyList<string> ProjectedFields { get; }
}

public sealed class MultiVectorInput
{
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
        Vector = vector.ToArray();
        Weight = weight;
    }

    public string FieldName { get; }

    public byte[] Vector { get; }

    public double Weight { get; }

    public static MultiVectorInput FromFloat32(string fieldName, float[] vector, double weight = 1d) =>
        new(fieldName, MemoryMarshal.AsBytes<float>(vector.AsSpan()).ToArray(), weight);

    public static MultiVectorInput FromFloat64(string fieldName, double[] vector, double weight = 1d) =>
        new(fieldName, MemoryMarshal.AsBytes<double>(vector.AsSpan()).ToArray(), weight);
}

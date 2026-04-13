namespace RedisVlDotNet.Vectorizers;

public interface IBatchTextVectorizer : ITextVectorizer
{
    Task<IReadOnlyList<float[]>> VectorizeAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);
}

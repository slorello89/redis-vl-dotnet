namespace RedisVlDotNet.Vectorizers;

public interface ITextVectorizer
{
    Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default);
}

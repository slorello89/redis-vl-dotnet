namespace RedisVlDotNet.Caches;

public interface ITextEmbeddingGenerator
{
    Task<float[]> GenerateAsync(string input, CancellationToken cancellationToken = default);
}

using RedisVlDotNet.Vectorizers;

namespace RedisVlDotNet.Caches;

[Obsolete("Use RedisVlDotNet.Vectorizers.ITextVectorizer from the RedisVlDotNet.Vectorizers.Abstractions package.")]
public interface ITextEmbeddingGenerator : ITextVectorizer
{
    Task<float[]> GenerateAsync(string input, CancellationToken cancellationToken = default);

    Task<float[]> ITextVectorizer.VectorizeAsync(string input, CancellationToken cancellationToken) =>
        GenerateAsync(input, cancellationToken);
}

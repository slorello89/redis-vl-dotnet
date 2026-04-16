using RedisVL.Vectorizers;

namespace RedisVL.Caches;

[Obsolete("Use RedisVL.Vectorizers.ITextVectorizer from the RedisVL.Vectorizers.Abstractions package.")]
public interface ITextEmbeddingGenerator : ITextVectorizer
{
    Task<float[]> GenerateAsync(string input, CancellationToken cancellationToken = default);

    Task<float[]> ITextVectorizer.VectorizeAsync(string input, CancellationToken cancellationToken) =>
        GenerateAsync(input, cancellationToken);
}

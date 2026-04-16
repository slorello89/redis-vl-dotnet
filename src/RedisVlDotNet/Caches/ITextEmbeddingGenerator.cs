using RedisVl.Vectorizers;

namespace RedisVl.Caches;

[Obsolete("Use RedisVl.Vectorizers.ITextVectorizer from the RedisVl.Vectorizers.Abstractions package.")]
public interface ITextEmbeddingGenerator : ITextVectorizer
{
    Task<float[]> GenerateAsync(string input, CancellationToken cancellationToken = default);

    Task<float[]> ITextVectorizer.VectorizeAsync(string input, CancellationToken cancellationToken) =>
        GenerateAsync(input, cancellationToken);
}

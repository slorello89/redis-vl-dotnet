using RedisVL.Vectorizers;

namespace RedisVL.Caches;

/// <summary>
/// Legacy abstraction for generating an embedding vector from input text.
/// </summary>
/// <remarks>Deprecated in favor of <see cref="ITextVectorizer" />, which this interface adapts to.</remarks>
[Obsolete("Use RedisVL.Vectorizers.ITextVectorizer from the RedisVL.Vectorizers.Abstractions package.")]
public interface ITextEmbeddingGenerator : ITextVectorizer
{
    /// <summary>Generates an embedding vector for the supplied <paramref name="input" /> text.</summary>
    /// <param name="input">The text to embed.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The embedding vector for <paramref name="input" />.</returns>
    Task<float[]> GenerateAsync(string input, CancellationToken cancellationToken = default);

    Task<float[]> ITextVectorizer.VectorizeAsync(string input, CancellationToken cancellationToken) =>
        GenerateAsync(input, cancellationToken);
}

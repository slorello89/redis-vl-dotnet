namespace RedisVL.Vectorizers;

/// <summary>
/// Converts text into a dense embedding vector.
/// </summary>
public interface ITextVectorizer
{
    /// <summary>
    /// Generates an embedding for a single text input.
    /// </summary>
    /// <param name="input">The text to embed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The embedding vector for <paramref name="input"/>.</returns>
    Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default);
}

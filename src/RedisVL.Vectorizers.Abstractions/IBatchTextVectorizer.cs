namespace RedisVL.Vectorizers;

/// <summary>
/// An <see cref="ITextVectorizer"/> that can embed multiple text inputs in a single batched request.
/// </summary>
public interface IBatchTextVectorizer : ITextVectorizer
{
    /// <summary>
    /// Generates embeddings for a batch of text inputs.
    /// </summary>
    /// <param name="inputs">The texts to embed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of embedding vectors, one per input, in the same order as <paramref name="inputs"/>.</returns>
    Task<IReadOnlyList<float[]>> VectorizeAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);
}

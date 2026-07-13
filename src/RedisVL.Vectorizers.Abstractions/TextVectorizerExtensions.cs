namespace RedisVL.Vectorizers;

/// <summary>
/// Extension methods for <see cref="ITextVectorizer"/>.
/// </summary>
public static class TextVectorizerExtensions
{
    /// <summary>
    /// Embeds multiple inputs, using a single batched request when the vectorizer implements
    /// <see cref="IBatchTextVectorizer"/> and falling back to sequential per-input calls otherwise.
    /// </summary>
    /// <param name="vectorizer">The vectorizer to use.</param>
    /// <param name="inputs">The texts to embed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of embedding vectors, one per input, in the same order as <paramref name="inputs"/>.</returns>
    public static async Task<IReadOnlyList<float[]>> VectorizeManyAsync(
        this ITextVectorizer vectorizer,
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count == 0)
        {
            return [];
        }

        if (vectorizer is IBatchTextVectorizer batchVectorizer)
        {
            return await batchVectorizer.VectorizeAsync(inputs, cancellationToken).ConfigureAwait(false);
        }

        var embeddings = new float[inputs.Count][];
        for (var index = 0; index < inputs.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            embeddings[index] = await vectorizer.VectorizeAsync(inputs[index], cancellationToken).ConfigureAwait(false);
        }

        return embeddings;
    }
}

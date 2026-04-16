namespace RedisVl.Vectorizers;

public static class TextVectorizerExtensions
{
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

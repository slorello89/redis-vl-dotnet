namespace RedisVL.Vectorizers.Onnx.Internal;

internal static class EmbeddingPooler
{
    /// <summary>
    /// Reduces a <c>tokens × hidden</c> matrix to a single embedding using the configured
    /// pooling strategy, then optionally L2-normalizes the result.
    /// </summary>
    public static float[] Pool(
        float[][] tokenEmbeddings,
        long[] attentionMask,
        OnnxPoolingStrategy strategy,
        bool normalize)
    {
        ArgumentNullException.ThrowIfNull(tokenEmbeddings);
        ArgumentNullException.ThrowIfNull(attentionMask);

        if (tokenEmbeddings.Length == 0)
        {
            throw new InvalidOperationException("The ONNX model produced no token embeddings to pool.");
        }

        var pooled = strategy switch
        {
            OnnxPoolingStrategy.Mean => MeanPool(tokenEmbeddings, attentionMask),
            OnnxPoolingStrategy.Cls => ClsPool(tokenEmbeddings),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unsupported pooling strategy.")
        };

        if (normalize)
        {
            L2Normalize(pooled);
        }

        return pooled;
    }

    private static float[] MeanPool(float[][] tokenEmbeddings, long[] attentionMask)
    {
        var hiddenSize = tokenEmbeddings[0].Length;
        var sums = new double[hiddenSize];
        var included = 0L;

        for (var token = 0; token < tokenEmbeddings.Length; token++)
        {
            // Treat tokens beyond the supplied mask as attended (mask defaults to 1).
            var maskValue = token < attentionMask.Length ? attentionMask[token] : 1L;
            if (maskValue == 0)
            {
                continue;
            }

            var embedding = tokenEmbeddings[token];
            if (embedding.Length != hiddenSize)
            {
                throw new InvalidOperationException("All token embeddings must share the same hidden size.");
            }

            for (var dimension = 0; dimension < hiddenSize; dimension++)
            {
                sums[dimension] += embedding[dimension];
            }

            included++;
        }

        // Guard against an all-zero mask the same way SentenceTransformers clamps the denominator.
        var denominator = included == 0 ? 1L : included;
        var pooled = new float[hiddenSize];
        for (var dimension = 0; dimension < hiddenSize; dimension++)
        {
            pooled[dimension] = (float)(sums[dimension] / denominator);
        }

        return pooled;
    }

    private static float[] ClsPool(float[][] tokenEmbeddings)
    {
        var cls = tokenEmbeddings[0];
        var pooled = new float[cls.Length];
        Array.Copy(cls, pooled, cls.Length);
        return pooled;
    }

    private static void L2Normalize(float[] embedding)
    {
        var sumOfSquares = 0d;
        foreach (var value in embedding)
        {
            sumOfSquares += (double)value * value;
        }

        var magnitude = Math.Sqrt(sumOfSquares);
        if (magnitude <= 0d)
        {
            return;
        }

        for (var index = 0; index < embedding.Length; index++)
        {
            embedding[index] = (float)(embedding[index] / magnitude);
        }
    }
}

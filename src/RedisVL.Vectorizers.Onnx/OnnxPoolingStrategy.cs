namespace RedisVL.Vectorizers.Onnx;

/// <summary>
/// Determines how per-token model outputs are reduced to a single sentence embedding.
/// </summary>
public enum OnnxPoolingStrategy
{
    /// <summary>
    /// Averages the token embeddings using the attention mask, matching the default
    /// SentenceTransformers mean-pooling behavior.
    /// </summary>
    Mean,

    /// <summary>
    /// Uses the embedding of the leading <c>[CLS]</c> token.
    /// </summary>
    Cls
}

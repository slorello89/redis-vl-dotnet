namespace RedisVL.Vectorizers.Onnx.Internal;

internal interface IOnnxEmbeddingRunner : IDisposable
{
    /// <summary>
    /// Runs the model for a single encoded input and returns the per-token output embeddings
    /// as a <c>tokens × hidden</c> matrix (the model's <c>last_hidden_state</c>).
    /// </summary>
    float[][] Run(EncodedOnnxInput input);
}

namespace RedisVL.Rerankers.Onnx.Internal;

internal interface IOnnxInferenceRunner : IDisposable
{
    double Score(EncodedOnnxInput input);
}

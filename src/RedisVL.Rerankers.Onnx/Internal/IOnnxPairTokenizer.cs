namespace RedisVL.Rerankers.Onnx.Internal;

internal interface IOnnxPairTokenizer
{
    EncodedOnnxInput Encode(string query, string document, int maxSequenceLength);
}

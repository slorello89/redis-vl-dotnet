namespace RedisVL.Vectorizers.Onnx.Internal;

internal interface IOnnxTextTokenizer
{
    EncodedOnnxInput Encode(string text, int maxSequenceLength);
}

namespace RedisVL.Vectorizers.Onnx.Internal;

internal readonly record struct EncodedOnnxInput(
    long[] InputIds,
    long[] AttentionMask,
    long[] TokenTypeIds);

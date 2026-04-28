namespace RedisVL.Rerankers.Onnx.Internal;

internal readonly record struct EncodedOnnxInput(
    long[] InputIds,
    long[] AttentionMask,
    long[] TokenTypeIds);

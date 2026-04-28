# ONNX Reranker Example

This example uses `RedisVL.Rerankers.Onnx` to rerank an existing candidate list locally with a BERT-style ONNX cross-encoder.

## Prerequisites

- `ONNX_RERANKER_MODEL_PATH` pointing to a local `model.onnx`
- `ONNX_RERANKER_TOKENIZER_PATH` pointing to a local `tokenizer.json`

If either variable is missing, the example exits immediately with an explicit environment-variable error instead of attempting inference.

## Run

```bash
dotnet run --project examples/OnnxRerankerExample/OnnxRerankerExample.csproj
```

## Related Docs

- [ONNX Reranker](../../docs-site/modules/ROOT/pages/extensions/onnx-reranker.adoc)
- [Reranker Abstractions](../../docs-site/modules/ROOT/pages/extensions/reranker-abstractions.adoc)
- [Examples index](../README.md)

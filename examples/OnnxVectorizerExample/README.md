# ONNX Vectorizer Example

This example uses `RedisVL.Vectorizers.Onnx` to generate sentence embeddings locally with a BERT-style ONNX SentenceTransformers model — no API key or network call required.

## Prerequisites

- `ONNX_VECTORIZER_MODEL_PATH` pointing to a local `model.onnx`
- `ONNX_VECTORIZER_TOKENIZER_PATH` pointing to a local `tokenizer.json`

If either variable is missing, the example exits immediately with an explicit environment-variable error instead of attempting inference.

A typical source is a sentence-embedding model such as `sentence-transformers/all-MiniLM-L6-v2` exported to ONNX (for example with Hugging Face Optimum), which ships both a `model.onnx` and a `tokenizer.json`.

## Run

```bash
export ONNX_VECTORIZER_MODEL_PATH=/path/to/model.onnx
export ONNX_VECTORIZER_TOKENIZER_PATH=/path/to/tokenizer.json
dotnet run --project examples/OnnxVectorizerExample/OnnxVectorizerExample.csproj
```

The example embeds three prompts in one batch, then prints the cosine similarity of two of them to the first. The paraphrased password prompt should score higher than the unrelated cafeteria prompt.

## Related Docs

- [ONNX Vectorizer](../../docs-site/modules/ROOT/pages/extensions/onnx-vectorizer.adoc)
- [Vectorizer Abstractions](../../docs-site/modules/ROOT/pages/extensions/vectorizer-abstractions.adoc)
- [Examples index](../README.md)

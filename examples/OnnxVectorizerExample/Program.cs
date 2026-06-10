using RedisVL.Vectorizers.Onnx;

var modelPath = Environment.GetEnvironmentVariable("ONNX_VECTORIZER_MODEL_PATH");
if (string.IsNullOrWhiteSpace(modelPath))
{
    throw new InvalidOperationException("Set ONNX_VECTORIZER_MODEL_PATH before running the ONNX vectorizer example.");
}

var tokenizerPath = Environment.GetEnvironmentVariable("ONNX_VECTORIZER_TOKENIZER_PATH");
if (string.IsNullOrWhiteSpace(tokenizerPath))
{
    throw new InvalidOperationException("Set ONNX_VECTORIZER_TOKENIZER_PATH before running the ONNX vectorizer example.");
}

var prompts = new[]
{
    "How do I reset my password?",
    "Steps to recover a forgotten account password.",
    "What time does the cafeteria open for lunch?"
};

using var vectorizer = new OnnxTextVectorizer(
    new OnnxVectorizerOptions
    {
        ModelPath = modelPath,
        TokenizerPath = tokenizerPath,
        MaxSequenceLength = 256,
        Pooling = OnnxPoolingStrategy.Mean,
        Normalize = true
    });

// Embed the whole batch offline in a single call — no API key or network required.
var embeddings = await vectorizer.VectorizeAsync(prompts);

Console.WriteLine($"Generated {embeddings.Count} embeddings with {embeddings[0].Length} dimensions each.");
Console.WriteLine();

Console.WriteLine("Cosine similarity to the first prompt:");
Console.WriteLine($"  \"{prompts[0]}\"");
Console.WriteLine();

for (var index = 1; index < prompts.Length; index++)
{
    var similarity = CosineSimilarity(embeddings[0], embeddings[index]);
    Console.WriteLine($"- {similarity:F4}  \"{prompts[index]}\"");
}

Console.WriteLine();
Console.WriteLine("The paraphrased password prompt should score higher than the unrelated cafeteria prompt.");

static double CosineSimilarity(float[] left, float[] right)
{
    // Embeddings are L2-normalized, so the dot product is already the cosine similarity.
    var dot = 0d;
    for (var index = 0; index < left.Length; index++)
    {
        dot += (double)left[index] * right[index];
    }

    return dot;
}

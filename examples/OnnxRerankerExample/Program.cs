using RedisVL.Rerankers;
using RedisVL.Rerankers.Onnx;

var modelPath = Environment.GetEnvironmentVariable("ONNX_RERANKER_MODEL_PATH");
if (string.IsNullOrWhiteSpace(modelPath))
{
    throw new InvalidOperationException("Set ONNX_RERANKER_MODEL_PATH before running the ONNX reranker example.");
}

var tokenizerPath = Environment.GetEnvironmentVariable("ONNX_RERANKER_TOKENIZER_PATH");
if (string.IsNullOrWhiteSpace(tokenizerPath))
{
    throw new InvalidOperationException("Set ONNX_RERANKER_TOKENIZER_PATH before running the ONNX reranker example.");
}

var candidates =
    new[]
    {
        new Article("billing-history", "Export billing history", "Open Billing and download invoices or payment history as CSV."),
        new Article("reset-password", "Reset your password", "Open Settings, select Security, and follow the password reset prompts."),
        new Article("rotate-token", "Rotate API tokens", "Create a replacement token first, update clients, then revoke the old token.")
    };

using var reranker = new OnnxTextReranker(
    new OnnxRerankerOptions
    {
        ModelPath = modelPath,
        TokenizerPath = tokenizerPath,
        MaxSequenceLength = 512
    });

var request = new RerankRequest(
    "How do I reset my password?",
    candidates.Select(article => new RerankDocument(
            $"{article.Title}\n{article.Body}",
            article.Id,
            article))
        .ToArray(),
    topN: 2);

var results = await reranker.RerankAsync(request);

Console.WriteLine("Initial candidate order:");
foreach (var article in candidates)
{
    Console.WriteLine($"- {article.Id}: {article.Title}");
}

Console.WriteLine();
Console.WriteLine("ONNX reranked order:");
foreach (var result in results)
{
    var article = (Article)result.Document.Metadata!;
    Console.WriteLine($"- {article.Id}: {article.Title} (score: {result.Score:F4})");
}

public sealed record Article(string Id, string Title, string Body);

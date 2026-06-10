using RedisVL.Vectorizers.Onnx;

namespace RedisVL.Tests.Vectorizers;

public sealed class OnnxTextVectorizerSmokeTests
{
    [OnnxVectorizerIntegrationFact]
    public async Task VectorizeAsync_WithLocalOnnxAssets_ProducesNormalizedSemanticEmbeddings()
    {
        using var vectorizer = new OnnxTextVectorizer(
            new OnnxVectorizerOptions
            {
                ModelPath = OnnxVectorizerTestEnvironment.ModelPath!,
                TokenizerPath = OnnxVectorizerTestEnvironment.TokenizerPath!,
                MaxSequenceLength = 256
            });

        var embeddings = await vectorizer.VectorizeAsync(
            [
                "how do i reset my password",
                "steps to recover a forgotten account password",
                "today's lunch menu in the cafeteria"
            ]);

        Assert.Equal(3, embeddings.Count);

        var dimension = embeddings[0].Length;
        Assert.True(dimension > 0);
        Assert.All(embeddings, embedding => Assert.Equal(dimension, embedding.Length));

        // Default options L2-normalize, so each embedding should be a unit vector.
        Assert.All(embeddings, embedding =>
        {
            var magnitude = Math.Sqrt(embedding.Sum(value => (double)value * value));
            Assert.Equal(1d, magnitude, precision: 3);
        });

        // The two password prompts should be closer to each other than to the lunch prompt.
        var passwordSimilarity = CosineSimilarity(embeddings[0], embeddings[1]);
        var unrelatedSimilarity = CosineSimilarity(embeddings[0], embeddings[2]);
        Assert.True(
            passwordSimilarity > unrelatedSimilarity,
            $"Expected related prompts to be more similar ({passwordSimilarity}) than unrelated prompts ({unrelatedSimilarity}).");
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        var dot = 0d;
        for (var index = 0; index < left.Length; index++)
        {
            dot += (double)left[index] * right[index];
        }

        return dot;
    }
}

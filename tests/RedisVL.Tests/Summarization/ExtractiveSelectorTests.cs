using RedisVL.Summarization;
using RedisVL.Vectorizers;

namespace RedisVL.Tests.Summarization;

public sealed class ExtractiveSelectorTests
{
    // Deterministic vectorizer: each sentence maps to a one-hot "topic" vector based on a keyword.
    private sealed class TopicVectorizer : IBatchTextVectorizer
    {
        public Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default) =>
            Task.FromResult(ToVector(input));

        public Task<IReadOnlyList<float[]>> VectorizeAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(ToVector).ToList());

        private static float[] ToVector(string text) =>
        [
            text.Contains("alpha", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
            text.Contains("beta", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
            text.Contains("gamma", StringComparison.OrdinalIgnoreCase) ? 1f : 0f
        ];
    }

    private static ExtractiveSelector CreateSelector(int defaultNumSentences = 10) =>
        new(new TopicVectorizer(), defaultNumSentences, maxIterations: 100, randomSeed: 42);

    [Fact]
    public async Task SelectsOneRepresentativePerTopicInOriginalOrder()
    {
        var selector = CreateSelector();
        var sentences = new[]
        {
            "The alpha report is ready.",
            "Beta metrics improved.",
            "Another alpha note here.",
            "Gamma launch was successful.",
            "Beta numbers look good."
        };

        var selected = await selector.SelectKeySentencesAsync(sentences, 3);

        // One sentence per distinct topic, earliest member of each, in original order.
        Assert.Equal(
            ["The alpha report is ready.", "Beta metrics improved.", "Gamma launch was successful."],
            selected);
    }

    [Fact]
    public async Task ReturnsAllWhenFewerSentencesThanK()
    {
        var selector = CreateSelector();

        var selected = await selector.SelectKeySentencesAsync(["Only one sentence."], 5);

        Assert.Equal(["Only one sentence."], selected);
    }

    [Fact]
    public async Task ReturnsEmptyForEmptyInput()
    {
        var selector = CreateSelector();

        Assert.Empty(await selector.SelectKeySentencesAsync([], 5));
    }

    [Fact]
    public async Task IgnoresBlankSentences()
    {
        var selector = CreateSelector();
        var sentences = new[] { "The alpha report is ready.", "   ", "", "Beta metrics improved." };

        var selected = await selector.SelectKeySentencesAsync(sentences, 5);

        Assert.Equal(["The alpha report is ready.", "Beta metrics improved."], selected);
    }

    [Fact]
    public async Task UsesDefaultCountWhenNotSpecified()
    {
        var selector = CreateSelector(defaultNumSentences: 2);
        var sentences = new[]
        {
            "The alpha report is ready.",
            "Beta metrics improved.",
            "Gamma launch was successful."
        };

        var selected = await selector.SelectKeySentencesAsync(sentences);

        Assert.Equal(2, selected.Count);
        Assert.All(selected, sentence => Assert.Contains(sentence, sentences));
    }

    [Fact]
    public void RejectsNullEmbedder()
    {
        Assert.Throws<ArgumentNullException>(() => new ExtractiveSelector(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveDefaultSentenceCount(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractiveSelector(new TopicVectorizer(), count));
    }

    [Fact]
    public async Task RejectsNonPositiveK()
    {
        var selector = CreateSelector();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => selector.SelectKeySentencesAsync(["The alpha report is ready."], 0));
    }
}

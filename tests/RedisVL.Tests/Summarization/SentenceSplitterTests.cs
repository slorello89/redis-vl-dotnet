using RedisVL.Summarization;

namespace RedisVL.Tests.Summarization;

public sealed class SentenceSplitterTests
{
    private readonly SentenceSplitter _splitter = new();

    [Fact]
    public void SplitsBasicSentences()
    {
        var sentences = _splitter.Split("This is sentence one. This is sentence two! Is this sentence three?");

        Assert.Equal(
            ["This is sentence one.", "This is sentence two!", "Is this sentence three?"],
            sentences);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ReturnsEmptyForBlankInput(string? text)
    {
        Assert.Empty(_splitter.Split(text!));
    }

    [Fact]
    public void DoesNotSplitOnAbbreviationsOrInitials()
    {
        var sentences = _splitter.Split("Dr. Smith met Mr. J. Loris today. They spoke at length.");

        Assert.Equal(
            ["Dr. Smith met Mr. J. Loris today.", "They spoke at length."],
            sentences);
    }

    [Fact]
    public void DoesNotSplitOnDecimalNumbers()
    {
        var sentences = _splitter.Split("Pi is about 3.14 in value. That is useful.");

        Assert.Equal(
            ["Pi is about 3.14 in value.", "That is useful."],
            sentences);
    }

    [Fact]
    public void HandlesEllipsisAndCombinedPunctuation()
    {
        var sentences = _splitter.Split("Wait... what?! Really.");

        Assert.Equal(
            ["Wait...", "what?!", "Really."],
            sentences);
    }

    [Fact]
    public void ReturnsSingleSentenceWhenNoTerminator()
    {
        var sentences = _splitter.Split("a trailing fragment without punctuation");

        Assert.Equal(["a trailing fragment without punctuation"], sentences);
    }
}

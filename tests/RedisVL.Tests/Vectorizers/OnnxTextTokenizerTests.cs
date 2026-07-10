using RedisVL.Vectorizers.Onnx.Internal;

namespace RedisVL.Tests.Vectorizers;

public sealed class OnnxTextTokenizerTests
{
    private const string TokenizerJson =
        """
        {
          "model": {
            "type": "WordPiece",
            "unk_token": "[UNK]",
            "continuing_subword_prefix": "##",
            "max_input_chars_per_word": 100,
            "vocab": {
              "[PAD]": 0,
              "[UNK]": 1,
              "[CLS]": 2,
              "[SEP]": 3,
              "hello": 4,
              "world": 5
            }
          },
          "normalizer": {
            "type": "BertNormalizer",
            "lowercase": true,
            "clean_text": true,
            "handle_chinese_chars": true,
            "strip_accents": null
          }
        }
        """;

    [Fact]
    public void Encode_WrapsWithClsAndSep_AndZeroesTokenTypes()
    {
        using var fixture = TokenizerFixture.Create(TokenizerJson);
        var tokenizer = new BertTokenizerJsonTextTokenizer(fixture.Path);

        var encoded = tokenizer.Encode("Hello world", maxSequenceLength: 16);

        Assert.Equal([2L, 4L, 5L, 3L], encoded.InputIds);
        Assert.Equal([1L, 1L, 1L, 1L], encoded.AttentionMask);
        Assert.Equal([0L, 0L, 0L, 0L], encoded.TokenTypeIds);
    }

    [Theory]
    [InlineData("hello\nworld")]
    [InlineData("hello\tworld")]
    [InlineData("hello\rworld")]
    [InlineData("hello\r\nworld")]
    public void Encode_TreatsTabsAndNewlinesAsWhitespace(string text)
    {
        using var fixture = TokenizerFixture.Create(TokenizerJson);
        var tokenizer = new BertTokenizerJsonTextTokenizer(fixture.Path);

        var encoded = tokenizer.Encode(text, maxSequenceLength: 16);

        // \t, \n and \r must split words like a space (matching the BERT
        // reference tokenizer) rather than being dropped as control characters,
        // which would otherwise merge "hello" and "world" into one [UNK] token.
        Assert.Equal([2L, 4L, 5L, 3L], encoded.InputIds);
    }

    [Fact]
    public void Encode_MapsOutOfVocabularyWordsToUnknownToken()
    {
        using var fixture = TokenizerFixture.Create(TokenizerJson);
        var tokenizer = new BertTokenizerJsonTextTokenizer(fixture.Path);

        var encoded = tokenizer.Encode("zzz", maxSequenceLength: 16);

        Assert.Equal([2L, 1L, 3L], encoded.InputIds);
    }

    [Fact]
    public void Encode_TruncatesBodyToFitMaxSequenceLength()
    {
        using var fixture = TokenizerFixture.Create(TokenizerJson);
        var tokenizer = new BertTokenizerJsonTextTokenizer(fixture.Path);

        // maxSequenceLength 3 leaves room for one body token between [CLS] and [SEP].
        var encoded = tokenizer.Encode("hello world", maxSequenceLength: 3);

        Assert.Equal([2L, 4L, 3L], encoded.InputIds);
    }

    [Fact]
    public void Encode_WithMaxSequenceLengthBelowTwo_Throws()
    {
        using var fixture = TokenizerFixture.Create(TokenizerJson);
        var tokenizer = new BertTokenizerJsonTextTokenizer(fixture.Path);

        Assert.Throws<ArgumentOutOfRangeException>(() => tokenizer.Encode("hello", maxSequenceLength: 1));
    }

    private sealed class TokenizerFixture : IDisposable
    {
        private TokenizerFixture(string path) => Path = path;

        public string Path { get; }

        public static TokenizerFixture Create(string json)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
            File.WriteAllText(path, json);
            return new TokenizerFixture(path);
        }

        public void Dispose() => File.Delete(Path);
    }
}

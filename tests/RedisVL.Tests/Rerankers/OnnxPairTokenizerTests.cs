using RedisVL.Rerankers.Onnx.Internal;

namespace RedisVL.Tests.Rerankers;

public sealed class OnnxPairTokenizerTests
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
    public void Encode_WrapsQueryAndDocumentWithClsAndSep()
    {
        using var fixture = TokenizerFixture.Create(TokenizerJson);
        var tokenizer = new BertTokenizerJsonPairTokenizer(fixture.Path);

        var encoded = tokenizer.Encode("hello world", "hello world", maxSequenceLength: 16);

        Assert.Equal([2L, 4L, 5L, 3L, 4L, 5L, 3L], encoded.InputIds);
        Assert.Equal([1L, 1L, 1L, 1L, 1L, 1L, 1L], encoded.AttentionMask);
        Assert.Equal([0L, 0L, 0L, 0L, 1L, 1L, 1L], encoded.TokenTypeIds);
    }

    [Theory]
    [InlineData("hello\nworld")]
    [InlineData("hello\tworld")]
    [InlineData("hello\rworld")]
    [InlineData("hello\r\nworld")]
    public void Encode_TreatsTabsAndNewlinesAsWhitespace(string text)
    {
        using var fixture = TokenizerFixture.Create(TokenizerJson);
        var tokenizer = new BertTokenizerJsonPairTokenizer(fixture.Path);

        var encoded = tokenizer.Encode(text, text, maxSequenceLength: 16);

        // \t, \n and \r must split words like a space (matching the BERT
        // reference tokenizer) rather than being dropped as control characters,
        // which would otherwise merge "hello" and "world" into one [UNK] token.
        Assert.Equal([2L, 4L, 5L, 3L, 4L, 5L, 3L], encoded.InputIds);
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

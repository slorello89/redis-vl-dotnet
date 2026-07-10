using System.Globalization;
using System.Text;
using System.Text.Json;

namespace RedisVL.Vectorizers.Onnx.Internal;

internal sealed class BertTokenizerJsonTextTokenizer : IOnnxTextTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly string _continuingSubwordPrefix;
    private readonly string _unknownToken;
    private readonly int _maxInputCharsPerWord;
    private readonly bool _cleanText;
    private readonly bool _handleChineseChars;
    private readonly bool _lowercase;
    private readonly bool _stripAccents;
    private readonly int _clsTokenId;
    private readonly int _sepTokenId;
    private readonly int _unknownTokenId;

    public BertTokenizerJsonTextTokenizer(string tokenizerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenizerPath);

        using var document = JsonDocument.Parse(File.ReadAllText(tokenizerPath));
        var root = document.RootElement;

        var model = root.GetProperty("model");
        if (!string.Equals(model.GetProperty("type").GetString(), "WordPiece", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only tokenizer.json files with a WordPiece model are supported.");
        }

        _vocab = model.GetProperty("vocab")
            .EnumerateObject()
            .ToDictionary(static property => property.Name, static property => property.Value.GetInt32(), StringComparer.Ordinal);

        _continuingSubwordPrefix = model.TryGetProperty("continuing_subword_prefix", out var continuingSubwordPrefix)
            ? continuingSubwordPrefix.GetString() ?? "##"
            : "##";
        _unknownToken = model.TryGetProperty("unk_token", out var unknownToken)
            ? unknownToken.GetString() ?? "[UNK]"
            : "[UNK]";
        _maxInputCharsPerWord = model.TryGetProperty("max_input_chars_per_word", out var maxInputCharsPerWord)
            ? maxInputCharsPerWord.GetInt32()
            : 100;

        var normalizer = root.TryGetProperty("normalizer", out var normalizerElement) ? normalizerElement : default;
        _cleanText = normalizer.ValueKind is JsonValueKind.Object &&
            normalizer.TryGetProperty("clean_text", out var cleanText) &&
            cleanText.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? cleanText.GetBoolean()
            : true;
        _handleChineseChars = normalizer.ValueKind is JsonValueKind.Object &&
            normalizer.TryGetProperty("handle_chinese_chars", out var handleChineseChars) &&
            handleChineseChars.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? handleChineseChars.GetBoolean()
            : true;
        _lowercase = normalizer.ValueKind is JsonValueKind.Object &&
            normalizer.TryGetProperty("lowercase", out var lowercase) &&
            lowercase.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? lowercase.GetBoolean()
            : false;
        _stripAccents = ResolveStripAccents(normalizer, _lowercase);

        _clsTokenId = GetRequiredTokenId("[CLS]");
        _sepTokenId = GetRequiredTokenId("[SEP]");
        _unknownTokenId = GetRequiredTokenId(_unknownToken);
    }

    public EncodedOnnxInput Encode(string text, int maxSequenceLength)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (maxSequenceLength < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSequenceLength), "MaxSequenceLength must be at least 2.");
        }

        var tokenIds = TokenizeToIds(text);

        // Reserve room for the leading [CLS] and trailing [SEP] tokens.
        var maxBodyLength = maxSequenceLength - 2;
        if (tokenIds.Count > maxBodyLength)
        {
            tokenIds.RemoveRange(maxBodyLength, tokenIds.Count - maxBodyLength);
        }

        var inputIds = new long[tokenIds.Count + 2];
        var attentionMask = new long[inputIds.Length];
        var tokenTypeIds = new long[inputIds.Length];

        var cursor = 0;
        inputIds[cursor] = _clsTokenId;
        attentionMask[cursor] = 1;
        cursor++;

        for (var index = 0; index < tokenIds.Count; index++, cursor++)
        {
            inputIds[cursor] = tokenIds[index];
            attentionMask[cursor] = 1;
        }

        inputIds[cursor] = _sepTokenId;
        attentionMask[cursor] = 1;

        return new EncodedOnnxInput(inputIds, attentionMask, tokenTypeIds);
    }

    private List<int> TokenizeToIds(string text)
    {
        var normalized = Normalize(text);
        var tokens = PreTokenize(normalized);
        var tokenIds = new List<int>(tokens.Count * 2);

        foreach (var token in tokens)
        {
            tokenIds.AddRange(TokenizeWordPiece(token));
        }

        return tokenIds;
    }

    private string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length + 16);

        foreach (var character in text)
        {
            if (_cleanText && IsInvalidCodePoint(character))
            {
                continue;
            }

            if (_handleChineseChars && IsChineseCharacter(character))
            {
                builder.Append(' ');
                builder.Append(character);
                builder.Append(' ');
                continue;
            }

            builder.Append(char.IsWhiteSpace(character) ? ' ' : character);
        }

        var normalized = builder.ToString();
        if (_lowercase)
        {
            normalized = normalized.ToLowerInvariant();
        }

        if (_stripAccents)
        {
            normalized = StripAccents(normalized);
        }

        return normalized;
    }

    private List<string> PreTokenize(string text)
    {
        var tokens = new List<string>();
        var builder = new StringBuilder();

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                FlushToken(builder, tokens);
                continue;
            }

            if (IsPunctuation(character))
            {
                FlushToken(builder, tokens);
                tokens.Add(character.ToString());
                continue;
            }

            builder.Append(character);
        }

        FlushToken(builder, tokens);
        return tokens;
    }

    private IEnumerable<int> TokenizeWordPiece(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return [];
        }

        if (token.Length > _maxInputCharsPerWord)
        {
            return [_unknownTokenId];
        }

        var tokenIds = new List<int>();
        var start = 0;
        while (start < token.Length)
        {
            int? matchedTokenId = null;
            var end = token.Length;

            while (start < end)
            {
                var piece = start == 0
                    ? token[start..end]
                    : string.Concat(_continuingSubwordPrefix, token[start..end]);

                if (_vocab.TryGetValue(piece, out var pieceId))
                {
                    matchedTokenId = pieceId;
                    break;
                }

                end--;
            }

            if (matchedTokenId is null)
            {
                return [_unknownTokenId];
            }

            tokenIds.Add(matchedTokenId.Value);
            start = end;
        }

        return tokenIds;
    }

    private int GetRequiredTokenId(string token)
    {
        if (_vocab.TryGetValue(token, out var id))
        {
            return id;
        }

        throw new InvalidOperationException($"The tokenizer vocabulary does not contain the required token '{token}'.");
    }

    private static bool ResolveStripAccents(JsonElement normalizer, bool lowercase)
    {
        if (normalizer.ValueKind is not JsonValueKind.Object ||
            !normalizer.TryGetProperty("strip_accents", out var stripAccents) ||
            stripAccents.ValueKind == JsonValueKind.Null)
        {
            return lowercase;
        }

        return stripAccents.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => lowercase
        };
    }

    private static bool IsInvalidCodePoint(char character)
    {
        // Tab, newline and carriage return are technically control characters but
        // the BERT reference tokenizer treats them as whitespace, so they must
        // survive clean_text and get normalized to a space below rather than being
        // deleted (which would merge words across line breaks).
        if (character is '\t' or '\n' or '\r')
        {
            return false;
        }

        return character == '\0' || character == '�' || char.GetUnicodeCategory(character) == UnicodeCategory.Control;
    }

    private static bool IsChineseCharacter(char character) =>
        character is >= '一' and <= '鿿' or
            >= '㐀' and <= '䶿' or
            >= '豈' and <= '﫿';

    private static bool IsPunctuation(char character)
    {
        var category = char.GetUnicodeCategory(character);
        return category is UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.DashPunctuation or
            UnicodeCategory.OpenPunctuation or
            UnicodeCategory.ClosePunctuation or
            UnicodeCategory.InitialQuotePunctuation or
            UnicodeCategory.FinalQuotePunctuation or
            UnicodeCategory.OtherPunctuation or
            UnicodeCategory.MathSymbol or
            UnicodeCategory.CurrencySymbol or
            UnicodeCategory.ModifierSymbol or
            UnicodeCategory.OtherSymbol;
    }

    private static string StripAccents(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (char.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static void FlushToken(StringBuilder builder, ICollection<string> tokens)
    {
        if (builder.Length == 0)
        {
            return;
        }

        tokens.Add(builder.ToString());
        builder.Clear();
    }
}

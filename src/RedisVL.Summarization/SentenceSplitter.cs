using System.Text.RegularExpressions;

namespace RedisVL.Summarization;

/// <summary>
/// Lightweight, rule-based sentence splitter. It splits on sentence-terminating punctuation
/// (<c>.</c>, <c>!</c>, <c>?</c>) followed by whitespace, while suppressing splits after common
/// abbreviations, single-letter initials, and decimal numbers.
/// </summary>
/// <remarks>
/// This is intentionally dependency-free — it does not load an NLP model. It handles the common
/// English cases well, but it is a heuristic, not a statistical sentence detector. For specialized
/// text (heavy abbreviation use, non-English content) supply pre-split sentences instead.
/// </remarks>
public sealed class SentenceSplitter
{
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "mr", "mrs", "ms", "dr", "prof", "sr", "jr", "st", "vs", "etc", "al",
        "inc", "ltd", "co", "corp", "dept", "fig", "no", "vol", "pp",
        "jan", "feb", "mar", "apr", "jun", "jul", "aug", "sep", "sept", "oct", "nov", "dec",
        "e.g", "i.e", "a.m", "p.m", "u.s", "u.k"
    };

    private static readonly Regex BoundaryCandidate = new(@"([.!?]+)(\s+)", RegexOptions.Compiled);

    /// <summary>Splits <paramref name="text"/> into sentences, preserving the original text exactly.</summary>
    /// <param name="text">The text to split.</param>
    /// <returns>The detected sentences, trimmed of surrounding whitespace.</returns>
    public IReadOnlyList<string> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var sentences = new List<string>();
        var start = 0;

        foreach (Match match in BoundaryCandidate.Matches(text))
        {
            var punctuation = match.Groups[1].Value;
            if (!ShouldSplit(text, match.Index, punctuation))
            {
                continue;
            }

            var terminatorEnd = match.Index + punctuation.Length;
            var sentence = text[start..terminatorEnd].Trim();
            if (sentence.Length > 0)
            {
                sentences.Add(sentence);
            }

            start = match.Index + match.Length;
        }

        var tail = text[start..].Trim();
        if (tail.Length > 0)
        {
            sentences.Add(tail);
        }

        return sentences;
    }

    private static bool ShouldSplit(string text, int punctuationIndex, string punctuation)
    {
        // '!' and '?' unambiguously terminate a sentence.
        if (punctuation.IndexOf('!') >= 0 || punctuation.IndexOf('?') >= 0)
        {
            return true;
        }

        // A lone period: suppress the split after a known abbreviation or a single-letter initial.
        var word = PrecedingWord(text, punctuationIndex);
        if (word.Length == 1 && char.IsUpper(word[0]))
        {
            return false;
        }

        return !Abbreviations.Contains(word);
    }

    private static string PrecedingWord(string text, int punctuationIndex)
    {
        var index = punctuationIndex - 1;
        while (index >= 0 && (char.IsLetterOrDigit(text[index]) || text[index] == '.'))
        {
            index--;
        }

        return text[(index + 1)..punctuationIndex];
    }
}

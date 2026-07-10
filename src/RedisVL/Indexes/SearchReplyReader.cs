using StackExchange.Redis;

namespace RedisVL.Indexes;

/// <summary>
/// Helpers for reading the map-shaped replies that RediSearch returns under RESP3.
/// </summary>
/// <remarks>
/// Under RESP2, <c>FT.SEARCH</c> / <c>FT.AGGREGATE</c> return a flat array
/// (<c>[total, id, [field, value, ...], ...]</c>). Under RESP3 the same commands return a map with
/// <c>total_results</c> and <c>results</c> entries, where each row is itself a map exposing
/// <c>id</c> and <c>extra_attributes</c>. StackExchange.Redis surfaces a RESP3 map as a
/// <see cref="RedisResult" /> whose <see cref="RedisResult.Resp3Type" /> is
/// <see cref="ResultType.Map" /> and which, when cast to <c>RedisResult[]</c>, flattens to
/// alternating key/value entries. <see cref="IsMapReply" /> lets the parsers branch on the reply
/// shape so both protocols are supported without pinning the connection to RESP2.
/// </remarks>
internal static class SearchReplyReader
{
    public const string TotalResultsKey = "total_results";
    public const string ResultsKey = "results";
    public const string IdKey = "id";
    public const string ExtraAttributesKey = "extra_attributes";

    /// <summary>
    /// Returns <see langword="true" /> when <paramref name="result" /> is a RESP3 map reply.
    /// </summary>
    public static bool IsMapReply(RedisResult result) => result.Resp3Type == ResultType.Map;

    /// <summary>
    /// Flattens a map-shaped reply (or a RESP2 alternating key/value array) into a dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, RedisResult> ToMap(RedisResult result)
    {
        var entries = (RedisResult[])result!;
        var map = new Dictionary<string, RedisResult>(Math.Max(entries.Length / 2, 0), StringComparer.Ordinal);
        for (var index = 0; index + 1 < entries.Length; index += 2)
        {
            var key = entries[index].ToString();
            if (!string.IsNullOrEmpty(key))
            {
                map[key] = entries[index + 1];
            }
        }

        return map;
    }

    /// <summary>
    /// Reads the <c>total_results</c> entry from a RESP3 reply map, defaulting to <c>0</c>.
    /// </summary>
    public static long ReadTotalCount(IReadOnlyDictionary<string, RedisResult> map) =>
        map.TryGetValue(TotalResultsKey, out var value) && !value.IsNull ? (long)value : 0;

    /// <summary>
    /// Returns the <c>results</c> rows from a RESP3 reply map, or an empty array when absent/null.
    /// </summary>
    public static RedisResult[] ReadRows(IReadOnlyDictionary<string, RedisResult> map) =>
        map.TryGetValue(ResultsKey, out var results) && !results.IsNull
            ? (RedisResult[])results!
            : [];

    /// <summary>
    /// Extracts the field/value payload from a <c>FT.SEARCH</c>/<c>FT.AGGREGATE</c> result row. Under
    /// RESP3 such a row is a map whose fields live under <c>extra_attributes</c>; its sibling keys
    /// (<c>id</c>, <c>values</c>) are row metadata, not fields, so a row-map without
    /// <c>extra_attributes</c> carries no fields. Over RESP2 the row is already the flat field/value
    /// array. (<c>FT.HYBRID</c> rows use no envelope and are parsed directly, not via this method.)
    /// </summary>
    public static RedisResult ExtractEnvelopedRowFields(RedisResult row)
    {
        if (IsMapReply(row))
        {
            return ToMap(row).TryGetValue(ExtraAttributesKey, out var attributes)
                ? attributes
                : RedisResult.Create(Array.Empty<RedisResult>());
        }

        return row;
    }
}

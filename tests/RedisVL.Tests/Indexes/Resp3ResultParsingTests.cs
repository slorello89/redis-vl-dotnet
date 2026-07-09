using RedisVL.Indexes;
using RedisVL.Queries;
using StackExchange.Redis;

namespace RedisVL.Tests.Indexes;

/// <summary>
/// Unit coverage for parsing the map-shaped replies RediSearch returns under RESP3. The fixtures
/// mirror the exact <see cref="RedisResult" /> shape StackExchange.Redis surfaces on a
/// <see cref="RedisProtocol.Resp3" /> connection (verified against Redis 8.x): the top-level reply
/// and each result row are <see cref="ResultType.Map" />, and rows expose their fields under an
/// <c>extra_attributes</c> map.
/// </summary>
public sealed class Resp3ResultParsingTests
{
    [Fact]
    public void SearchResultsParser_ParsesResp3MapReply()
    {
        var reply = Map(
            ("attributes", RedisResult.Create(Array.Empty<RedisResult>())),
            ("format", Str("STRING")),
            ("results", RedisResult.Create(
            [
                SearchRow("doc:1", ("title", "hello world"), ("price", "10")),
                SearchRow("doc:2", ("title", "hello there"), ("price", "20")),
            ])),
            ("total_results", RedisResult.Create(2)),
            ("warning", RedisResult.Create(Array.Empty<RedisResult>())));

        var results = SearchResultsParser.Parse(reply);

        Assert.Equal(2, results.TotalCount);
        Assert.Equal(["doc:1", "doc:2"], results.Documents.Select(static d => d.Id).ToArray());
        Assert.Equal("hello world", results.Documents[0].Values["title"].ToString());
        Assert.Equal("10", results.Documents[0].Values["price"].ToString());
        Assert.Equal("20", results.Documents[1].Values["price"].ToString());
    }

    [Fact]
    public void SearchResultsParser_ParsesResp3CountReply_WithNoRows()
    {
        // FT.SEARCH ... NOCONTENT LIMIT 0 0 (the CountAsync shape): total_results is populated
        // while results is empty.
        var reply = Map(
            ("attributes", RedisResult.Create(Array.Empty<RedisResult>())),
            ("results", RedisResult.Create(Array.Empty<RedisResult>())),
            ("total_results", RedisResult.Create(5)));

        var results = SearchResultsParser.Parse(reply);

        Assert.Equal(5, results.TotalCount);
        Assert.Empty(results.Documents);
    }

    [Fact]
    public void AggregationResultsParser_ParsesResp3MapReply()
    {
        var reply = Map(
            ("attributes", RedisResult.Create(Array.Empty<RedisResult>())),
            ("results", RedisResult.Create(
            [
                AggregateRow(("title", "hello world"), ("price", "10")),
                AggregateRow(("title", "hello there"), ("price", "20")),
            ])),
            ("total_results", RedisResult.Create(2)));

        var results = AggregationResultsParser.Parse(reply);

        Assert.Equal(2, results.TotalCount);
        Assert.Equal(2, results.Rows.Count);
        Assert.Equal("hello world", results.Rows[0].Values["title"].ToString());
        Assert.Equal("20", results.Rows[1].Values["price"].ToString());
    }

    [Fact]
    public void HybridSearchResultsParser_ParsesResp3MapReplyWithFlatRows()
    {
        // FT.HYBRID keeps flat field/value rows even under RESP3, but the top-level reply is a map.
        var reply = Map(
            ("total_results", RedisResult.Create(1)),
            ("results", RedisResult.Create(
            [
                RedisResult.Create(
                [
                    Str(HybridSearchQuery.KeyField),
                    Str("doc:1"),
                    Str(HybridSearchQuery.ScoreField),
                    Str("0.5"),
                    Str("title"),
                    Str("hello world"),
                ]),
            ])),
            ("warnings", RedisResult.Create(Array.Empty<RedisResult>())));

        var results = HybridSearchResultsParser.Parse(reply);

        Assert.Equal(1, results.TotalCount);
        var document = Assert.Single(results.Documents);
        Assert.Equal("doc:1", document.Id);
        Assert.Equal("hello world", document.Values["title"].ToString());
        Assert.False(document.Values.ContainsKey(HybridSearchQuery.KeyField));
    }

    private static RedisResult Str(string value) => RedisResult.Create((RedisValue)value);

    private static RedisResult Map(params (string Key, RedisResult Value)[] entries)
    {
        var flattened = new List<RedisResult>(entries.Length * 2);
        foreach (var (key, value) in entries)
        {
            flattened.Add(Str(key));
            flattened.Add(value);
        }

        return RedisResult.Create(flattened.ToArray(), ResultType.Map);
    }

    private static RedisResult SearchRow(string id, params (string Field, string Value)[] fields) =>
        Map(
            ("id", Str(id)),
            ("extra_attributes", FieldMap(fields)),
            ("values", RedisResult.Create(Array.Empty<RedisResult>())));

    private static RedisResult AggregateRow(params (string Field, string Value)[] fields) =>
        Map(
            ("extra_attributes", FieldMap(fields)),
            ("values", RedisResult.Create(Array.Empty<RedisResult>())));

    private static RedisResult FieldMap((string Field, string Value)[] fields) =>
        Map(fields.Select(static field => (field.Field, Str(field.Value))).ToArray());
}

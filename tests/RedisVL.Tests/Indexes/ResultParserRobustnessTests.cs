using RedisVL.Indexes;
using RedisVL.Queries;
using StackExchange.Redis;

namespace RedisVL.Tests.Indexes;

/// <summary>
/// Negative and edge-case coverage for the three RediSearch result parsers. The happy-path RESP2
/// and RESP3 shapes are covered in <see cref="Resp3ResultParsingTests" /> and the command-builder
/// suites; this file exercises the inputs those don't: nil replies, empty arrays, malformed
/// odd-length field lists, and rows missing required keys. A malformed server reply must degrade
/// predictably (drop the dangling data or throw a descriptive error) rather than surface an opaque
/// <see cref="IndexOutOfRangeException" />.
/// </summary>
public sealed class ResultParserRobustnessTests
{
    // ---- SearchResultsParser (RESP2 flat array) ----

    [Fact]
    public void SearchResultsParser_NilReply_ReturnsEmpty()
    {
        var results = SearchResultsParser.Parse(RedisResult.Create(RedisValue.Null));

        Assert.Equal(0, results.TotalCount);
        Assert.Empty(results.Documents);
    }

    [Fact]
    public void SearchResultsParser_EmptyArray_ReturnsEmpty()
    {
        var results = SearchResultsParser.Parse(Arr());

        Assert.Equal(0, results.TotalCount);
        Assert.Empty(results.Documents);
    }

    [Fact]
    public void SearchResultsParser_ParsesResp2FlatArray()
    {
        // [total, id1, [field, value, ...], id2, [...]]
        var reply = Arr(
            Int(2),
            Str("doc:1"), Arr(Str("title"), Str("hello world")),
            Str("doc:2"), Arr(Str("title"), Str("hello there")));

        var results = SearchResultsParser.Parse(reply);

        Assert.Equal(2, results.TotalCount);
        Assert.Equal(["doc:1", "doc:2"], results.Documents.Select(static d => d.Id).ToArray());
        Assert.Equal("hello world", results.Documents[0].Values["title"].ToString());
    }

    [Fact]
    public void SearchResultsParser_TruncatedReply_DropsDanglingIdWithoutThrowing()
    {
        // A trailing document id with no field payload (even-length array) must not throw; the
        // dangling id is dropped and the well-formed rows survive.
        var reply = Arr(
            Int(2),
            Str("doc:1"), Arr(Str("title"), Str("hello world")),
            Str("doc:2"));

        var results = SearchResultsParser.Parse(reply);

        Assert.Equal(2, results.TotalCount);
        var document = Assert.Single(results.Documents);
        Assert.Equal("doc:1", document.Id);
    }

    [Fact]
    public void SearchResultsParser_OddLengthFieldList_DropsDanglingField()
    {
        // A field list with a dangling key and no value must drop the dangling key rather than throw.
        var reply = Arr(
            Int(1),
            Str("doc:1"), Arr(Str("title"), Str("hello world"), Str("orphan")));

        var results = SearchResultsParser.Parse(reply);

        var document = Assert.Single(results.Documents);
        Assert.Equal("hello world", document.Values["title"].ToString());
        Assert.False(document.Values.ContainsKey("orphan"));
    }

    // ---- AggregationResultsParser (RESP2 flat array) ----

    [Fact]
    public void AggregationResultsParser_NilReply_ReturnsEmpty()
    {
        var results = AggregationResultsParser.Parse(RedisResult.Create(RedisValue.Null));

        Assert.Equal(0, results.TotalCount);
        Assert.Empty(results.Rows);
    }

    [Fact]
    public void AggregationResultsParser_ParsesResp2FlatArray()
    {
        // [total, [field, value, ...], [field, value, ...]]
        var reply = Arr(
            Int(2),
            Arr(Str("genre"), Str("crime"), Str("count"), Str("3")),
            Arr(Str("genre"), Str("drama"), Str("count"), Str("5")));

        var results = AggregationResultsParser.Parse(reply);

        Assert.Equal(2, results.TotalCount);
        Assert.Equal(2, results.Rows.Count);
        Assert.Equal("crime", results.Rows[0].Values["genre"].ToString());
        Assert.Equal("5", results.Rows[1].Values["count"].ToString());
    }

    [Fact]
    public void AggregationResultsParser_OddLengthRow_DropsDanglingField()
    {
        var reply = Arr(
            Int(1),
            Arr(Str("genre"), Str("crime"), Str("orphan")));

        var results = AggregationResultsParser.Parse(reply);

        var row = Assert.Single(results.Rows);
        Assert.Equal("crime", row.Values["genre"].ToString());
        Assert.False(row.Values.ContainsKey("orphan"));
    }

    // ---- HybridSearchResultsParser (flat field/value rows, map envelope) ----

    [Fact]
    public void HybridSearchResultsParser_NilReply_ReturnsEmpty()
    {
        var results = HybridSearchResultsParser.Parse(RedisResult.Create(RedisValue.Null));

        Assert.Equal(0, results.TotalCount);
        Assert.Empty(results.Documents);
    }

    [Fact]
    public void HybridSearchResultsParser_NilResultsEntry_ReturnsEmptyDocuments()
    {
        var reply = Arr(
            Str("total_results"), Int(0),
            Str("results"), RedisResult.Create(RedisValue.Null));

        var results = HybridSearchResultsParser.Parse(reply);

        Assert.Equal(0, results.TotalCount);
        Assert.Empty(results.Documents);
    }

    [Fact]
    public void HybridSearchResultsParser_RowMissingKeyField_Throws()
    {
        var reply = Arr(
            Str("total_results"), Int(1),
            Str("results"), Arr(
                Arr(Str(HybridSearchQuery.ScoreField), Str("0.5"), Str("title"), Str("hello world"))));

        var exception = Assert.Throws<InvalidOperationException>(() => HybridSearchResultsParser.Parse(reply));
        Assert.Contains(HybridSearchQuery.KeyField, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HybridSearchResultsParser_OddLengthRow_DropsDanglingFieldWithoutThrowing()
    {
        // The row still carries the key field, so it parses; the dangling trailing token is dropped.
        var reply = Arr(
            Str("total_results"), Int(1),
            Str("results"), Arr(
                Arr(Str(HybridSearchQuery.KeyField), Str("doc:1"), Str("title"), Str("hello world"), Str("orphan"))));

        var results = HybridSearchResultsParser.Parse(reply);

        var document = Assert.Single(results.Documents);
        Assert.Equal("doc:1", document.Id);
        Assert.Equal("hello world", document.Values["title"].ToString());
        Assert.False(document.Values.ContainsKey("orphan"));
    }

    private static RedisResult Str(string value) => RedisResult.Create((RedisValue)value);

    private static RedisResult Int(long value) => RedisResult.Create(value);

    private static RedisResult Arr(params RedisResult[] entries) => RedisResult.Create(entries);
}

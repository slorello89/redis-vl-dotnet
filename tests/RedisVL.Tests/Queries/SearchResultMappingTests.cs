using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using RedisVL.Queries;
using StackExchange.Redis;

namespace RedisVL.Tests.Queries;

public sealed class SearchResultMappingTests
{
    [Fact]
    public void MapsProjectedPrimitiveFieldsIntoTypedResults()
    {
        var results = new SearchResults(
            2,
            [
                new SearchDocument(
                    "movie:1",
                    new Dictionary<string, RedisValue>(StringComparer.Ordinal)
                    {
                        ["title"] = "Heat",
                        ["year"] = "1995",
                        ["distance"] = "0.125"
                    }),
                new SearchDocument(
                    "movie:2",
                    new Dictionary<string, RedisValue>(StringComparer.Ordinal)
                    {
                        ["title"] = "Thief",
                        ["year"] = "1981",
                        ["distance"] = "0.25"
                    })
            ]);

        var mapped = results.Map<MovieProjection>();

        Assert.Equal(2, mapped.TotalCount);
        Assert.Collection(
            mapped.Documents,
            document =>
            {
                Assert.Equal("movie:1", document.Id);
                Assert.Equal("Heat", document.Title);
                Assert.Equal(1995, document.Year);
                Assert.Equal(0.125d, document.Distance);
            },
            document =>
            {
                Assert.Equal("movie:2", document.Id);
                Assert.Equal("Thief", document.Title);
                Assert.Equal(1981, document.Year);
                Assert.Equal(0.25d, document.Distance);
            });
    }

    [Fact]
    public void MapsNestedJsonProjectionIntoTypedDocument()
    {
        var document = new SearchDocument(
            "movie:1",
            new Dictionary<string, RedisValue>(StringComparer.Ordinal)
            {
                ["title"] = "Heat",
                ["metadata"] = "{\"director\":\"Michael Mann\",\"runtimeMinutes\":170}"
            });

        var mapped = document.Map<NestedMovieProjection>();

        Assert.Equal("Heat", mapped.Title);
        Assert.Equal("Michael Mann", mapped.Metadata.Director);
        Assert.Equal(170, mapped.Metadata.RuntimeMinutes);
    }

    [Fact]
    public void MapsBinaryVectorProjectionIntoFloatArray()
    {
        var vector = new[] { 1.5f, 2.5f };
        var bytes = MemoryMarshal.AsBytes<float>(vector.AsSpan()).ToArray();
        var document = new SearchDocument(
            "movie:1",
            new Dictionary<string, RedisValue>(StringComparer.Ordinal)
            {
                ["embedding"] = bytes
            });

        var mapped = document.Map<VectorProjection>();

        Assert.Equal([1.5f, 2.5f], mapped.Embedding);
    }

    [Fact]
    public void ThrowsClearExceptionWhenRequiredProjectionFieldIsMissing()
    {
        var document = new SearchDocument(
            "movie:1",
            new Dictionary<string, RedisValue>(StringComparer.Ordinal)
            {
                ["title"] = "Heat"
            });

        var exception = Assert.Throws<SearchResultMappingException>(() => document.Map<MovieProjection>());

        Assert.Contains("Required field 'Year'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("movie:1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThrowsClearExceptionWhenProjectedValueIsIncompatible()
    {
        var document = new SearchDocument(
            "movie:1",
            new Dictionary<string, RedisValue>(StringComparer.Ordinal)
            {
                ["title"] = "Heat",
                ["year"] = "nineteen-ninety-five",
                ["distance"] = "0.1"
            });

        var exception = Assert.Throws<SearchResultMappingException>(() => document.Map<MovieProjection>());

        Assert.Contains("could not be mapped", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Year", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapsAggregationRowsIntoTypedResults()
    {
        var results = new AggregationResults(
            2,
            [
                new AggregationResultRow(
                    new Dictionary<string, RedisValue>(StringComparer.Ordinal)
                    {
                        ["genre"] = "crime",
                        ["movieCount"] = "2",
                        ["averageYear"] = "1988"
                    }),
                new AggregationResultRow(
                    new Dictionary<string, RedisValue>(StringComparer.Ordinal)
                    {
                        ["genre"] = "science-fiction",
                        ["movieCount"] = "1",
                        ["averageYear"] = "2016"
                    })
            ]);

        var mapped = results.Map<GenreAggregationRow>();

        Assert.Equal(2, mapped.TotalCount);
        Assert.Collection(
            mapped.Rows,
            row =>
            {
                Assert.Equal("crime", row.Genre);
                Assert.Equal(2, row.MovieCount);
                Assert.Equal(1988d, row.AverageYear);
            },
            row =>
            {
                Assert.Equal("science-fiction", row.Genre);
                Assert.Equal(1, row.MovieCount);
                Assert.Equal(2016d, row.AverageYear);
            });
    }

    [Fact]
    public void ThrowsClearExceptionWhenRequiredAggregationFieldIsMissing()
    {
        var row = new AggregationResultRow(
            new Dictionary<string, RedisValue>(StringComparer.Ordinal)
            {
                ["genre"] = "crime"
            });

        var exception = Assert.Throws<SearchResultMappingException>(() => row.Map<GenreAggregationRow>());

        Assert.Contains("Required field 'MovieCount'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("aggregation result", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record MovieProjection(
        string Id,
        string Title,
        int Year,
        [property: JsonPropertyName("distance")] double Distance);

    private sealed record NestedMovieProjection(string Title, MovieMetadata Metadata);

    private sealed record MovieMetadata(string Director, int RuntimeMinutes);

    private sealed record VectorProjection(float[] Embedding);

    private sealed record GenreAggregationRow(string Genre, int MovieCount, double AverageYear);
}

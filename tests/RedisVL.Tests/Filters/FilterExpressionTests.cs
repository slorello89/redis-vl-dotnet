using RedisVL.Filters;

namespace RedisVL.Tests.Filters;

public sealed class FilterExpressionTests
{
    [Fact]
    public void BuildsTagFilterWithEscapedValues()
    {
        var filter = Filter.Tag("genre").In("science fiction", "young-adult");

        Assert.Equal("@genre:{science\\ fiction|young\\-adult}", filter.ToQueryString());
    }

    [Fact]
    public void BuildsNumericFiltersWithInclusiveAndExclusiveBounds()
    {
        Assert.Equal("@rating:[4.5 9.25]", Filter.Numeric("rating").Between(4.5, 9.25).ToQueryString());
        Assert.Equal("@rating:[(4.5 +inf]", Filter.Numeric("rating").GreaterThan(4.5).ToQueryString());
        Assert.Equal("@rating:[-inf (9.25]", Filter.Numeric("rating").LessThan(9.25).ToQueryString());
    }

    [Fact]
    public void BuildsTextFiltersForTermsPhrasesAndPrefixes()
    {
        Assert.Equal("@title:neo\\-noir", Filter.Text("title").Match("neo-noir").ToQueryString());
        Assert.Equal("@title:\"hello \\\"redis\\\"\"", Filter.Text("title").Phrase("hello \"redis\"").ToQueryString());
        Assert.Equal("@title:vec*", Filter.Text("title").Prefix("vec").ToQueryString());
    }

    [Fact]
    public void BuildsGeoRadiusFilters()
    {
        var filter = Filter.Geo("location").WithinRadius(-73.9857, 40.7484, 5, GeoUnit.Kilometers);

        Assert.Equal("@location:[-73.9857 40.7484 5 km]", filter.ToQueryString());
    }

    [Fact]
    public void SupportsLogicalCompositionOperators()
    {
        var filter =
            Filter.Tag("genre").Eq("science fiction") &
            (Filter.Numeric("rating").GreaterThanOrEqualTo(8) | Filter.Text("title").Phrase("blade runner")) &
            !Filter.Geo("location").WithinRadius(-73.9857, 40.7484, 25, GeoUnit.Miles);

        Assert.Equal(
            "@genre:{science\\ fiction} (@rating:[8 +inf] | @title:\"blade runner\") -@location:[-73.9857 40.7484 25 mi]",
            filter.ToQueryString());
    }

    [Fact]
    public void RejectsInvalidFilterInputs()
    {
        Assert.Throws<ArgumentException>(() => Filter.Tag("genre").In());
        Assert.Throws<ArgumentException>(() => Filter.Numeric("rating").Between(10, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Filter.Geo("location").WithinRadius(1, 2, 0, GeoUnit.Meters));
        Assert.Throws<ArgumentException>(() => Filter.And(Filter.Tag("genre").Eq("science fiction")));
    }
}

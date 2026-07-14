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
    public void EscapesAsteriskInTermAndPrefixMatches()
    {
        // A '*' in an exact-term match is escaped so Match stays a literal term instead of a silent
        // prefix wildcard, and Prefix cannot emit an invalid double wildcard like `foo**`.
        Assert.Equal("@title:foo\\*", Filter.Text("title").Match("foo*").ToQueryString());
        Assert.Equal("@title:foo\\**", Filter.Text("title").Prefix("foo*").ToQueryString());

        // The ordinary prefix case still appends exactly one trailing wildcard.
        Assert.Equal("@title:foo*", Filter.Text("title").Prefix("foo").ToQueryString());
    }

    [Fact]
    public void BuildsGeoRadiusFilters()
    {
        var filter = Filter.Geo("location").WithinRadius(-73.9857, 40.7484, 5, GeoUnit.Kilometers);

        Assert.Equal("@location:[-73.9857 40.7484 5 km]", filter.ToQueryString());
    }

    [Fact]
    public void BuildsFuzzyTextFilters()
    {
        Assert.Equal("@title:%redis%", Filter.Text("title").Fuzzy("redis").ToQueryString());
        Assert.Equal("@title:%%redis%%", Filter.Text("title").Fuzzy("redis", 2).ToQueryString());
        Assert.Equal("@title:%%%redis%%%", Filter.Text("title").Fuzzy("redis", 3).ToQueryString());
    }

    [Fact]
    public void BuildsWildcardTextFilters()
    {
        Assert.Equal("@title:w'f?o*bar'", Filter.Text("title").Wildcard("f?o*bar").ToQueryString());
        Assert.Equal("@title:w'it\\'s*'", Filter.Text("title").Wildcard("it's*").ToQueryString());
    }

    [Fact]
    public void BuildsTagLikeFiltersAsWildcardPatterns()
    {
        // Rendered with the w'...' form so both `*` and `?` act as wildcards (plain {...} tag syntax
        // takes `?` literally). Inside w'...' only the backslash and quote delimiter are escaped.
        Assert.Equal("@category:{w'tech*'}", Filter.Tag("category").Like("tech*").ToQueryString());
        Assert.Equal("@category:{w'tech*'|w'*soft'}", Filter.Tag("category").Like("tech*", "*soft").ToQueryString());
        Assert.Equal("@category:{w'open source*'}", Filter.Tag("category").Like("open source*").ToQueryString());
        Assert.Equal("@category:{w'v?.?'}", Filter.Tag("category").Like("v?.?").ToQueryString());
        Assert.Equal("@category:{w'it\\'s*'}", Filter.Tag("category").Like("it's*").ToQueryString());
    }

    [Fact]
    public void BuildsTimestampFilters()
    {
        var after = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var before = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        Assert.Equal("@created:[(1700000000 +inf]", Filter.Timestamp("created").After(after).ToQueryString());
        Assert.Equal("@created:[-inf (1800000000]", Filter.Timestamp("created").Before(before).ToQueryString());
        Assert.Equal("@created:[1700000000 1800000000]", Filter.Timestamp("created").Between(after, before).ToQueryString());
        Assert.Equal("@created:[1700000000 1700000000]", Filter.Timestamp("created").Eq(1_700_000_000).ToQueryString());
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
        Assert.Throws<ArgumentException>(() => Filter.Numeric("rating").Between(double.NaN, 5));
        Assert.Throws<ArgumentException>(() => Filter.Numeric("rating").Between(5, double.NaN));
        Assert.Throws<ArgumentException>(() => Filter.Numeric("rating").Eq(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Filter.Geo("location").WithinRadius(1, 2, 0, GeoUnit.Meters));
        Assert.Throws<ArgumentException>(() => Filter.And(Filter.Tag("genre").Eq("science fiction")));
        Assert.Throws<ArgumentException>(() => Filter.Tag("genre").Like());
        Assert.Throws<ArgumentOutOfRangeException>(() => Filter.Text("title").Fuzzy("redis", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Filter.Text("title").Fuzzy("redis", 4));
        Assert.Throws<ArgumentException>(() => Filter.Timestamp("created").Between(20, 10));
    }
}

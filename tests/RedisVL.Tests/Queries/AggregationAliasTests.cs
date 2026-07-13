using RedisVL.Queries;

namespace RedisVL.Tests.Queries;

public sealed class AggregationAliasTests
{
    [Theory]
    [InlineData("score", "score")]
    [InlineData("@score", "score")]
    [InlineData(" @score", "score")]
    [InlineData("  @score  ", "score")]
    [InlineData("@@score", "score")]
    public void AggregationApplyStripsLeadingAtAfterTrimming(string alias, string expected)
    {
        var apply = new AggregationApply("@price * @quantity", alias);

        Assert.Equal(expected, apply.Alias);
    }

    [Theory]
    [InlineData("total", "total")]
    [InlineData("@total", "total")]
    [InlineData(" @total ", "total")]
    public void AggregationReducerStripsLeadingAtAfterTrimming(string alias, string expected)
    {
        var reducer = new AggregationReducer("COUNT", arguments: null, alias);

        Assert.Equal(expected, reducer.Alias);
    }
}

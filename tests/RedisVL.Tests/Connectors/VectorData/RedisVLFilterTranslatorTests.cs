using System.Linq.Expressions;
using RedisVL.Connectors.VectorData.Mapping;

namespace RedisVL.Tests.Connectors.VectorData;

public sealed class RedisVLFilterTranslatorTests
{
    private static readonly RedisVLRecordModel Model = RedisVLRecordModel.Build(typeof(ConnectorMovie), definition: null);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Translate_ProducesExpectedQueryString(Expression<Func<ConnectorMovie, bool>> filter, string expected)
    {
        var translator = new RedisVLFilterTranslator(Model);

        var result = translator.Translate(filter);

        Assert.Equal(expected, result.ToQueryString());
    }

    public static TheoryData<Expression<Func<ConnectorMovie, bool>>, string> Cases() => new()
    {
        { m => m.Genre == "scifi", "@genre:{scifi}" },
        { m => m.Genre != "crime", "-@genre:{crime}" },
        { m => m.Year > 2000, "@year:[(2000 +inf]" },
        { m => m.Year >= 1990, "@year:[1990 +inf]" },
        { m => m.Year < 2010, "@year:[-inf (2010]" },
        { m => m.Year == 1999, "@year:[1999 1999]" },
        { m => m.Genre == "scifi" && m.Year >= 1990, "@genre:{scifi} @year:[1990 +inf]" },
        { m => m.Genre == "scifi" || m.Genre == "crime", "@genre:{scifi} | @genre:{crime}" },
        { m => 2000 < m.Year, "@year:[(2000 +inf]" },
    };

    [Fact]
    public void Translate_InClause_ProducesTagUnion()
    {
        var translator = new RedisVLFilterTranslator(Model);
        var genres = new[] { "scifi", "crime" };
        Expression<Func<ConnectorMovie, bool>> filter = m => genres.Contains(m.Genre);

        var result = translator.Translate(filter);

        Assert.Equal("@genre:{scifi|crime}", result.ToQueryString());
    }

    [Fact]
    public void Translate_CapturedVariable_IsEvaluated()
    {
        var translator = new RedisVLFilterTranslator(Model);
        var year = 1995;
        Expression<Func<ConnectorMovie, bool>> filter = m => m.Year == year;

        var result = translator.Translate(filter);

        Assert.Equal("@year:[1995 1995]", result.ToQueryString());
    }
}

using System.Linq.Expressions;
using Microsoft.Extensions.VectorData;
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
    public void Translate_NumericIn_ProducesOrOfEqualities()
    {
        // A numeric field must translate `Contains` to an OR of numeric equalities. Before the fix
        // this emitted TAG syntax (@year:{1995|1999}) on a numeric field, which matches nothing.
        var translator = new RedisVLFilterTranslator(Model);
        var years = new[] { 1995, 1999 };
        Expression<Func<ConnectorMovie, bool>> filter = m => years.Contains(m.Year);

        var result = translator.Translate(filter);

        Assert.Equal("@year:[1995 1995] | @year:[1999 1999]", result.ToQueryString());
    }

    [Fact]
    public void Translate_NumericIn_SingleValue_ProducesSingleEquality()
    {
        var translator = new RedisVLFilterTranslator(Model);
        var years = new[] { 1995 };
        Expression<Func<ConnectorMovie, bool>> filter = m => years.Contains(m.Year);

        var result = translator.Translate(filter);

        Assert.Equal("@year:[1995 1995]", result.ToQueryString());
    }

    [Fact]
    public void Translate_CollectionTagMembership_ProducesTagEquality()
    {
        // A collection-typed TAG property retains set-membership semantics unchanged.
        var model = RedisVLRecordModel.Build(typeof(TaggedRecord), definition: null);
        var translator = new RedisVLFilterTranslator(model);
        Expression<Func<TaggedRecord, bool>> filter = r => r.Tags.Contains("crime");

        var result = translator.Translate(filter);

        Assert.Equal("@tags:{crime}", result.ToQueryString());
    }

    [Fact]
    public void Translate_ScalarStringContains_Throws()
    {
        // `Contains` on a scalar string is a substring request that RediSearch cannot express.
        // Before the fix it was silently translated to tag equality (@genre:{crime}).
        var translator = new RedisVLFilterTranslator(Model);
        Expression<Func<ConnectorMovie, bool>> filter = m => m.Genre.Contains("crime");

        Assert.Throws<NotSupportedException>(() => translator.Translate(filter));
    }

    [Fact]
    public void Translate_ContainsOnTextProperty_Throws()
    {
        // `values.Contains(record.TextField)` has no tag/numeric membership meaning. Before the fix
        // it was silently translated to TAG IN syntax on a full-text field.
        var translator = new RedisVLFilterTranslator(Model);
        var titles = new[] { "Heat", "Thief" };
        Expression<Func<ConnectorMovie, bool>> filter = m => titles.Contains(m.Title);

        Assert.Throws<NotSupportedException>(() => translator.Translate(filter));
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

    // Regression: a single translator instance is shared per collection (typically a DI singleton).
    // Before the fix it stored the lambda's parameter in a mutable field, so concurrent Translate
    // calls raced and intermittently produced wrong output or threw. Distinct filters translated in
    // parallel on one instance must each yield their own correct query string.
    [Fact]
    public void Translate_IsThreadSafe_AcrossConcurrentCalls()
    {
        var translator = new RedisVLFilterTranslator(Model);

        Expression<Func<ConnectorMovie, bool>> scifi = m => m.Genre == "scifi";
        Expression<Func<ConnectorMovie, bool>> recent = m => m.Year >= 1990;

        Parallel.For(0, 5_000, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            if ((i & 1) == 0)
            {
                Assert.Equal("@genre:{scifi}", translator.Translate(scifi).ToQueryString());
            }
            else
            {
                Assert.Equal("@year:[1990 +inf]", translator.Translate(recent).ToQueryString());
            }
        });
    }

    /// <summary>A record with a collection-typed TAG property, to exercise set-membership Contains.</summary>
    private sealed class TaggedRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreData(IsIndexed = true)]
        public string[] Tags { get; set; } = [];
    }
}

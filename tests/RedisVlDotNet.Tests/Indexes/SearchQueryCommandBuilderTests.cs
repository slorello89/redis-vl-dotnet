using RedisVlDotNet.Filters;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Indexes;

public sealed class SearchQueryCommandBuilderTests
{
    [Fact]
    public void BuildsFilterSearchArgumentsWithProjectionAndPaging()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new NumericFieldDefinition("year"),
                new TextFieldDefinition("title")
            ]);
        var query = new FilterQuery(
            Filter.Tag("genre").Eq("crime") & Filter.Numeric("year").GreaterThanOrEqualTo(1990),
            ["title", "@year", "title"],
            offset: 5,
            limit: 10);

        var arguments = SearchQueryCommandBuilder.BuildFilterSearchArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal("movies-idx", rendered[0]);
        Assert.Equal("@genre:{crime} @year:[1990 +inf]", rendered[1]);
        Assert.Equal(
            ["RETURN", "2", "title", "year", "LIMIT", "5", "10", "DIALECT", "2"],
            rendered[2..]);
    }

    [Fact]
    public void BuildsCountArgumentsWithNoContent()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [new TagFieldDefinition("genre")]);
        var query = new CountQuery(Filter.Tag("genre").Eq("crime"));

        var arguments = SearchQueryCommandBuilder.BuildCountArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal(
            ["movies-idx", "@genre:{crime}", "NOCONTENT", "LIMIT", "0", "0", "DIALECT", "2"],
            rendered);
    }

    [Fact]
    public void BuildsVectorSearchArgumentsWithFilterProjectionAndAlias()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var query = VectorQuery.FromFloat32(
            "embedding",
            [1f, 2f],
            3,
            Filter.Tag("genre").Eq("crime"),
            ["title"],
            scoreAlias: "distance");

        var arguments = SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal("movies-idx", rendered[0]);
        Assert.Equal("@genre:{crime}=>[KNN 3 @embedding $vector AS distance]", rendered[1]);
        Assert.Equal(
            [
                "PARAMS", "2", "vector", "<binary>",
                "SORTBY", "distance", "ASC",
                "RETURN", "2", "title", "distance",
                "LIMIT", "0", "3",
                "DIALECT", "2"
            ],
            rendered[2..]);
    }

    [Fact]
    public void BuildsVectorSearchArgumentsAgainstJsonAlias()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "doc:", StorageType.Json),
            [
                new TextFieldDefinition("$.title"),
                new VectorFieldDefinition(
                    "$.embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2),
                    alias: "embedding")
            ]);
        var query = VectorQuery.FromFloat32("$.embedding", [1f, 2f], 2, returnFields: ["title"]);

        var arguments = SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query);

        Assert.Equal(
            "docs-idx",
            arguments[0].ToString());
        Assert.Equal(
            "*=>[KNN 2 @embedding $vector AS vector_distance]",
            arguments[1].ToString());
    }

    [Fact]
    public void RejectsUnknownVectorField()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "doc:", StorageType.Hash),
            [new TextFieldDefinition("title")]);
        var query = VectorQuery.FromFloat32("embedding", [1f, 2f], 2);

        var exception = Assert.Throws<InvalidOperationException>(() => SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query));

        Assert.Contains("was not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsNonVectorFields()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "doc:", StorageType.Hash),
            [new TextFieldDefinition("title")]);
        var query = VectorQuery.FromFloat32("title", [1f, 2f], 2);

        var exception = Assert.Throws<InvalidOperationException>(() => SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query));

        Assert.Contains("not configured as a vector field", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsVectorPayloadThatDoesNotMatchSchemaDimensions()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "doc:", StorageType.Hash),
            [
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var query = new VectorQuery("embedding", [0x01, 0x02], 2);

        var exception = Assert.Throws<ArgumentException>(() => SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query));

        Assert.Contains("exactly 8 bytes", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsesSearchResultsFromRedisResponse()
    {
        var rawResult = RedisResult.Create(
            [
                RedisResult.Create(2),
                RedisResult.Create((RedisValue)"movie:1"),
                RedisResult.Create(
                    [
                        RedisResult.Create((RedisValue)"title"),
                        RedisResult.Create((RedisValue)"Heat"),
                        RedisResult.Create((RedisValue)"distance"),
                        RedisResult.Create((RedisValue)"0")
                    ]),
                RedisResult.Create((RedisValue)"movie:2"),
                RedisResult.Create(
                    [
                        RedisResult.Create((RedisValue)"title"),
                        RedisResult.Create((RedisValue)"Thief"),
                        RedisResult.Create((RedisValue)"distance"),
                        RedisResult.Create((RedisValue)"0.25")
                    ])
            ]);

        var results = SearchResultsParser.Parse(rawResult);

        Assert.Equal(2, results.TotalCount);
        Assert.Collection(
            results.Documents,
            document =>
            {
                Assert.Equal("movie:1", document.Id);
                Assert.Equal("Heat", document.Values["title"]);
                Assert.Equal("0", document.Values["distance"]);
            },
            document =>
            {
                Assert.Equal("movie:2", document.Id);
                Assert.Equal("Thief", document.Values["title"]);
                Assert.Equal("0.25", document.Values["distance"]);
            });
    }

    [Fact]
    public void VectorQueryNormalizesReturnFieldsAndScoreAlias()
    {
        var query = VectorQuery.FromFloat32(
            "@embedding",
            [1f, 2f],
            1,
            returnFields: ["@title", "title", "distance"],
            scoreAlias: "@distance");

        Assert.Equal("embedding", query.FieldName);
        Assert.Equal("distance", query.ScoreAlias);
        Assert.Equal(["title", "distance"], query.ReturnFields);
    }

    [Fact]
    public void FilterQueryNormalizesReturnFieldsAndPaging()
    {
        var query = new FilterQuery(
            returnFields: ["@title", "title", "year"],
            offset: 2,
            limit: 5);

        Assert.Equal(["title", "year"], query.ReturnFields);
        Assert.Equal(2, query.Offset);
        Assert.Equal(5, query.Limit);
    }

    [Fact]
    public void CountParsesSearchResponseTotalWithoutDocuments()
    {
        var rawResult = RedisResult.Create([RedisResult.Create(3)]);

        var results = SearchResultsParser.Parse(rawResult);

        Assert.Equal(3, results.TotalCount);
        Assert.Empty(results.Documents);
    }

    private static string RenderArgument(object argument) =>
        argument is byte[] ? "<binary>" : argument.ToString()!;
}

using RedisVlDotNet.Filters;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Indexes;

public sealed class SearchQueryCommandBuilderTests
{
    [Fact]
    public void BuildsTextSearchArgumentsWithProjectionAndPaging()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year")
            ]);
        var query = new TextQuery("hel* world", ["title", "@year", "title"], offset: 5, limit: 10);

        var arguments = SearchQueryCommandBuilder.BuildTextSearchArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal("movies-idx", rendered[0]);
        Assert.Equal("hel* world", rendered[1]);
        Assert.Equal(
            ["RETURN", "2", "title", "year", "LIMIT", "5", "10", "DIALECT", "2"],
            rendered[2..]);
    }

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
    public void BuildsAggregateArgumentsWithLoadApplyGroupBySortAndPaging()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new TagFieldDefinition("genre"),
                new NumericFieldDefinition("year")
            ]);
        var query = new AggregationQuery(
            queryString: "@genre:{crime}",
            loadFields: ["title", "@title"],
            applyClauses: [new AggregationApply("@year - (@year % 10)", "decade")],
            groupBy: new AggregationGroupBy(
                ["genre", "decade"],
                [
                    AggregationReducer.Count("movie_count"),
                    AggregationReducer.Average("year", "avg_year")
                ]),
            sortBy: new AggregationSortBy([new AggregationSortField("avg_year", descending: true)]),
            offset: 1,
            limit: 5);

        var arguments = SearchQueryCommandBuilder.BuildAggregateArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal(
            [
                "movies-idx",
                "@genre:{crime}",
                "LOAD", "1", "@title",
                "APPLY", "@year - (@year % 10)", "AS", "decade",
                "GROUPBY", "2", "@genre", "@decade",
                "REDUCE", "COUNT", "0", "AS", "movie_count",
                "REDUCE", "AVG", "1", "@year", "AS", "avg_year",
                "SORTBY", "2", "@avg_year", "DESC",
                "LIMIT", "1", "5",
                "DIALECT", "2"
            ],
            rendered);
    }

    [Fact]
    public void BuildsAggregateArgumentsAgainstJsonAliases()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Json),
            [
                new TagFieldDefinition("$.genre", alias: "genre"),
                new NumericFieldDefinition("$.year", alias: "year")
            ]);
        var query = new AggregationQuery(
            groupBy: new AggregationGroupBy(["$.genre"], [AggregationReducer.Max("$.year", "latest_year")]));

        var arguments = SearchQueryCommandBuilder.BuildAggregateArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal(
            [
                "movies-idx",
                "*",
                "GROUPBY", "1", "@genre",
                "REDUCE", "MAX", "1", "@year", "AS", "latest_year",
                "LIMIT", "0", "10",
                "DIALECT", "2"
            ],
            rendered);
    }

    [Fact]
    public void BuildsAggregateHybridArgumentsWithVectorParamsAndAggregationPipeline()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new TagFieldDefinition("genre"),
                new NumericFieldDefinition("year"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var query = AggregateHybridQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "embedding",
            [1f, 0f],
            3,
            Filter.Tag("genre").Eq("crime"),
            loadFields: ["title", "@title"],
            applyClauses: [new AggregationApply("@year - (@year % 10)", "decade")],
            groupBy: new AggregationGroupBy(
                ["genre", "decade"],
                [
                    AggregationReducer.Count("movieCount"),
                    AggregationReducer.Average("vector_distance", "avgDistance")
                ]),
            sortBy: new AggregationSortBy([new AggregationSortField("avgDistance")]),
            offset: 1,
            limit: 2,
            scoreAlias: "vector_distance");

        var arguments = SearchQueryCommandBuilder.BuildAggregateHybridArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal(
            [
                "movies-idx",
                "(@title:He* @genre:{crime})=>[KNN 3 @embedding $vector AS vector_distance]",
                "PARAMS", "2", "vector", "<binary>",
                "LOAD", "1", "@title",
                "APPLY", "@year - (@year % 10)", "AS", "decade",
                "GROUPBY", "2", "@genre", "@decade",
                "REDUCE", "COUNT", "0", "AS", "movieCount",
                "REDUCE", "AVG", "1", "@vector_distance", "AS", "avgDistance",
                "SORTBY", "2", "@avgDistance", "ASC",
                "LIMIT", "1", "2",
                "DIALECT", "2"
            ],
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
    public void BuildsHybridSearchArgumentsWithTextAndMetadataFilters()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new TagFieldDefinition("genre"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var query = HybridQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "embedding",
            [1f, 0f],
            2,
            Filter.Tag("genre").Eq("crime"),
            ["title"],
            scoreAlias: "distance");

        var arguments = SearchQueryCommandBuilder.BuildHybridSearchArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal("movies-idx", rendered[0]);
        Assert.Equal("(@title:He* @genre:{crime})=>[KNN 2 @embedding $vector AS distance]", rendered[1]);
        Assert.Equal(
            [
                "PARAMS", "2", "vector", "<binary>",
                "SORTBY", "distance", "ASC",
                "RETURN", "2", "title", "distance",
                "LIMIT", "0", "2",
                "DIALECT", "2"
            ],
            rendered[2..]);
    }

    [Fact]
    public void BuildsVectorRangeArgumentsWithSortingProjectionAndPaging()
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
        var query = VectorRangeQuery.FromFloat32(
            "embedding",
            [1f, 0f],
            0.3,
            Filter.Tag("genre").Eq("crime"),
            ["title"],
            scoreAlias: "distance",
            offset: 1,
            limit: 5);

        var arguments = SearchQueryCommandBuilder.BuildVectorRangeArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal("movies-idx", rendered[0]);
        Assert.Equal("@genre:{crime} @embedding:[VECTOR_RANGE 0.3 $vector]=>{$YIELD_DISTANCE_AS: distance}", rendered[1]);
        Assert.Equal(
            [
                "PARAMS", "2", "vector", "<binary>",
                "SORTBY", "distance", "ASC",
                "RETURN", "2", "title", "distance",
                "LIMIT", "1", "5",
                "DIALECT", "2"
            ],
            rendered[2..]);
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
    public void HybridQueryRequiresATextPredicate()
    {
        var exception = Assert.Throws<ArgumentException>(() => HybridQuery.FromFloat32(
            Filter.Tag("genre").Eq("crime"),
            "embedding",
            [1f, 2f],
            1));

        Assert.Contains("text predicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HybridQueryNormalizesReturnFieldsAndScoreAlias()
    {
        var query = HybridQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "@embedding",
            [1f, 2f],
            2,
            returnFields: ["@title", "title", "distance"],
            scoreAlias: "@distance");

        Assert.Equal("embedding", query.VectorFieldName);
        Assert.Equal("distance", query.ScoreAlias);
        Assert.Equal(["title", "distance"], query.ReturnFields);
    }

    [Fact]
    public void AggregateHybridQueryRequiresATextPredicate()
    {
        var exception = Assert.Throws<ArgumentException>(() => AggregateHybridQuery.FromFloat32(
            Filter.Tag("genre").Eq("crime"),
            "embedding",
            [1f, 2f],
            1));

        Assert.Contains("text predicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AggregateHybridQueryNormalizesFieldsPagingAndScoreAlias()
    {
        var query = AggregateHybridQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "@embedding",
            [1f, 2f],
            2,
            loadFields: ["@title", "title", "vector_distance"],
            offset: 2,
            limit: 5,
            scoreAlias: "@distance");

        Assert.Equal("embedding", query.VectorFieldName);
        Assert.Equal(["@title", "vector_distance"], query.LoadFields);
        Assert.Equal(2, query.Offset);
        Assert.Equal(5, query.Limit);
        Assert.Equal("distance", query.ScoreAlias);
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
    public void TextQueryNormalizesTextReturnFieldsAndPaging()
    {
        var query = new TextQuery(
            "  hello world  ",
            returnFields: ["@title", "title", "year"],
            offset: 2,
            limit: 5);

        Assert.Equal("hello world", query.Text);
        Assert.Equal(["title", "year"], query.ReturnFields);
        Assert.Equal(2, query.Offset);
        Assert.Equal(5, query.Limit);
    }

    [Fact]
    public void TextQueryRejectsBlankText()
    {
        Assert.Throws<ArgumentException>(() => new TextQuery(" "));
    }

    [Fact]
    public void AggregationQueryRejectsBlankQueryString()
    {
        Assert.Throws<ArgumentException>(() => new AggregationQuery(" "));
    }

    [Fact]
    public void AggregationGroupByRequiresAPropertyOrReducer()
    {
        Assert.Throws<ArgumentException>(() => new AggregationGroupBy());
    }

    [Fact]
    public void AggregationSortByRequiresAtLeastOneField()
    {
        Assert.Throws<ArgumentException>(() => new AggregationSortBy([]));
    }

    [Fact]
    public void AggregationReducerQuantileRejectsInvalidPercentiles()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AggregationReducer.Quantile("year", -0.1d, "p10"));
        Assert.Throws<ArgumentOutOfRangeException>(() => AggregationReducer.Quantile("year", 1.1d, "p10"));
    }

    [Fact]
    public void VectorRangeQueryNormalizesReturnFieldsAndPaging()
    {
        var query = VectorRangeQuery.FromFloat32(
            "@embedding",
            [1f, 2f],
            0.5,
            returnFields: ["@title", "title", "distance"],
            scoreAlias: "@distance",
            offset: 2,
            limit: 5);

        Assert.Equal("embedding", query.FieldName);
        Assert.Equal("distance", query.ScoreAlias);
        Assert.Equal(["title", "distance"], query.ReturnFields);
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

    [Fact]
    public void ParsesAggregationResultsFromRedisResponse()
    {
        var rawResult = RedisResult.Create(
            [
                RedisResult.Create(2),
                RedisResult.Create(
                    [
                        RedisResult.Create((RedisValue)"genre"),
                        RedisResult.Create((RedisValue)"crime"),
                        RedisResult.Create((RedisValue)"movie_count"),
                        RedisResult.Create((RedisValue)"2")
                    ]),
                RedisResult.Create(
                    [
                        RedisResult.Create((RedisValue)"genre"),
                        RedisResult.Create((RedisValue)"science-fiction"),
                        RedisResult.Create((RedisValue)"movie_count"),
                        RedisResult.Create((RedisValue)"1")
                    ])
            ]);

        var results = AggregationResultsParser.Parse(rawResult);

        Assert.Equal(2, results.TotalCount);
        Assert.Collection(
            results.Rows,
            row =>
            {
                Assert.Equal("crime", row.Values["genre"]);
                Assert.Equal("2", row.Values["movie_count"]);
            },
            row =>
            {
                Assert.Equal("science-fiction", row.Values["genre"]);
                Assert.Equal("1", row.Values["movie_count"]);
            });
    }

    private static string RenderArgument(object argument) =>
        argument is byte[] ? "<binary>" : argument.ToString()!;
}

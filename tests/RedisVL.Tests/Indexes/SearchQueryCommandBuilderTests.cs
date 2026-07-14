using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using StackExchange.Redis;

namespace RedisVL.Tests.Indexes;

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
    public void BuildsFilterSearchArgumentsWithSortBy()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new NumericFieldDefinition("year", sortable: true)
            ]);
        var query = new FilterQuery(
            Filter.Tag("genre").Eq("crime"),
            offset: 0,
            limit: 10,
            sortBy: new SearchSortBy("@year", descending: true));

        var arguments = SearchQueryCommandBuilder.BuildFilterSearchArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal("@genre:{crime}", rendered[1]);
        Assert.Equal(
            ["SORTBY", "year", "DESC", "LIMIT", "0", "10", "DIALECT", "2"],
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
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        m: 16,
                        efConstruction: 200))
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
            scoreAlias: "vector_distance",
            runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 150));

        var arguments = SearchQueryCommandBuilder.BuildAggregateHybridArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal(
            [
                "movies-idx",
                "(@title:He* @genre:{crime})=>[KNN 3 @embedding $vector EF_RUNTIME $ef_runtime AS vector_distance]",
                "PARAMS", "4", "vector", "<binary>", "ef_runtime", "150",
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
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        m: 16,
                        efConstruction: 200))
            ]);
        var query = VectorQuery.FromFloat32(
            "embedding",
            [1f, 2f],
            3,
            Filter.Tag("genre").Eq("crime"),
            ["title"],
            scoreAlias: "distance",
            runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 125),
            pagination: new QueryPagination(offset: 1, limit: 2));

        var arguments = SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal("movies-idx", rendered[0]);
        Assert.Equal("(@genre:{crime})=>[KNN 3 @embedding $vector EF_RUNTIME $ef_runtime AS distance]", rendered[1]);
        Assert.Equal(
            [
                "PARAMS", "4", "vector", "<binary>", "ef_runtime", "125",
                "SORTBY", "distance", "ASC",
                "RETURN", "2", "title", "distance",
                "LIMIT", "1", "2",
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
            "(*)=>[KNN 2 @embedding $vector AS vector_distance]",
            arguments[1].ToString());
    }

    [Theory]
    [MemberData(nameof(CompoundVectorFilterCases))]
    public void ParenthesizesCompoundVectorSearchFilters(FilterExpression filter, string expectedFilterClause)
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
        var query = VectorQuery.FromFloat32("embedding", [1f, 2f], 3, filter, scoreAlias: "distance");

        var arguments = SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query);

        Assert.Equal(
            $"{expectedFilterClause}=>[KNN 3 @embedding $vector AS distance]",
            arguments[1].ToString());
    }

    public static TheoryData<FilterExpression, string> CompoundVectorFilterCases() => new()
    {
        { Filter.Tag("genre").Eq("crime") & Filter.Text("title").Match("heat"), "(@genre:{crime} @title:heat)" },
        { Filter.Tag("genre").Eq("crime") | Filter.Tag("genre").Eq("action"), "(@genre:{crime} | @genre:{action})" },
        { Filter.Not(Filter.Tag("genre").Eq("crime")), "(-@genre:{crime})" },
    };

    [Fact]
    public void DefaultVectorQueryOmitsReturnSoAllFieldsComeBack()
    {
        var schema = VectorSchema();
        var query = VectorQuery.FromFloat32("embedding", [1f, 2f], 2);

        var tokens = SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query)
            .Select(static argument => argument.ToString())
            .ToArray();

        // No RETURN → the server returns every stored field plus the yielded score, so the typed
        // happy path can map a POCO instead of throwing on a missing required field.
        Assert.Empty(query.ReturnFields);
        Assert.DoesNotContain("RETURN", tokens);
        // SORTBY on the score alias is still emitted so results stay ordered by distance.
        Assert.Contains("SORTBY", tokens);
    }

    [Fact]
    public void ExplicitVectorQueryReturnFieldsStillEmitReturn()
    {
        var schema = VectorSchema();
        var query = VectorQuery.FromFloat32("embedding", [1f, 2f], 2, returnFields: ["title"], scoreAlias: "distance");

        var tokens = SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query)
            .Select(static argument => argument.ToString())
            .ToArray();

        Assert.Equal(["title", "distance"], query.ReturnFields);
        Assert.Contains("RETURN", tokens);
    }

    private static SearchSchema VectorSchema() => new(
        new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
        [
            new TextFieldDefinition("title"),
            new VectorFieldDefinition(
                "embedding",
                new VectorFieldAttributes(
                    VectorAlgorithm.Flat,
                    VectorDataType.Float32,
                    VectorDistanceMetric.Cosine,
                    2))
        ]);

    [Fact]
    public void DefaultHybridQueryOmitsReturnSoAllFieldsComeBack()
    {
        var schema = VectorSchema();
        var query = HybridQuery.FromFloat32(Filter.Text("title").Match("heat"), "embedding", [1f, 0f], 2);

        var tokens = SearchQueryCommandBuilder.BuildHybridSearchArguments(schema, query)
            .Select(static argument => argument.ToString())
            .ToArray();

        // No RETURN → the server returns every stored field plus the yielded score, so the typed
        // happy path can map a POCO instead of throwing on a missing required field.
        Assert.Empty(query.ReturnFields);
        Assert.DoesNotContain("RETURN", tokens);
        // SORTBY on the score alias (yielded by the KNN `AS` clause) is still emitted so results stay ordered.
        Assert.Contains("SORTBY", tokens);
    }

    [Fact]
    public void ExplicitHybridQueryReturnFieldsStillEmitReturn()
    {
        var schema = VectorSchema();
        var query = HybridQuery.FromFloat32(
            Filter.Text("title").Match("heat"),
            "embedding",
            [1f, 0f],
            2,
            returnFields: ["title"],
            scoreAlias: "distance");

        var tokens = SearchQueryCommandBuilder.BuildHybridSearchArguments(schema, query)
            .Select(static argument => argument.ToString())
            .ToArray();

        Assert.Equal(["title", "distance"], query.ReturnFields);
        Assert.Contains("RETURN", tokens);
    }

    [Fact]
    public void DefaultVectorRangeQueryOmitsReturnSoAllFieldsComeBack()
    {
        var schema = VectorSchema();
        var query = VectorRangeQuery.FromFloat32("embedding", [1f, 0f], 0.3);

        var tokens = SearchQueryCommandBuilder.BuildVectorRangeArguments(schema, query)
            .Select(static argument => argument.ToString())
            .ToArray();

        // No RETURN → the server returns every stored field plus the yielded distance, so the typed
        // happy path can map a POCO instead of throwing on a missing required field.
        Assert.Empty(query.ReturnFields);
        Assert.DoesNotContain("RETURN", tokens);
        // SORTBY on the distance alias (yielded by VECTOR_RANGE `$YIELD_DISTANCE_AS`) is still emitted.
        Assert.Contains("SORTBY", tokens);
    }

    [Fact]
    public void ExplicitVectorRangeQueryReturnFieldsStillEmitReturn()
    {
        var schema = VectorSchema();
        var query = VectorRangeQuery.FromFloat32("embedding", [1f, 0f], 0.3, returnFields: ["title"], scoreAlias: "distance");

        var tokens = SearchQueryCommandBuilder.BuildVectorRangeArguments(schema, query)
            .Select(static argument => argument.ToString())
            .ToArray();

        Assert.Equal(["title", "distance"], query.ReturnFields);
        Assert.Contains("RETURN", tokens);
    }

    [Fact]
    public void BuildsMultiVectorSearchArgumentsWithStableAliases()
    {
        var schema = new SearchSchema(
            new IndexDefinition("products-idx", "product:", StorageType.Hash),
            [
                new TagFieldDefinition("category"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "text_embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        m: 16,
                        efConstruction: 200)),
                new VectorFieldDefinition(
                    "image_embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        m: 16,
                        efConstruction: 200))
            ]);
        var query = new MultiVectorQuery(
            [
                MultiVectorInput.FromFloat32("text_embedding", [1f, 0f], weight: 0.7),
                MultiVectorInput.FromFloat32("image_embedding", [0f, 1f], weight: 0.3)
            ],
            topK: 3,
            filter: Filter.Tag("category").Eq("footwear"),
            returnFields: ["title"],
            runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 64),
            pagination: new QueryPagination(offset: 1, limit: 2));

        var arguments = SearchQueryCommandBuilder.BuildMultiVectorSearchArguments(schema, query);

        Assert.Equal(2, arguments.Count);
        Assert.Equal(
            [
                "products-idx",
                "(@category:{footwear})=>[KNN 3 @text_embedding $vector EF_RUNTIME $ef_runtime AS __mv_score_0]",
                "PARAMS", "4", "vector", "<binary>", "ef_runtime", "64",
                "SORTBY", "__mv_score_0", "ASC",
                "RETURN", "2", "title", "__mv_score_0",
                "LIMIT", "0", "3",
                "DIALECT", "2"
            ],
            arguments[0].Select(RenderArgument).ToArray());
        Assert.Equal(
            [
                "products-idx",
                "(@category:{footwear})=>[KNN 3 @image_embedding $vector EF_RUNTIME $ef_runtime AS __mv_score_1]",
                "PARAMS", "4", "vector", "<binary>", "ef_runtime", "64",
                "SORTBY", "__mv_score_1", "ASC",
                "RETURN", "2", "title", "__mv_score_1",
                "LIMIT", "0", "3",
                "DIALECT", "2"
            ],
            arguments[1].Select(RenderArgument).ToArray());
    }

    [Fact]
    public void DefaultMultiVectorQueryFanOutOmitsReturnButKeepsScoreAliases()
    {
        var schema = new SearchSchema(
            new IndexDefinition("products-idx", "product:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "text_embedding",
                    new VectorFieldAttributes(VectorAlgorithm.Flat, VectorDataType.Float32, VectorDistanceMetric.Cosine, 2)),
                new VectorFieldDefinition(
                    "image_embedding",
                    new VectorFieldAttributes(VectorAlgorithm.Flat, VectorDataType.Float32, VectorDistanceMetric.Cosine, 2))
            ]);
        var query = new MultiVectorQuery(
            [
                MultiVectorInput.FromFloat32("text_embedding", [1f, 0f], weight: 0.7),
                MultiVectorInput.FromFloat32("image_embedding", [0f, 1f], weight: 0.3)
            ],
            topK: 3);

        var arguments = SearchQueryCommandBuilder.BuildMultiVectorSearchArguments(schema, query);

        Assert.Equal(2, arguments.Count);
        // Unspecified projected fields → each fan-out sub-query omits RETURN so all stored fields come back,
        // but the internal per-vector score alias is still yielded via the KNN `AS` clause for combining.
        Assert.All(
            arguments,
            static subCommand => Assert.DoesNotContain("RETURN", subCommand.Select(RenderArgument)));
        Assert.Contains("(*)=>[KNN 3 @text_embedding $vector AS __mv_score_0]", arguments[0].Select(RenderArgument));
        Assert.Contains("(*)=>[KNN 3 @image_embedding $vector AS __mv_score_1]", arguments[1].Select(RenderArgument));
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
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        m: 16,
                        efConstruction: 200))
            ]);
        var query = HybridQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "embedding",
            [1f, 0f],
            2,
            Filter.Tag("genre").Eq("crime"),
            ["title"],
            scoreAlias: "distance",
            runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 100),
            pagination: new QueryPagination(limit: 1));

        var arguments = SearchQueryCommandBuilder.BuildHybridSearchArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal("movies-idx", rendered[0]);
        Assert.Equal("(@title:He* @genre:{crime})=>[KNN 2 @embedding $vector EF_RUNTIME $ef_runtime AS distance]", rendered[1]);
        Assert.Equal(
            [
                "PARAMS", "4", "vector", "<binary>", "ef_runtime", "100",
                "SORTBY", "distance", "ASC",
                "RETURN", "2", "title", "distance",
                "LIMIT", "0", "1",
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
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        m: 16,
                        efConstruction: 200))
            ]);
        var query = VectorRangeQuery.FromFloat32(
            "embedding",
            [1f, 0f],
            0.3,
            Filter.Tag("genre").Eq("crime"),
            ["title"],
            scoreAlias: "distance",
            offset: 1,
            limit: 5,
            runtimeOptions: new VectorRangeRuntimeOptions(epsilon: 0.05));

        var arguments = SearchQueryCommandBuilder.BuildVectorRangeArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal("movies-idx", rendered[0]);
        Assert.Equal("(@genre:{crime}) @embedding:[VECTOR_RANGE 0.3 $vector]=>{$YIELD_DISTANCE_AS: distance; $EPSILON: $epsilon}", rendered[1]);
        Assert.Equal(
            [
                "PARAMS", "4", "vector", "<binary>", "epsilon", "0.05",
                "SORTBY", "distance", "ASC",
                "RETURN", "2", "title", "distance",
                "LIMIT", "1", "5",
                "DIALECT", "2"
            ],
            rendered[2..]);
    }

    [Fact]
    public void ParenthesizesTopLevelOrFilterInVectorRangeQuery()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
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
            Filter.Tag("genre").Eq("crime") | Filter.Tag("genre").Eq("drama"),
            scoreAlias: "distance");

        var arguments = SearchQueryCommandBuilder.BuildVectorRangeArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        // The filter must be parenthesized so the VECTOR_RANGE clause applies to the
        // whole OR expression. Without parens, DIALECT 2 precedence parses this as
        // `A | (B AND range)`, silently returning out-of-range documents via the left branch.
        Assert.Equal(
            "(@genre:{crime} | @genre:{drama}) @embedding:[VECTOR_RANGE 0.3 $vector]=>{$YIELD_DISTANCE_AS: distance}",
            rendered[1]);
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
    public void RejectsMultiVectorQueriesAgainstNonCosineFields()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "doc:", StorageType.Hash),
            [
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.L2,
                        2))
            ]);
        var query = new MultiVectorQuery([MultiVectorInput.FromFloat32("embedding", [1f, 0f])], 2);

        var exception = Assert.Throws<InvalidOperationException>(() => SearchQueryCommandBuilder.BuildMultiVectorSearchArguments(schema, query));

        Assert.Contains("cosine distance fields", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsEfRuntimeForFlatVectorFields()
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
        var query = VectorQuery.FromFloat32("embedding", [1f, 2f], 2, runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 50));

        var exception = Assert.Throws<InvalidOperationException>(() => SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query));

        Assert.Contains("EF_RUNTIME", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsEpsilonForFlatVectorFields()
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
        var query = VectorRangeQuery.FromFloat32("embedding", [1f, 2f], 0.25, runtimeOptions: new VectorRangeRuntimeOptions(epsilon: 0.01));

        var exception = Assert.Throws<InvalidOperationException>(() => SearchQueryCommandBuilder.BuildVectorRangeArguments(schema, query));

        Assert.Contains("EPSILON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildsVectorSearchArgumentsWithSvsRuntimeKnobs()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.SvsVamana,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        compression: VectorCompression.Lvq8))
            ]);
        var query = VectorQuery.FromFloat32(
            "embedding",
            [1f, 2f],
            3,
            Filter.Tag("genre").Eq("crime"),
            ["title"],
            scoreAlias: "distance",
            runtimeOptions: new VectorKnnRuntimeOptions(
                searchWindowSize: 30,
                useSearchHistory: SvsSearchHistory.On,
                searchBufferCapacity: 60));

        var arguments = SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query);
        var rendered = arguments.Select(RenderArgument).ToArray();

        Assert.Equal(
            "(@genre:{crime})=>[KNN 3 @embedding $vector SEARCH_WINDOW_SIZE $search_window_size USE_SEARCH_HISTORY $use_search_history SEARCH_BUFFER_CAPACITY $search_buffer_capacity AS distance]",
            rendered[1]);
        Assert.Equal(
            [
                "PARAMS", "8", "vector", "<binary>",
                "search_window_size", "30",
                "use_search_history", "ON",
                "search_buffer_capacity", "60"
            ],
            rendered[2..12]);
    }

    [Fact]
    public void RejectsSvsRuntimeKnobsForHnswVectorFields()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "doc:", StorageType.Hash),
            [
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        m: 16))
            ]);
        var query = VectorQuery.FromFloat32("embedding", [1f, 2f], 2, runtimeOptions: new VectorKnnRuntimeOptions(searchWindowSize: 30));

        var exception = Assert.Throws<InvalidOperationException>(() => SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query));

        Assert.Contains("SEARCH_WINDOW_SIZE", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsEfRuntimeForSvsVectorFields()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "doc:", StorageType.Hash),
            [
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.SvsVamana,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var query = VectorQuery.FromFloat32("embedding", [1f, 2f], 2, runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 50));

        var exception = Assert.Throws<InvalidOperationException>(() => SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query));

        Assert.Contains("EF_RUNTIME", exception.Message, StringComparison.Ordinal);
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
            scoreAlias: "@distance",
            pagination: new QueryPagination(offset: 0, limit: 1));

        Assert.Equal("embedding", query.FieldName);
        Assert.Equal("distance", query.ScoreAlias);
        Assert.Equal(["title", "distance"], query.ReturnFields);
        Assert.Equal(0, query.Offset);
        Assert.Equal(1, query.Limit);
        Assert.Equal(1, query.Pagination.Limit);
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
            scoreAlias: "@distance",
            pagination: new QueryPagination(offset: 1, limit: 1));

        Assert.Equal("embedding", query.VectorFieldName);
        Assert.Equal("distance", query.ScoreAlias);
        Assert.Equal(["title", "distance"], query.ReturnFields);
        Assert.Equal(1, query.Offset);
        Assert.Equal(1, query.Limit);
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
            pagination: new QueryPagination(offset: 2, limit: 5));

        Assert.Equal(["title", "year"], query.ReturnFields);
        Assert.Equal(2, query.Offset);
        Assert.Equal(5, query.Limit);
        Assert.Equal(2, query.Pagination.Offset);
    }

    [Fact]
    public void TextQueryNormalizesTextReturnFieldsAndPaging()
    {
        var query = new TextQuery(
            "  hello world  ",
            returnFields: ["@title", "title", "year"],
            pagination: new QueryPagination(offset: 2, limit: 5));

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
    public void TextQueryWithoutWeightsUsesRawText()
    {
        var query = new TextQuery("@title:redis");

        Assert.Empty(query.FieldWeights);
        Assert.Equal("@title:redis", query.QueryString);
    }

    [Fact]
    public void TextQuerySingleFieldWeightWrapsTermsAndAppendsWeight()
    {
        var defaultWeight = new TextQuery("redis search", fieldWeights: new Dictionary<string, double> { ["title"] = 1.0 });
        Assert.Equal("@title:(redis | search)", defaultWeight.QueryString);

        var weighted = new TextQuery("redis search", fieldWeights: new Dictionary<string, double> { ["title"] = 5.0 });
        Assert.Equal("@title:(redis | search) => { $weight: 5 }", weighted.QueryString);
    }

    [Fact]
    public void TextQueryMultipleFieldWeightsAreOrGroupedInDeclarationOrder()
    {
        var query = new TextQuery(
            "redis",
            fieldWeights: new Dictionary<string, double>
            {
                ["@title"] = 5.0,
                ["content"] = 2.0,
                ["tags"] = 1.0,
            });

        Assert.Equal(
            "(@title:(redis) => { $weight: 5 } | @content:(redis) => { $weight: 2 } | @tags:(redis))",
            query.QueryString);
    }

    [Fact]
    public void TextQueryFieldWeightsFlowIntoCommandArguments()
    {
        var schema = new SearchSchema(
            new IndexDefinition("articles-idx", "article:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new TextFieldDefinition("body")
            ]);
        var query = new TextQuery(
            "redis",
            fieldWeights: new Dictionary<string, double> { ["title"] = 5.0, ["body"] = 1.0 });

        var rendered = SearchQueryCommandBuilder.BuildTextSearchArguments(schema, query)
            .Select(RenderArgument)
            .ToArray();

        Assert.Equal("articles-idx", rendered[0]);
        Assert.Equal("(@title:(redis) => { $weight: 5 } | @body:(redis))", rendered[1]);
    }

    [Fact]
    public void TextQueryRejectsNonPositiveWeights()
    {
        Assert.Throws<ArgumentException>(
            () => new TextQuery("redis", fieldWeights: new Dictionary<string, double> { ["title"] = 0 }));
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
            pagination: new QueryPagination(offset: 2, limit: 5));

        Assert.Equal("embedding", query.FieldName);
        Assert.Equal("distance", query.ScoreAlias);
        Assert.Equal(["title", "distance"], query.ReturnFields);
        Assert.Equal(2, query.Offset);
        Assert.Equal(5, query.Limit);
    }

    [Fact]
    public void QueryPaginationRejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new QueryPagination(offset: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QueryPagination(limit: -1));
    }

    [Fact]
    public void VectorStyleQueriesRejectPaginationPastTopK()
    {
        Assert.Throws<ArgumentException>(() => VectorQuery.FromFloat32(
            "embedding",
            [1f, 0f],
            2,
            pagination: new QueryPagination(offset: 1, limit: 2)));

        Assert.Throws<ArgumentException>(() => HybridQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "embedding",
            [1f, 0f],
            2,
            pagination: new QueryPagination(offset: 1, limit: 2)));

        Assert.Throws<ArgumentException>(() => new MultiVectorQuery(
            [MultiVectorInput.FromFloat32("embedding", [1f, 0f])],
            2,
            pagination: new QueryPagination(offset: 1, limit: 2)));
    }

    [Fact]
    public void RejectsInvalidRuntimeOptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VectorKnnRuntimeOptions(efRuntime: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VectorRangeRuntimeOptions(epsilon: -0.1));
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

    [Fact]
    public void BuildsNativeHybridArgumentsWithServerDefaultCombination()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var query = HybridSearchQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "embedding",
            [1f, 0f],
            3);

        var rendered = SearchQueryCommandBuilder.BuildNativeHybridArguments(schema, query)
            .Select(RenderArgument)
            .ToArray();

        Assert.Equal(
            [
                "movies-idx",
                "SEARCH", "@title:He*",
                "VSIM", "@embedding", "$vector",
                "KNN", "2", "K", "3",
                "LIMIT", "0", "3",
                "LOAD", "2", "@__key", "@__score",
                "PARAMS", "2", "vector", "<binary>"
            ],
            rendered);
    }

    [Fact]
    public void BuildsNativeHybridArgumentsWithLinearCombinationFilterAndRuntimeOptions()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new TagFieldDefinition("genre"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        m: 16,
                        efConstruction: 200))
            ]);
        var query = HybridSearchQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "embedding",
            [1f, 0f],
            5,
            combination: new LinearHybridCombination(0.7, 0.3, window: 20),
            vectorFilter: Filter.Tag("genre").Eq("crime"),
            returnFields: ["title"],
            runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 100),
            pagination: new QueryPagination(limit: 5));

        var rendered = SearchQueryCommandBuilder.BuildNativeHybridArguments(schema, query)
            .Select(RenderArgument)
            .ToArray();

        Assert.Equal(
            [
                "movies-idx",
                "SEARCH", "@title:He*",
                "VSIM", "@embedding", "$vector",
                "KNN", "4", "K", "5", "EF_RUNTIME", "100",
                "FILTER", "@genre:{crime}",
                "COMBINE", "LINEAR", "6", "ALPHA", "0.7", "BETA", "0.3", "WINDOW", "20",
                "LIMIT", "0", "5",
                "LOAD", "3", "@__key", "@__score", "@title",
                "PARAMS", "2", "vector", "<binary>"
            ],
            rendered);
    }

    [Fact]
    public void BuildsNativeHybridArgumentsWithReciprocalRankFusionCombination()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var query = HybridSearchQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "embedding",
            [1f, 0f],
            4,
            combination: new ReciprocalRankFusionHybridCombination(constant: 60, window: 50));

        var rendered = SearchQueryCommandBuilder.BuildNativeHybridArguments(schema, query)
            .Select(RenderArgument)
            .ToArray();

        Assert.Equal(
            [
                "movies-idx",
                "SEARCH", "@title:He*",
                "VSIM", "@embedding", "$vector",
                "KNN", "2", "K", "4",
                "COMBINE", "RRF", "4", "CONSTANT", "60", "WINDOW", "50",
                "LIMIT", "0", "4",
                "LOAD", "2", "@__key", "@__score",
                "PARAMS", "2", "vector", "<binary>"
            ],
            rendered);
    }

    [Fact]
    public void HybridSearchQueryRequiresTextPredicate()
    {
        Assert.Throws<ArgumentException>(() => HybridSearchQuery.FromFloat32(
            Filter.Tag("genre").Eq("crime"),
            "embedding",
            [1f, 0f],
            3));
    }

    [Fact]
    public void ReciprocalRankFusionCombinationRequiresAtLeastOneArgument()
    {
        Assert.Throws<ArgumentException>(() => new ReciprocalRankFusionHybridCombination());
    }

    private static string RenderArgument(object argument) =>
        argument is byte[] ? "<binary>" : argument.ToString()!;
}

using RedisVlDotNet.Indexes;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Indexes;

public sealed class SearchIndexCommandBuilderTests
{
    [Fact]
    public void BuildsHashCreateArguments()
    {
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new TextFieldDefinition("title", sortable: true, noStem: true, phoneticMatch: true),
                new TagFieldDefinition("genre", separator: ';', caseSensitive: true),
                new NumericFieldDefinition("rating", sortable: true),
                new GeoFieldDefinition("location"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        3,
                        initialCapacity: 1000,
                        m: 16,
                        efConstruction: 200,
                        efRuntime: 10))
            ]);

        var arguments = SearchIndexCommandBuilder.BuildCreateArguments(schema);

        Assert.Equal(
            [
                "movies-idx", "ON", "HASH", "PREFIX", "1", "movie:", "SCHEMA",
                "title", "TEXT", "NOSTEM", "PHONETIC", "dm:en", "SORTABLE",
                "genre", "TAG", "SEPARATOR", ";", "CASESENSITIVE",
                "rating", "NUMERIC", "SORTABLE",
                "location", "GEO",
                "embedding", "VECTOR", "HNSW", "14",
                "TYPE", "FLOAT32", "DIM", "3", "DISTANCE_METRIC", "COSINE",
                "INITIAL_CAP", "1000", "M", "16", "EF_CONSTRUCTION", "200", "EF_RUNTIME", "10"
            ],
            arguments.Select(static argument => argument.ToString()!).ToArray());
    }

    [Fact]
    public void BuildsJsonCreateArgumentsWithDefaultAliases()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "docs:", StorageType.Json),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("$.rating", alias: "score"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.InnerProduct,
                        2,
                        blockSize: 500))
            ]);

        var arguments = SearchIndexCommandBuilder.BuildCreateArguments(schema);

        Assert.Equal(
            [
                "docs-idx", "ON", "JSON", "PREFIX", "1", "docs:", "SCHEMA",
                "$.title", "AS", "title", "TEXT",
                "$.rating", "AS", "score", "NUMERIC",
                "$.embedding", "AS", "embedding", "VECTOR", "FLAT", "8",
                "TYPE", "FLOAT32", "DIM", "2", "DISTANCE_METRIC", "IP", "BLOCK_SIZE", "500"
            ],
            arguments.Select(static argument => argument.ToString()!).ToArray());
    }

    [Fact]
    public void BuildsCreateArgumentsWithMultiplePrefixes()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", ["docs:", "archive:"], StorageType.Hash),
            [
                new TextFieldDefinition("title")
            ]);

        var arguments = SearchIndexCommandBuilder.BuildCreateArguments(schema);

        Assert.Equal(
            [
                "docs-idx", "ON", "HASH", "PREFIX", "2", "docs:", "archive:", "SCHEMA",
                "title", "TEXT"
            ],
            arguments.Select(static argument => argument.ToString()!).ToArray());
    }

    [Fact]
    public void BuildsCreateArgumentsWithKeySeparatorAndDisabledStopwords()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "docs:", StorageType.Hash, keySeparator: '|', stopwords: []),
            [
                new TextFieldDefinition("title")
            ]);

        var arguments = SearchIndexCommandBuilder.BuildCreateArguments(schema);

        Assert.Equal(
            [
                "docs-idx", "ON", "HASH", "PREFIX", "1", "docs:", "STOPWORDS", "0", "SCHEMA",
                "title", "TEXT"
            ],
            arguments.Select(static argument => argument.ToString()!).ToArray());
    }

    [Fact]
    public void BuildsCreateArgumentsWithCustomStopwords()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "docs:", StorageType.Hash, stopwords: ["the", "a", "an"]),
            [
                new TextFieldDefinition("title")
            ]);

        var arguments = SearchIndexCommandBuilder.BuildCreateArguments(schema);

        Assert.Equal(
            [
                "docs-idx", "ON", "HASH", "PREFIX", "1", "docs:", "STOPWORDS", "3", "the", "a", "an", "SCHEMA",
                "title", "TEXT"
            ],
            arguments.Select(static argument => argument.ToString()!).ToArray());
    }

    [Fact]
    public void BuildsCreateArgumentsWithAdvancedFieldAndIndexOptions()
    {
        var schema = new SearchSchema(
            new IndexDefinition(
                "docs-idx",
                "docs:",
                StorageType.Hash,
                maxTextFields: true,
                temporarySeconds: 300,
                noOffsets: true,
                noHighlight: true,
                noFields: true,
                noFrequencies: true,
                skipInitialScan: true),
            [
                new TextFieldDefinition(
                    "title",
                    sortable: true,
                    weight: 2.5,
                    noStem: true,
                    phoneticMatch: true,
                    withSuffixTrie: true,
                    indexMissing: true,
                    indexEmpty: true,
                    unNormalizedForm: true),
                new TagFieldDefinition(
                    "genre",
                    sortable: true,
                    separator: ';',
                    caseSensitive: true,
                    withSuffixTrie: true,
                    indexMissing: true,
                    indexEmpty: true,
                    noIndex: true),
                new NumericFieldDefinition("rating", sortable: true, indexMissing: true, noIndex: true, unNormalizedForm: true),
                new GeoFieldDefinition("location", sortable: true, indexMissing: true, noIndex: true),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        3,
                        initialCapacity: 1000,
                        m: 16,
                        efConstruction: 200,
                        efRuntime: 10),
                    indexMissing: true)
            ]);

        var arguments = SearchIndexCommandBuilder.BuildCreateArguments(schema);

        Assert.Equal(
            [
                "docs-idx", "ON", "HASH", "PREFIX", "1", "docs:",
                "MAXTEXTFIELDS", "TEMPORARY", "300", "NOOFFSETS", "NOHL", "NOFIELDS", "NOFREQS", "SKIPINITIALSCAN",
                "SCHEMA",
                "title", "TEXT", "WEIGHT", "2.5", "NOSTEM", "PHONETIC", "dm:en", "WITHSUFFIXTRIE", "INDEXEMPTY", "INDEXMISSING", "SORTABLE", "UNF",
                "genre", "TAG", "SEPARATOR", ";", "CASESENSITIVE", "WITHSUFFIXTRIE", "INDEXEMPTY", "INDEXMISSING", "SORTABLE", "NOINDEX",
                "rating", "NUMERIC", "INDEXMISSING", "SORTABLE", "UNF", "NOINDEX",
                "location", "GEO", "INDEXMISSING", "SORTABLE", "NOINDEX",
                "embedding", "VECTOR", "HNSW", "14",
                "TYPE", "FLOAT32", "DIM", "3", "DISTANCE_METRIC", "COSINE",
                "INITIAL_CAP", "1000", "M", "16", "EF_CONSTRUCTION", "200", "EF_RUNTIME", "10"
            ],
            arguments.Select(static argument => argument.ToString()!).ToArray());
    }

    [Fact]
    public void BuildsDropArgumentsWithDeleteDocumentsFlag()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "docs:", StorageType.Hash),
            Array.Empty<FieldDefinition>());

        var arguments = SearchIndexCommandBuilder.BuildDropArguments(schema, deleteDocuments: true);

        Assert.Equal(["docs-idx", "DD"], arguments.Select(static argument => argument.ToString()!).ToArray());
    }

    [Fact]
    public void RejectsConflictingCreateOptions()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CreateIndexOptions(skipIfExists: true, overwrite: true));

        Assert.Contains("cannot both be enabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsesSearchIndexInfoFromRedisResult()
    {
        var rawEntries = RedisResult.Create(
            [
                RedisResult.Create((RedisValue)"index_name"),
                RedisResult.Create((RedisValue)"docs-idx"),
                RedisResult.Create((RedisValue)"index_definition"),
                RedisResult.Create(
                    [
                        RedisResult.Create((RedisValue)"key_type"),
                        RedisResult.Create((RedisValue)"HASH")
                    ])
            ]);

        var info = SearchIndexInfo.FromRedisResult(rawEntries);

        Assert.Equal("docs-idx", info.Name);
        Assert.True(info.TryGetValue("index_definition", out _));
    }

    [Fact]
    public void ParsesSearchIndexListItemsFromRedisResult()
    {
        var rawEntries = RedisResult.Create(
            [
                RedisResult.Create((RedisValue)"docs-idx"),
                RedisResult.Create((RedisValue)"movies-idx")
            ]);

        var items = SearchIndexListItem.FromRedisResult(rawEntries);

        Assert.Equal(["docs-idx", "movies-idx"], items.Select(static item => item.Name).ToArray());
    }
}

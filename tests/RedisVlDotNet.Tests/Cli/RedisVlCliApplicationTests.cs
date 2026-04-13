using System.Text;
using RedisVlDotNet.Cli;
using RedisVlDotNet.Schema;

namespace RedisVlDotNet.Tests.Cli;

public sealed class RedisVlCliApplicationTests
{
    private static readonly string SchemaFixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Schema", "Fixtures");

    [Fact]
    public async Task WritesRootHelpWhenNoArgumentsAreProvided()
    {
        var application = new RedisVlCliApplication(new FakeCliService());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await application.RunAsync([], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("redisvl <command>", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("schema", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task CreatesIndexFromInlineSchemaAndPrintsConfirmation()
    {
        var service = new FakeCliService();
        var application = new RedisVlCliApplication(service);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await application.RunAsync(
            [
                "index",
                "create",
                "--redis", "localhost:6379",
                "--name", "movies-idx",
                "--prefix", "movie:",
                "--storage", "hash",
                "--field", "text:title",
                "--field", "tag:genre",
                "--skip-if-exists"
            ],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal("movies-idx", service.LastCreateRequest!.Schema.Index.Name);
        Assert.Equal(StorageType.Hash, service.LastCreateRequest.Schema.Index.StorageType);
        Assert.Equal(["movie:"], service.LastCreateRequest.Schema.Index.Prefixes);
        Assert.Equal(["title", "genre"], service.LastCreateRequest.Schema.Fields.Select(static field => field.Name).ToArray());
        Assert.True(service.LastCreateRequest.SkipIfExists);
        Assert.Contains("Created index 'movies-idx'.", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task CreatesIndexFromSchemaFileAndPrintsConfirmation()
    {
        var service = new FakeCliService();
        var application = new RedisVlCliApplication(service);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var schemaPath = Path.Combine(SchemaFixtureDirectory, "advanced-schema.yaml");

        var exitCode = await application.RunAsync(
            [
                "index",
                "create",
                "--redis", "localhost:6379",
                "--schema", schemaPath,
                "--skip-if-exists"
            ],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(schemaPath, service.LastCreateRequest!.SchemaFilePath);
        Assert.Equal("advanced-docs-idx", service.LastCreateRequest.Schema.Index.Name);
        Assert.True(service.LastCreateRequest.SkipIfExists);
        Assert.Contains("Created index 'advanced-docs-idx'.", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task PrintsSortedIndexNames()
    {
        var service = new FakeCliService
        {
            Indexes = ["zeta-idx", "alpha-idx", "movies-idx"]
        };
        var application = new RedisVlCliApplication(service);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await application.RunAsync(["index", "list", "--redis", "localhost:6379"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            """
            alpha-idx
            movies-idx
            zeta-idx

            """,
            output.ToString().ReplaceLineEndings());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task PrintsJsonIndexInfo()
    {
        var service = new FakeCliService
        {
            Info = new IndexInfoView(
                "movies-idx",
                "Json",
                ["movie:"],
                ":",
                ["the"],
                2,
                [new IndexFieldView("text", "title", null, false)],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["indexing"] = "0"
                })
        };
        var application = new RedisVlCliApplication(service);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await application.RunAsync(
            ["index", "info", "--redis", "localhost:6379", "--name", "movies-idx"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"name\": \"movies-idx\"", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"numDocs\": 2", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task ValidatesSchemaFileAndPrintsConfirmation()
    {
        var application = new RedisVlCliApplication(new FakeCliService());
        using var output = new StringWriter();
        using var error = new StringWriter();
        var schemaPath = Path.Combine(SchemaFixtureDirectory, "basic-schema.yaml");

        var exitCode = await application.RunAsync(["schema", "validate", "--file", schemaPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains($"Schema '{schemaPath}' is valid for index 'media-idx'.", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task PrintsSchemaJsonSummary()
    {
        var application = new RedisVlCliApplication(new FakeCliService());
        using var output = new StringWriter();
        using var error = new StringWriter();
        var schemaPath = Path.Combine(SchemaFixtureDirectory, "advanced-schema.yaml");

        var exitCode = await application.RunAsync(["schema", "show", "--file", schemaPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"name\": \"advanced-docs-idx\"", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"storageType\": \"Json\"", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"fields\": [", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RejectsInvalidFieldDefinition()
    {
        var application = new RedisVlCliApplication(new FakeCliService());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await application.RunAsync(
            [
                "index",
                "create",
                "--redis", "localhost:6379",
                "--name", "movies-idx",
                "--prefix", "movie:",
                "--storage", "hash",
                "--field", "title"
            ],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid field definition 'title'.", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsCombiningSchemaAndInlineCreateOptions()
    {
        var application = new RedisVlCliApplication(new FakeCliService());
        using var output = new StringWriter();
        using var error = new StringWriter();
        var schemaPath = Path.Combine(SchemaFixtureDirectory, "basic-schema.yaml");

        var exitCode = await application.RunAsync(
            [
                "index",
                "create",
                "--redis", "localhost:6379",
                "--schema", schemaPath,
                "--name", "movies-idx"
            ],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("cannot be combined", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsInvalidSchemaFile()
    {
        var application = new RedisVlCliApplication(new FakeCliService());
        using var output = new StringWriter();
        using var error = new StringWriter();
        var schemaPath = Path.Combine(SchemaFixtureDirectory, "unsupported-schema.yaml");

        var exitCode = await application.RunAsync(["schema", "validate", "--file", schemaPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("could not be parsed", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PassesDeleteFlagsThroughToService()
    {
        var service = new FakeCliService();
        var application = new RedisVlCliApplication(service);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await application.RunAsync(
            ["index", "delete", "--redis", "localhost:6379", "--name", "movies-idx", "--drop-documents"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(("movies-idx", true), service.LastDeleteRequest);
        Assert.Contains("Deleted index 'movies-idx' and indexed documents.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequiresRedisConnectionStringWhenEnvironmentFallbackIsMissing()
    {
        var application = new RedisVlCliApplication(new FakeCliService());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await application.RunAsync(["index", "list"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("REDIS_VL_REDIS_URL", error.ToString(), StringComparison.Ordinal);
    }

    private sealed class FakeCliService : IRedisVlCliService
    {
        public CreateIndexRequest? LastCreateRequest { get; private set; }

        public (string IndexName, bool DropDocuments)? LastDeleteRequest { get; private set; }

        public IReadOnlyList<string> Indexes { get; init; } = [];

        public IndexInfoView Info { get; init; } = new(
            "default-idx",
            "Hash",
            ["doc:"],
            ":",
            null,
            0,
            [],
            new Dictionary<string, string>());

        public Task<bool> CreateIndexAsync(string redisConnectionString, CreateIndexRequest request, CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<string>> ListIndexesAsync(string redisConnectionString, CancellationToken cancellationToken = default) =>
            Task.FromResult(Indexes);

        public Task<IndexInfoView> GetIndexInfoAsync(string redisConnectionString, string indexName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Info);

        public Task<long> ClearIndexAsync(string redisConnectionString, string indexName, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(2L);

        public Task DeleteIndexAsync(string redisConnectionString, string indexName, bool dropDocuments, CancellationToken cancellationToken = default)
        {
            LastDeleteRequest = (indexName, dropDocuments);
            return Task.CompletedTask;
        }

        public Task<SearchSchema> LoadSchemaAsync(string schemaFilePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(SearchSchema.FromYamlFile(schemaFilePath));
    }
}

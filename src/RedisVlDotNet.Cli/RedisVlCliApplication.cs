using System.Globalization;
using System.Text.Json;
using RedisVl.Schema;

namespace RedisVl.Cli;

public sealed class RedisVlCliApplication
{
    public const string RedisUrlEnvironmentVariable = "REDIS_VL_REDIS_URL";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IRedisVlCliService _service;
    private readonly Func<string, string?> _environmentVariableReader;

    public RedisVlCliApplication(
        IRedisVlCliService? service = null,
        Func<string, string?>? environmentVariableReader = null)
    {
        _service = service ?? new RedisVlCliService();
        _environmentVariableReader = environmentVariableReader ?? Environment.GetEnvironmentVariable;
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var parseResult = CliParser.Parse(args, _environmentVariableReader(RedisUrlEnvironmentVariable));
        if (parseResult.ErrorText is not null)
        {
            await error.WriteLineAsync(parseResult.ErrorText).ConfigureAwait(false);
            if (parseResult.HelpText is not null)
            {
                await error.WriteLineAsync().ConfigureAwait(false);
                await error.WriteLineAsync(parseResult.HelpText).ConfigureAwait(false);
            }

            return 1;
        }

        if (parseResult.HelpText is not null)
        {
            await output.WriteLineAsync(parseResult.HelpText).ConfigureAwait(false);
            return 0;
        }

        try
        {
            await ExecuteAsync(parseResult.Request!, output, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private async Task ExecuteAsync(CliRequest request, TextWriter output, CancellationToken cancellationToken)
    {
        switch (request)
        {
            case ValidateSchemaRequest validateRequest:
            {
                var schema = await _service.LoadSchemaAsync(validateRequest.SchemaFilePath, cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync(
                    $"Schema '{validateRequest.SchemaFilePath}' is valid for index '{schema.Index.Name}'.").ConfigureAwait(false);
                break;
            }
            case ShowSchemaRequest showRequest:
            {
                var schema = await _service.LoadSchemaAsync(showRequest.SchemaFilePath, cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync(
                    JsonSerializer.Serialize(SchemaView.FromSchema(schema), JsonOptions)).ConfigureAwait(false);
                break;
            }
            case ListIndexesRequest listRequest:
            {
                var indexes = await _service.ListIndexesAsync(listRequest.RedisConnectionString, cancellationToken).ConfigureAwait(false);
                foreach (var indexName in indexes.OrderBy(static name => name, StringComparer.Ordinal))
                {
                    await output.WriteLineAsync(indexName).ConfigureAwait(false);
                }

                break;
            }
            case GetIndexInfoRequest infoRequest:
            {
                var info = await _service.GetIndexInfoAsync(infoRequest.RedisConnectionString, infoRequest.IndexName, cancellationToken)
                    .ConfigureAwait(false);
                await output.WriteLineAsync(JsonSerializer.Serialize(info, JsonOptions)).ConfigureAwait(false);
                break;
            }
            case CreateIndexRequest createRequest:
            {
                var created = await _service.CreateIndexAsync(createRequest.RedisConnectionString, createRequest, cancellationToken)
                    .ConfigureAwait(false);
                var action = created ? "Created" : "Skipped";
                await output.WriteLineAsync($"{action} index '{createRequest.Schema.Index.Name}'.").ConfigureAwait(false);
                break;
            }
            case ClearIndexRequest clearRequest:
            {
                var cleared = await _service.ClearIndexAsync(
                    clearRequest.RedisConnectionString,
                    clearRequest.IndexName,
                    clearRequest.BatchSize,
                    cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cleared {0} document(s) from index '{1}'.",
                        cleared,
                        clearRequest.IndexName)).ConfigureAwait(false);
                break;
            }
            case DeleteIndexRequest deleteRequest:
            {
                await _service.DeleteIndexAsync(
                    deleteRequest.RedisConnectionString,
                    deleteRequest.IndexName,
                    deleteRequest.DropDocuments,
                    cancellationToken).ConfigureAwait(false);
                var suffix = deleteRequest.DropDocuments ? " and indexed documents" : string.Empty;
                await output.WriteLineAsync($"Deleted index '{deleteRequest.IndexName}'{suffix}.").ConfigureAwait(false);
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported request type '{request.GetType().Name}'.");
        }
    }

    private static class CliParser
    {
        public static CliParseResult Parse(IReadOnlyList<string> args, string? fallbackRedisConnectionString)
        {
            if (args.Count == 0 || IsHelpToken(args[0]))
            {
                return CliParseResult.Help(HelpText.Root);
            }

            var group = args[0];
            if (string.Equals(group, "schema", StringComparison.OrdinalIgnoreCase))
            {
                return ParseSchema(args.Skip(1).ToArray());
            }

            if (!string.Equals(group, "index", StringComparison.OrdinalIgnoreCase))
            {
                return CliParseResult.Error($"Unknown command '{group}'.", HelpText.Root);
            }

            if (args.Count == 1 || IsHelpToken(args[1]))
            {
                return CliParseResult.Help(HelpText.Index);
            }

            var command = args[1];
            var tokens = args.Skip(2).ToArray();

            return command.ToLowerInvariant() switch
            {
                "list" => ParseList(tokens, fallbackRedisConnectionString),
                "info" => ParseInfo(tokens, fallbackRedisConnectionString),
                "create" => ParseCreate(tokens, fallbackRedisConnectionString),
                "clear" => ParseClear(tokens, fallbackRedisConnectionString),
                "delete" => ParseDelete(tokens, fallbackRedisConnectionString),
                _ => CliParseResult.Error($"Unknown index command '{command}'.", HelpText.Index)
            };
        }

        private static CliParseResult ParseSchema(IReadOnlyList<string> args)
        {
            if (args.Count == 0 || IsHelpToken(args[0]))
            {
                return CliParseResult.Help(HelpText.Schema);
            }

            var command = args[0];
            var tokens = args.Skip(1).ToArray();

            return command.ToLowerInvariant() switch
            {
                "validate" => ParseSchemaValidate(tokens),
                "show" => ParseSchemaShow(tokens),
                _ => CliParseResult.Error($"Unknown schema command '{command}'.", HelpText.Schema)
            };
        }

        private static CliParseResult ParseList(IReadOnlyList<string> tokens, string? fallbackRedisConnectionString)
        {
            if (ContainsHelp(tokens))
            {
                return CliParseResult.Help(HelpText.List);
            }

            var redis = ReadRedisConnectionString(tokens, fallbackRedisConnectionString, HelpText.List, out var error);
            return error is null
                ? CliParseResult.Success(new ListIndexesRequest(redis!))
                : error;
        }

        private static CliParseResult ParseInfo(IReadOnlyList<string> tokens, string? fallbackRedisConnectionString)
        {
            if (ContainsHelp(tokens))
            {
                return CliParseResult.Help(HelpText.Info);
            }

            var options = ParseOptions(tokens, CreateMultiValueOptions());
            if (options.ErrorText is not null)
            {
                return options;
            }

            if (!TryReadRequired(options, "--name", out var indexName, out var error))
            {
                return CliParseResult.Error(error!, HelpText.Info);
            }

            var redis = ReadRedisConnectionString(options, fallbackRedisConnectionString, HelpText.Info, out error);
            return error is null
                ? CliParseResult.Success(new GetIndexInfoRequest(redis!, indexName!))
                : CliParseResult.Error(error, HelpText.Info);
        }

        private static CliParseResult ParseCreate(IReadOnlyList<string> tokens, string? fallbackRedisConnectionString)
        {
            if (ContainsHelp(tokens))
            {
                return CliParseResult.Help(HelpText.Create);
            }

            var options = ParseOptions(tokens, CreateMultiValueOptions());
            if (options.ErrorText is not null)
            {
                return options;
            }

            var redis = ReadRedisConnectionString(options, fallbackRedisConnectionString, HelpText.Create, out var error);
            if (error is not null)
            {
                return CliParseResult.Error(error, HelpText.Create);
            }

            if (options.Values.TryGetValue("--schema", out var schemaPathValues))
            {
                if (schemaPathValues.Count > 1)
                {
                    return CliParseResult.Error("The '--schema' option can only be provided once.", HelpText.Create);
                }

                var conflictingOptions = new[] { "--name", "--prefix", "--storage", "--field" }
                    .Where(options.Values.ContainsKey)
                    .ToArray();
                if (conflictingOptions.Length > 0)
                {
                    return CliParseResult.Error(
                        $"The '--schema' option cannot be combined with {string.Join(", ", conflictingOptions)}.",
                        HelpText.Create);
                }

                if (!TryLoadSchema(schemaPathValues[0], HelpText.Create, out var loadedSchema, out var schemaError))
                {
                    return schemaError!;
                }

                return CliParseResult.Success(
                    new CreateIndexRequest(
                        redis!,
                        loadedSchema!,
                        schemaPathValues[0].Trim(),
                        Overwrite: options.Flags.Contains("--overwrite"),
                        DropDocuments: options.Flags.Contains("--drop-documents"),
                        SkipIfExists: options.Flags.Contains("--skip-if-exists")));
            }

            if (!TryReadRequired(options, "--name", out var indexName, out error))
            {
                return CliParseResult.Error(error!, HelpText.Create);
            }

            var prefixes = options.Values.TryGetValue("--prefix", out var prefixValues)
                ? prefixValues.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()).ToArray()
                : [];
            if (prefixes.Length == 0)
            {
                return CliParseResult.Error("The '--prefix' option is required at least once.", HelpText.Create);
            }

            if (!TryReadRequired(options, "--storage", out var storageText, out error))
            {
                return CliParseResult.Error(error!, HelpText.Create);
            }

            if (!Enum.TryParse<StorageType>(storageText, ignoreCase: true, out var storageType))
            {
                return CliParseResult.Error(
                    $"Unsupported storage type '{storageText}'. Expected 'hash' or 'json'.",
                    HelpText.Create);
            }

            var fieldSpecs = options.Values.TryGetValue("--field", out var fields) ? fields : [];
            if (fieldSpecs.Count == 0)
            {
                return CliParseResult.Error("The '--field' option is required at least once.", HelpText.Create);
            }

            var parsedFields = new List<CliFieldDefinition>(fieldSpecs.Count);
            foreach (var fieldSpec in fieldSpecs)
            {
                if (!CliFieldDefinition.TryParse(fieldSpec, out var field, out error))
                {
                    return CliParseResult.Error(error!, HelpText.Create);
                }

                parsedFields.Add(field!);
            }

            return CliParseResult.Success(
                new CreateIndexRequest(
                    redis!,
                    BuildInlineSchema(indexName!, prefixes, storageType, parsedFields),
                    null,
                    Overwrite: options.Flags.Contains("--overwrite"),
                    DropDocuments: options.Flags.Contains("--drop-documents"),
                    SkipIfExists: options.Flags.Contains("--skip-if-exists")));
        }

        private static CliParseResult ParseSchemaValidate(IReadOnlyList<string> tokens)
        {
            if (ContainsHelp(tokens))
            {
                return CliParseResult.Help(HelpText.SchemaValidate);
            }

            return TryParseSchemaFileRequest(tokens, HelpText.SchemaValidate, static path => new ValidateSchemaRequest(path));
        }

        private static CliParseResult ParseSchemaShow(IReadOnlyList<string> tokens)
        {
            if (ContainsHelp(tokens))
            {
                return CliParseResult.Help(HelpText.SchemaShow);
            }

            return TryParseSchemaFileRequest(tokens, HelpText.SchemaShow, static path => new ShowSchemaRequest(path));
        }

        private static CliParseResult ParseClear(IReadOnlyList<string> tokens, string? fallbackRedisConnectionString)
        {
            if (ContainsHelp(tokens))
            {
                return CliParseResult.Help(HelpText.Clear);
            }

            var options = ParseOptions(tokens, CreateMultiValueOptions());
            if (options.ErrorText is not null)
            {
                return options;
            }

            if (!TryReadRequired(options, "--name", out var indexName, out var error))
            {
                return CliParseResult.Error(error!, HelpText.Clear);
            }

            var batchSize = 1000;
            if (options.Values.TryGetValue("--batch-size", out var batchSizeValues))
            {
                var rawValue = batchSizeValues[^1];
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out batchSize) || batchSize <= 0)
                {
                    return CliParseResult.Error("The '--batch-size' option must be a positive integer.", HelpText.Clear);
                }
            }

            var redis = ReadRedisConnectionString(options, fallbackRedisConnectionString, HelpText.Clear, out error);
            return error is null
                ? CliParseResult.Success(new ClearIndexRequest(redis!, indexName!, batchSize))
                : CliParseResult.Error(error, HelpText.Clear);
        }

        private static CliParseResult ParseDelete(IReadOnlyList<string> tokens, string? fallbackRedisConnectionString)
        {
            if (ContainsHelp(tokens))
            {
                return CliParseResult.Help(HelpText.Delete);
            }

            var options = ParseOptions(tokens, CreateMultiValueOptions());
            if (options.ErrorText is not null)
            {
                return options;
            }

            if (!TryReadRequired(options, "--name", out var indexName, out var error))
            {
                return CliParseResult.Error(error!, HelpText.Delete);
            }

            var redis = ReadRedisConnectionString(options, fallbackRedisConnectionString, HelpText.Delete, out error);
            return error is null
                ? CliParseResult.Success(new DeleteIndexRequest(redis!, indexName!, options.Flags.Contains("--drop-documents")))
                : CliParseResult.Error(error, HelpText.Delete);
        }

        private static CliParseResult ParseOptions(IReadOnlyList<string> tokens, IReadOnlySet<string> allowMultiple)
        {
            var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
                if (!token.StartsWith("--", StringComparison.Ordinal))
                {
                    return CliParseResult.Error($"Unexpected argument '{token}'.");
                }

                if (token is "--overwrite" or "--drop-documents" or "--skip-if-exists")
                {
                    flags.Add(token);
                    continue;
                }

                if (index + 1 >= tokens.Count || tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return CliParseResult.Error($"The '{token}' option requires a value.");
                }

                var value = tokens[++index];
                if (!allowMultiple.Contains(token) && values.ContainsKey(token))
                {
                    return CliParseResult.Error($"The '{token}' option can only be provided once.");
                }

                if (!values.TryGetValue(token, out var entries))
                {
                    entries = [];
                    values[token] = entries;
                }

                entries.Add(value);
            }

            return new CliParseResult(null, null, null, values, flags);
        }

        private static string? ReadRedisConnectionString(
            CliParseResult options,
            string? fallbackRedisConnectionString,
            string helpText,
            out string? error)
        {
            var redis = options.Values.TryGetValue("--redis", out var entries)
                ? entries[^1]
                : fallbackRedisConnectionString;

            if (string.IsNullOrWhiteSpace(redis))
            {
                error = $"The '--redis' option is required when {RedisUrlEnvironmentVariable} is not set.";
                return null;
            }

            error = null;
            return redis.Trim();
        }

        private static string? ReadRedisConnectionString(
            IReadOnlyList<string> tokens,
            string? fallbackRedisConnectionString,
            string helpText,
            out CliParseResult? error)
        {
            var options = ParseOptions(tokens, CreateMultiValueOptions());
            if (options.ErrorText is not null)
            {
                error = CliParseResult.Error(options.ErrorText, helpText);
                return null;
            }

            var redis = ReadRedisConnectionString(options, fallbackRedisConnectionString, helpText, out var redisError);
            error = redisError is null ? null : CliParseResult.Error(redisError, helpText);
            return redis;
        }

        private static bool TryReadRequired(CliParseResult options, string optionName, out string? value, out string? error)
        {
            if (!options.Values.TryGetValue(optionName, out var entries) || entries.Count == 0 || string.IsNullOrWhiteSpace(entries[^1]))
            {
                value = null;
                error = $"The '{optionName}' option is required.";
                return false;
            }

            value = entries[^1].Trim();
            error = null;
            return true;
        }

        private static CliParseResult TryParseSchemaFileRequest(
            IReadOnlyList<string> tokens,
            string helpText,
            Func<string, CliRequest> factory)
        {
            var options = ParseOptions(tokens, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            if (options.ErrorText is not null)
            {
                return CliParseResult.Error(options.ErrorText, helpText);
            }

            if (!TryReadRequired(options, "--file", out var schemaPath, out var error))
            {
                return CliParseResult.Error(error!, helpText);
            }

            return TryLoadSchema(schemaPath!, helpText, out _, out var schemaError)
                ? CliParseResult.Success(factory(schemaPath!))
                : schemaError!;
        }

        private static bool TryLoadSchema(
            string rawPath,
            string helpText,
            out SearchSchema? schema,
            out CliParseResult? error)
        {
            try
            {
                schema = SearchSchema.FromYamlFile(rawPath.Trim());
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                schema = null;
                error = CliParseResult.Error(exception.Message, helpText);
                return false;
            }
        }

        private static SearchSchema BuildInlineSchema(
            string indexName,
            IReadOnlyList<string> prefixes,
            StorageType storageType,
            IReadOnlyList<CliFieldDefinition> fields)
        {
            return new SearchSchema(
                new IndexDefinition(indexName, prefixes, storageType),
                fields.Select(MapFieldDefinition));
        }

        private static FieldDefinition MapFieldDefinition(CliFieldDefinition field)
        {
            return field.Type switch
            {
                "text" => new TextFieldDefinition(field.Name),
                "tag" => new TagFieldDefinition(field.Name),
                "numeric" => new NumericFieldDefinition(field.Name),
                "geo" => new GeoFieldDefinition(field.Name),
                _ => throw new ArgumentException(
                    $"Unsupported field type '{field.Type}'. Supported types: text, tag, numeric, geo.",
                    nameof(field))
            };
        }

        private static bool ContainsHelp(IReadOnlyList<string> tokens) =>
            tokens.Any(IsHelpToken);

        private static IReadOnlySet<string> CreateMultiValueOptions() =>
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "--prefix",
                "--field"
            };

        private static bool IsHelpToken(string token) =>
            string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase);
    }

    private static class HelpText
    {
        public const string Root =
            """
            redisvl

            Usage:
              redisvl <command> [options]

            Commands:
              index   Manage Redis search indexes.
              schema  Validate or inspect YAML schema files.

            Use 'redisvl <command> --help' for command-specific details.
            """;

        public const string Index =
            """
            redisvl index

            Usage:
              redisvl index <command> [options]

            Commands:
              create
              info
              list
              clear
              delete
            """;

        public const string Schema =
            """
            redisvl schema

            Usage:
              redisvl schema <command> [options]

            Commands:
              validate
              show
            """;

        public const string SchemaValidate =
            """
            redisvl schema validate

            Usage:
              redisvl schema validate --file <schema-path>

            Options:
              --file <schema-path>  Path to a YAML schema file to validate.
            """;

        public const string SchemaShow =
            """
            redisvl schema show

            Usage:
              redisvl schema show --file <schema-path>

            Options:
              --file <schema-path>  Path to a YAML schema file to load and print as JSON.
            """;

        public const string List =
            """
            redisvl index list

            Usage:
              redisvl index list [--redis <connection-string>]

            Options:
              --redis <connection-string>  Redis connection string. Falls back to REDIS_VL_REDIS_URL.
            """;

        public const string Info =
            """
            redisvl index info

            Usage:
              redisvl index info --name <index-name> [--redis <connection-string>]

            Options:
              --name <index-name>          Index name to inspect.
              --redis <connection-string>  Redis connection string. Falls back to REDIS_VL_REDIS_URL.
            """;

        public const string Create =
            """
            redisvl index create

            Usage:
              redisvl index create (--schema <schema-path> | --name <index-name> --prefix <key-prefix> --storage <hash|json> --field <type:name>) [options]

            Options:
              --schema <schema-path>       YAML schema file to load for index creation.
              --name <index-name>          Index name to create.
              --prefix <key-prefix>        Key prefix to index. Repeat for multiple prefixes.
              --storage <hash|json>        Redis storage type for indexed documents.
              --field <type:name>          Field definition. Repeat for multiple fields. Supported types: text, tag, numeric, geo.
              --redis <connection-string>  Redis connection string. Falls back to REDIS_VL_REDIS_URL.
              --overwrite                  Drop an existing index before creating it.
              --drop-documents             With --overwrite, drop indexed documents too.
              --skip-if-exists             Exit successfully without creating when the index already exists.
            """;

        public const string Clear =
            """
            redisvl index clear

            Usage:
              redisvl index clear --name <index-name> [--batch-size <count>] [--redis <connection-string>]

            Options:
              --name <index-name>          Index name to clear.
              --batch-size <count>         Redis SCAN delete batch size. Defaults to 1000.
              --redis <connection-string>  Redis connection string. Falls back to REDIS_VL_REDIS_URL.
            """;

        public const string Delete =
            """
            redisvl index delete

            Usage:
              redisvl index delete --name <index-name> [--drop-documents] [--redis <connection-string>]

            Options:
              --name <index-name>          Index name to drop.
              --drop-documents             Delete indexed documents together with the index definition.
              --redis <connection-string>  Redis connection string. Falls back to REDIS_VL_REDIS_URL.
            """;
    }
}

public interface IRedisVlCliService
{
    Task<bool> CreateIndexAsync(string redisConnectionString, CreateIndexRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListIndexesAsync(string redisConnectionString, CancellationToken cancellationToken = default);

    Task<IndexInfoView> GetIndexInfoAsync(string redisConnectionString, string indexName, CancellationToken cancellationToken = default);

    Task<long> ClearIndexAsync(string redisConnectionString, string indexName, int batchSize, CancellationToken cancellationToken = default);

    Task DeleteIndexAsync(string redisConnectionString, string indexName, bool dropDocuments, CancellationToken cancellationToken = default);

    Task<SearchSchema> LoadSchemaAsync(string schemaFilePath, CancellationToken cancellationToken = default);
}

public abstract record CliRequest;

public sealed record ValidateSchemaRequest(string SchemaFilePath) : CliRequest;

public sealed record ShowSchemaRequest(string SchemaFilePath) : CliRequest;

public sealed record ListIndexesRequest(string RedisConnectionString) : CliRequest;

public sealed record GetIndexInfoRequest(string RedisConnectionString, string IndexName) : CliRequest;

public sealed record CreateIndexRequest(
    string RedisConnectionString,
    SearchSchema Schema,
    string? SchemaFilePath,
    bool Overwrite,
    bool DropDocuments,
    bool SkipIfExists) : CliRequest;

public sealed record ClearIndexRequest(string RedisConnectionString, string IndexName, int BatchSize)
    : CliRequest;

public sealed record DeleteIndexRequest(string RedisConnectionString, string IndexName, bool DropDocuments)
    : CliRequest;

public sealed record CliFieldDefinition(string Type, string Name)
{
    public static bool TryParse(string rawValue, out CliFieldDefinition? field, out string? error)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            field = null;
            error = "Field definitions cannot be blank.";
            return false;
        }

        var parts = rawValue.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            field = null;
            error = $"Invalid field definition '{rawValue}'. Expected '<type>:<name>'.";
            return false;
        }

        field = new CliFieldDefinition(parts[0].ToLowerInvariant(), parts[1]);
        error = null;
        return true;
    }
}

public sealed record IndexInfoView(
    string Name,
    string StorageType,
    IReadOnlyList<string> Prefixes,
    string KeySeparator,
    IReadOnlyList<string>? Stopwords,
    long? NumDocs,
    IReadOnlyList<IndexFieldView> Fields,
    IReadOnlyDictionary<string, string> Scalars);

public sealed record IndexFieldView(string Type, string Name, string? Alias, bool Sortable);

public sealed record SchemaView(
    string Name,
    string StorageType,
    IReadOnlyList<string> Prefixes,
    string KeySeparator,
    IReadOnlyList<string>? Stopwords,
    IReadOnlyList<IndexFieldView> Fields)
{
    public static SchemaView FromSchema(SearchSchema schema) =>
        new(
            schema.Index.Name,
            schema.Index.StorageType.ToString(),
            schema.Index.Prefixes.ToArray(),
            schema.Index.KeySeparator.ToString(CultureInfo.InvariantCulture),
            schema.Index.Stopwords?.ToArray(),
            schema.Fields.Select(
                static field => new IndexFieldView(
                    field switch
                    {
                        TextFieldDefinition => "text",
                        TagFieldDefinition => "tag",
                        NumericFieldDefinition => "numeric",
                        GeoFieldDefinition => "geo",
                        VectorFieldDefinition => "vector",
                        _ => field.GetType().Name
                    },
                    field.Name,
                    field.Alias,
                    field.Sortable)).ToArray());
}

public sealed class CliParseResult
{
    public CliParseResult(
        CliRequest? request,
        string? helpText,
        string? errorText,
        Dictionary<string, List<string>>? values = null,
        HashSet<string>? flags = null)
    {
        Request = request;
        HelpText = helpText;
        ErrorText = errorText;
        Values = values ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        Flags = flags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public CliRequest? Request { get; }

    public string? HelpText { get; }

    public string? ErrorText { get; }

    public Dictionary<string, List<string>> Values { get; }

    public HashSet<string> Flags { get; }

    public static CliParseResult Success(CliRequest request) => new(request, null, null);

    public static CliParseResult Help(string helpText) => new(null, helpText, null);

    public static CliParseResult Error(string errorText, string? helpText = null) => new(null, helpText, errorText);
}

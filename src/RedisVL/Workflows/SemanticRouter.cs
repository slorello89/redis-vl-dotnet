using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RedisVL.Caches;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using RedisVL.Vectorizers;
using StackExchange.Redis;

namespace RedisVL.Workflows;

public sealed class SemanticRouter
{
    private const string RouteThresholdFieldName = "routeThreshold";
    private const string MetadataFieldName = "metadata";
    private const string DistanceAlias = "distance";

    private readonly IDatabase _database;
    private readonly SearchIndex _index;
    private readonly JsonSerializerOptions _serializerOptions;

    public SemanticRouter(IDatabase database, SemanticRouterOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _database = database;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Options = options;
        _index = new SearchIndex(
            database,
            new SearchSchema(
                new IndexDefinition(CreateIndexName(options), CreateKeyPrefix(options), StorageType.Hash),
                [
                    new TagFieldDefinition(options.RouteNameFieldName, caseSensitive: true),
                    new TextFieldDefinition(options.ReferenceFieldName),
                    new VectorFieldDefinition(options.EmbeddingFieldName, options.EmbeddingFieldAttributes)
                ]));
    }

    public SemanticRouterOptions Options { get; }

    public string Name => Options.Name;

    public string? KeyNamespace => Options.KeyNamespace;

    public double DistanceThreshold => Options.DistanceThreshold;

    public RoutingConfig RoutingConfig => Options.RoutingConfig;

    public Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default) =>
        _index.CreateAsync(options, cancellationToken);

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        _index.ExistsAsync(cancellationToken);

    public Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default) =>
        _index.DropAsync(deleteDocuments, cancellationToken);

    public async Task<string> AddRouteAsync(
        string routeName,
        string reference,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        var normalizedRouteName = NormalizeRouteName(routeName);
        var normalizedReference = NormalizeReference(reference);
        ArgumentNullException.ThrowIfNull(embedding);

        cancellationToken.ThrowIfCancellationRequested();

        return await WriteReferenceAsync(
            normalizedRouteName,
            normalizedReference,
            embedding,
            distanceThreshold: null,
            metadata: null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> AddRouteAsync(
        string routeName,
        string reference,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var embedding = await vectorizer.VectorizeAsync(NormalizeReference(reference), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await AddRouteAsync(routeName, reference, embedding, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stores a route's reference phrases using precomputed embeddings, persisting the optional per-route
    /// distance threshold and metadata on each reference. The returned key list is aligned to
    /// <see cref="Route.References" />.
    /// </summary>
    public async Task<IReadOnlyList<string>> AddRouteAsync(
        Route route,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(embeddings);

        if (embeddings.Count != route.References.Count)
        {
            throw new ArgumentException(
                $"Expected {route.References.Count} embeddings to match the route's references but received {embeddings.Count}.",
                nameof(embeddings));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var metadata = SerializeMetadata(route.Metadata);
        var keys = new List<string>(route.References.Count);
        for (var index = 0; index < route.References.Count; index++)
        {
            keys.Add(await WriteReferenceAsync(
                route.Name,
                route.References[index],
                embeddings[index],
                route.DistanceThreshold,
                metadata,
                cancellationToken).ConfigureAwait(false));
        }

        return keys;
    }

    /// <summary>
    /// Stores a route's reference phrases, vectorizing them in a single batch via <paramref name="vectorizer" />.
    /// </summary>
    public async Task<IReadOnlyList<string>> AddRouteAsync(
        Route route,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(vectorizer);

        var embeddings = await vectorizer.VectorizeManyAsync(route.References, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await AddRouteAsync(route, embeddings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds reference phrases to an existing route using precomputed embeddings. Existing per-route threshold
    /// and metadata are left unchanged. The returned key list is aligned to <paramref name="references" />.
    /// </summary>
    public async Task<IReadOnlyList<string>> AddRouteReferencesAsync(
        string routeName,
        IReadOnlyList<string> references,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken = default)
    {
        var normalizedRouteName = NormalizeRouteName(routeName);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(embeddings);

        if (references.Count == 0)
        {
            throw new ArgumentException("At least one reference is required.", nameof(references));
        }

        if (embeddings.Count != references.Count)
        {
            throw new ArgumentException(
                $"Expected {references.Count} embeddings to match the supplied references but received {embeddings.Count}.",
                nameof(embeddings));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var keys = new List<string>(references.Count);
        for (var index = 0; index < references.Count; index++)
        {
            keys.Add(await WriteReferenceAsync(
                normalizedRouteName,
                NormalizeReference(references[index]),
                embeddings[index],
                distanceThreshold: null,
                metadata: null,
                cancellationToken).ConfigureAwait(false));
        }

        return keys;
    }

    public async Task<IReadOnlyList<string>> AddRouteReferencesAsync(
        string routeName,
        IReadOnlyList<string> references,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default)
    {
        NormalizeRouteName(routeName);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(vectorizer);

        var embeddings = await vectorizer.VectorizeManyAsync(references, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await AddRouteReferencesAsync(routeName, references, embeddings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns the stored references for a route, or an empty list when the route has none.</summary>
    public async Task<IReadOnlyList<RouteReference>> GetRouteReferencesAsync(
        string routeName,
        CancellationToken cancellationToken = default)
    {
        var normalizedRouteName = NormalizeRouteName(routeName);
        cancellationToken.ThrowIfCancellationRequested();

        var results = await _index.SearchAsync(
            new FilterQuery(
                Filter.Tag(Options.RouteNameFieldName).Eq(normalizedRouteName),
                returnFields: [Options.RouteNameFieldName, Options.ReferenceFieldName],
                limit: RoutingConfig.MaxReferenceCandidates),
            cancellationToken).ConfigureAwait(false);

        var references = new List<RouteReference>(results.Documents.Count);
        foreach (var document in results.Documents)
        {
            if (document.TryGetValue(Options.ReferenceFieldName, out var reference))
            {
                references.Add(new RouteReference(document.Id, normalizedRouteName, reference.ToString()!));
            }
        }

        return references;
    }

    /// <summary>
    /// Reconstructs a route from its stored references, including any persisted metadata and per-route
    /// threshold. Returns <see langword="null" /> when the route has no references.
    /// </summary>
    public async Task<Route?> GetRouteAsync(string routeName, CancellationToken cancellationToken = default)
    {
        var normalizedRouteName = NormalizeRouteName(routeName);
        cancellationToken.ThrowIfCancellationRequested();

        var results = await _index.SearchAsync(
            new FilterQuery(
                Filter.Tag(Options.RouteNameFieldName).Eq(normalizedRouteName),
                returnFields: [Options.ReferenceFieldName, RouteThresholdFieldName, MetadataFieldName],
                limit: RoutingConfig.MaxReferenceCandidates),
            cancellationToken).ConfigureAwait(false);

        if (results.Documents.Count == 0)
        {
            return null;
        }

        var references = new List<string>(results.Documents.Count);
        double? distanceThreshold = null;
        IReadOnlyDictionary<string, object?>? metadata = null;
        foreach (var document in results.Documents)
        {
            if (document.TryGetValue(Options.ReferenceFieldName, out var reference))
            {
                references.Add(reference.ToString()!);
            }

            if (distanceThreshold is null &&
                document.TryGetValue(RouteThresholdFieldName, out var thresholdValue) &&
                double.TryParse(thresholdValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedThreshold))
            {
                distanceThreshold = parsedThreshold;
            }

            if (metadata is null &&
                document.TryGetValue(MetadataFieldName, out var metadataValue) &&
                !metadataValue.IsNull)
            {
                metadata = DeserializeMetadata(metadataValue.ToString());
            }
        }

        return references.Count == 0
            ? null
            : new Route(normalizedRouteName, references, metadata, distanceThreshold);
    }

    /// <summary>Deletes specific references from a route. Returns the number of references actually removed.</summary>
    public async Task<long> DeleteRouteReferencesAsync(
        string routeName,
        IEnumerable<string> references,
        CancellationToken cancellationToken = default)
    {
        var normalizedRouteName = NormalizeRouteName(routeName);
        ArgumentNullException.ThrowIfNull(references);

        var keys = references
            .Select(reference => CreateKey(normalizedRouteName, NormalizeReference(reference)))
            .ToArray();

        if (keys.Length == 0)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await _database.KeyDeleteAsync(keys).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes every reference belonging to a route. Returns the number of references removed.</summary>
    public async Task<long> DeleteRouteAsync(string routeName, CancellationToken cancellationToken = default)
    {
        var normalizedRouteName = NormalizeRouteName(routeName);
        cancellationToken.ThrowIfCancellationRequested();

        var results = await _index.SearchAsync(
            new FilterQuery(
                Filter.Tag(Options.RouteNameFieldName).Eq(normalizedRouteName),
                returnFields: [Options.RouteNameFieldName],
                limit: RoutingConfig.MaxReferenceCandidates),
            cancellationToken).ConfigureAwait(false);

        if (results.Documents.Count == 0)
        {
            return 0;
        }

        var keys = results.Documents.Select(document => (RedisKey)document.Id).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.KeyDeleteAsync(keys).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SemanticRouteMatch?> RouteAsync(
        string input,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        var normalizedInput = NormalizeInput(input);
        ArgumentNullException.ThrowIfNull(embedding);

        cancellationToken.ThrowIfCancellationRequested();

        var results = await _index.SearchAsync<SemanticRouteDocument>(
            VectorRangeQuery.FromFloat32(
                Options.EmbeddingFieldName,
                embedding,
                DistanceThreshold,
                returnFields: [Options.RouteNameFieldName, Options.ReferenceFieldName],
                scoreAlias: DistanceAlias,
                limit: 1),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var match = results.Documents.FirstOrDefault();
        return match is null
            ? null
            : new SemanticRouteMatch(normalizedInput, match.RouteName, match.Reference, match.Distance);
    }

    public async Task<SemanticRouteMatch?> RouteAsync(
        string input,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var normalizedInput = NormalizeInput(input);
        var embedding = await vectorizer.VectorizeAsync(normalizedInput, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await RouteAsync(normalizedInput, embedding, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Classifies an input across all routes and returns up to <paramref name="maxResults" /> matches ordered
    /// nearest-first. Each route's reference distances are combined with <paramref name="aggregationMethod" />,
    /// and a route is only returned when its aggregated distance is within its effective threshold (the per-route
    /// threshold when set, otherwise the router default, capped by the router default). An empty list is a miss.
    /// </summary>
    public async Task<IReadOnlyList<SemanticRouterMatch>> RouteManyAsync(
        string input,
        float[] embedding,
        int? maxResults = null,
        DistanceAggregationMethod? aggregationMethod = null,
        CancellationToken cancellationToken = default)
    {
        NormalizeInput(input);
        ArgumentNullException.ThrowIfNull(embedding);

        var effectiveMaxResults = maxResults ?? RoutingConfig.MaxResults;
        if (effectiveMaxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "Routing max results must be greater than zero.");
        }

        var method = aggregationMethod ?? RoutingConfig.AggregationMethod;

        cancellationToken.ThrowIfCancellationRequested();

        var results = await _index.SearchAsync(
            VectorRangeQuery.FromFloat32(
                Options.EmbeddingFieldName,
                embedding,
                DistanceThreshold,
                returnFields: [Options.RouteNameFieldName, RouteThresholdFieldName],
                scoreAlias: DistanceAlias,
                limit: RoutingConfig.MaxReferenceCandidates),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return AggregateMatches(results, method, effectiveMaxResults);
    }

    public async Task<IReadOnlyList<SemanticRouterMatch>> RouteManyAsync(
        string input,
        ITextVectorizer vectorizer,
        int? maxResults = null,
        DistanceAggregationMethod? aggregationMethod = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var normalizedInput = NormalizeInput(input);
        var embedding = await vectorizer.VectorizeAsync(normalizedInput, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await RouteManyAsync(normalizedInput, embedding, maxResults, aggregationMethod, cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<SemanticRouterMatch> AggregateMatches(
        SearchResults results,
        DistanceAggregationMethod method,
        int maxResults)
    {
        var groups = new Dictionary<string, RouteAccumulator>(StringComparer.Ordinal);
        foreach (var document in results.Documents)
        {
            if (!document.TryGetValue(Options.RouteNameFieldName, out var routeNameValue) ||
                !document.TryGetValue(DistanceAlias, out var distanceValue) ||
                !double.TryParse(distanceValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
            {
                continue;
            }

            var routeName = routeNameValue.ToString()!;
            if (!groups.TryGetValue(routeName, out var accumulator))
            {
                accumulator = new RouteAccumulator(ResolveRouteThreshold(document));
                groups[routeName] = accumulator;
            }

            accumulator.Add(distance);
        }

        var matches = new List<SemanticRouterMatch>(groups.Count);
        foreach (var (routeName, accumulator) in groups)
        {
            var aggregated = accumulator.Aggregate(method);
            if (aggregated <= accumulator.Threshold)
            {
                matches.Add(new SemanticRouterMatch(routeName, aggregated));
            }
        }

        matches.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
        return matches.Count <= maxResults ? matches : matches.GetRange(0, maxResults);
    }

    private double ResolveRouteThreshold(SearchDocument document)
    {
        if (document.TryGetValue(RouteThresholdFieldName, out var thresholdValue) &&
            double.TryParse(thresholdValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold))
        {
            return Math.Min(threshold, DistanceThreshold);
        }

        return DistanceThreshold;
    }

    private async Task<string> WriteReferenceAsync(
        string routeName,
        string reference,
        float[] embedding,
        double? distanceThreshold,
        string? metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        var key = CreateKey(routeName, reference);
        var entries = new List<HashEntry>
        {
            new(Options.RouteNameFieldName, routeName),
            new(Options.ReferenceFieldName, reference),
            new(Options.EmbeddingFieldName, EmbeddingsCache.EncodeFloat32(embedding))
        };

        if (distanceThreshold is double threshold)
        {
            entries.Add(new HashEntry(RouteThresholdFieldName, threshold.ToString("G", CultureInfo.InvariantCulture)));
        }

        if (metadata is not null)
        {
            entries.Add(new HashEntry(MetadataFieldName, metadata));
        }

        await _database.HashSetAsync(key, entries.ToArray()).WaitAsync(cancellationToken).ConfigureAwait(false);
        return key!;
    }

    internal RedisKey CreateKey(string routeName, string reference)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{routeName}\n{reference}"))).ToLowerInvariant();
        return $"{CreateKeyPrefix(Options)}{hash}";
    }

    private string? SerializeMetadata(IReadOnlyDictionary<string, object?>? metadata) =>
        metadata is null || metadata.Count == 0 ? null : JsonSerializer.Serialize(metadata, _serializerOptions);

    private IReadOnlyDictionary<string, object?>? DeserializeMetadata(string? metadata) =>
        string.IsNullOrEmpty(metadata)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(metadata, _serializerOptions);

    private static string CreateIndexName(SemanticRouterOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic-router:{options.Name}"
            : $"semantic-router:{options.Name}:{options.KeyNamespace}";

    private static string CreateKeyPrefix(SemanticRouterOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic-router:{options.Name}:"
            : $"semantic-router:{options.Name}:{options.KeyNamespace}:";

    private static string NormalizeInput(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        return input;
    }

    private static string NormalizeRouteName(string routeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);
        if (routeName.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException("Route name must not contain a comma.", nameof(routeName));
        }

        return routeName;
    }

    private static string NormalizeReference(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return reference;
    }

    private sealed class RouteAccumulator(double threshold)
    {
        private double _sum;
        private double _min = double.PositiveInfinity;
        private int _count;

        public double Threshold { get; } = threshold;

        public void Add(double distance)
        {
            _sum += distance;
            _count++;
            if (distance < _min)
            {
                _min = distance;
            }
        }

        public double Aggregate(DistanceAggregationMethod method) => method switch
        {
            DistanceAggregationMethod.Minimum => _min,
            DistanceAggregationMethod.Sum => _sum,
            _ => _sum / _count
        };
    }

    private sealed record SemanticRouteDocument(string RouteName, string Reference, double Distance);
}

public sealed record SemanticRouteMatch(string Input, string RouteName, string Reference, double Distance);

/// <summary>A route matched by <see cref="SemanticRouter.RouteManyAsync(string, float[], int?, DistanceAggregationMethod?, System.Threading.CancellationToken)" />, carrying the aggregated distance.</summary>
public sealed record SemanticRouterMatch(string RouteName, double Distance);

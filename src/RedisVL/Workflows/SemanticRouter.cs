using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RedisVL.Caches;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Internal;
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

    // Per-route config (threshold, metadata) lives in a dedicated key rather than being denormalized
    // onto every reference. This infix keeps a route's config key distinct from its reference keys
    // (which are bare SHA-256 hex hashes) so the two never collide, while sharing the index prefix so
    // FT.DROPINDEX DD and ClearAsync clean them up alongside references.
    private const string ConfigKeyInfix = "__config__";

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

        // A bare reference add carries no route-level config, so it only writes the reference and
        // leaves any existing per-route threshold/metadata untouched.
        return await WriteReferenceAsync(normalizedRouteName, normalizedReference, embedding, cancellationToken)
            .ConfigureAwait(false);
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
    /// distance threshold and metadata under a dedicated route-config key (not on each reference). The
    /// returned key list is aligned to <see cref="Route.References" />.
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

        // Persist the route-level config once, before the references, so a query that races the write
        // never sees references indexed under the router default while the stricter per-route threshold
        // is still pending. Rewriting a route replaces its config wholesale.
        await WriteRouteConfigAsync(route.Name, route.DistanceThreshold, SerializeMetadata(route.Metadata), cancellationToken)
            .ConfigureAwait(false);

        return await RedisBatch.RunAsync(
            route.References,
            (reference, index, token) => WriteReferenceAsync(route.Name, reference, embeddings[index], token),
            cancellationToken).ConfigureAwait(false);
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
    /// Adds reference phrases to an existing route using precomputed embeddings. The route's per-route
    /// threshold and metadata (stored under a dedicated route-config key) are left unchanged. The returned
    /// key list is aligned to <paramref name="references" />.
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

        // Normalize every reference up front so a blank reference fails the whole call before any
        // write, then pipeline the HSET commands instead of awaiting one per reference.
        var normalizedReferences = new string[references.Count];
        for (var index = 0; index < references.Count; index++)
        {
            normalizedReferences[index] = NormalizeReference(references[index]);
        }

        return await RedisBatch.RunAsync(
            normalizedReferences,
            (reference, index, token) => WriteReferenceAsync(normalizedRouteName, reference, embeddings[index], token),
            cancellationToken).ConfigureAwait(false);
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
    /// threshold (read from the dedicated route-config key). Returns <see langword="null" /> when the route
    /// has no references.
    /// </summary>
    public async Task<Route?> GetRouteAsync(string routeName, CancellationToken cancellationToken = default)
    {
        var normalizedRouteName = NormalizeRouteName(routeName);
        cancellationToken.ThrowIfCancellationRequested();

        var results = await _index.SearchAsync(
            new FilterQuery(
                Filter.Tag(Options.RouteNameFieldName).Eq(normalizedRouteName),
                returnFields: [Options.ReferenceFieldName],
                limit: RoutingConfig.MaxReferenceCandidates),
            cancellationToken).ConfigureAwait(false);

        if (results.Documents.Count == 0)
        {
            return null;
        }

        var references = new List<string>(results.Documents.Count);
        foreach (var document in results.Documents)
        {
            if (document.TryGetValue(Options.ReferenceFieldName, out var reference))
            {
                references.Add(reference.ToString()!);
            }
        }

        if (references.Count == 0)
        {
            return null;
        }

        var (distanceThreshold, metadata) = await LoadRouteConfigAsync(normalizedRouteName, cancellationToken).ConfigureAwait(false);
        return new Route(normalizedRouteName, references, metadata, distanceThreshold);
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

    /// <summary>
    /// Deletes every reference belonging to a route, along with its per-route config. Returns the number of
    /// references removed.
    /// </summary>
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

        // Always drop the config key so a future route reusing this name cannot inherit a stale threshold
        // or metadata, even when the route currently has no references. The config delete is not counted in
        // the returned total, which reports references removed.
        var configKey = CreateConfigKey(normalizedRouteName);
        if (results.Documents.Count == 0)
        {
            await _database.KeyDeleteAsync(configKey).WaitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        var keys = results.Documents.Select(document => (RedisKey)document.Id).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var removed = await _database.KeyDeleteAsync(keys).WaitAsync(cancellationToken).ConfigureAwait(false);
        await _database.KeyDeleteAsync(configKey).WaitAsync(cancellationToken).ConfigureAwait(false);
        return removed;
    }

    /// <summary>
    /// Classifies an input and returns the single nearest reference whose route accepts it, or
    /// <see langword="null" /> for a miss. A reference is accepted only when its distance is within its
    /// route's effective threshold (the per-route threshold when set, otherwise the router default, capped
    /// by the router default) — resolved identically to
    /// <see cref="RouteManyAsync(string, float[], int?, DistanceAggregationMethod?, System.Threading.CancellationToken)" />
    /// so the two never disagree on whether a route is in range. Because a nearer reference may belong to a
    /// route whose stricter threshold rejects it, the returned reference is not necessarily the globally
    /// nearest one.
    /// </summary>
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
                limit: RoutingConfig.MaxReferenceCandidates),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (results.Documents.Count == 0)
        {
            return null;
        }

        // Candidates arrive nearest-first (SORTBY distance ASC). Resolve each candidate route's threshold
        // once, then walk in order and return the first reference its route accepts.
        var routeNames = results.Documents
            .Select(document => document.RouteName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var thresholds = await LoadRouteThresholdsAsync(routeNames, cancellationToken).ConfigureAwait(false);

        foreach (var match in results.Documents)
        {
            if (match.Distance <= thresholds[match.RouteName])
            {
                return new SemanticRouteMatch(normalizedInput, match.RouteName, match.Reference, match.Distance);
            }
        }

        return null;
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
                returnFields: [Options.RouteNameFieldName],
                scoreAlias: DistanceAlias,
                limit: RoutingConfig.MaxReferenceCandidates),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return await AggregateMatchesAsync(results, method, effectiveMaxResults, cancellationToken).ConfigureAwait(false);
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

    private async Task<IReadOnlyList<SemanticRouterMatch>> AggregateMatchesAsync(
        SearchResults results,
        DistanceAggregationMethod method,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var groups = new Dictionary<string, RouteAccumulator>(StringComparer.Ordinal);
        var routeOrder = new List<string>();
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
                accumulator = new RouteAccumulator();
                groups[routeName] = accumulator;
                routeOrder.Add(routeName);
            }

            accumulator.Add(distance);
        }

        if (groups.Count == 0)
        {
            return [];
        }

        var thresholds = await LoadRouteThresholdsAsync(routeOrder, cancellationToken).ConfigureAwait(false);

        var matches = new List<SemanticRouterMatch>(groups.Count);
        foreach (var (routeName, accumulator) in groups)
        {
            var aggregated = accumulator.Aggregate(method);
            if (aggregated <= thresholds[routeName])
            {
                matches.Add(new SemanticRouterMatch(routeName, aggregated));
            }
        }

        matches.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
        return matches.Count <= maxResults ? matches : matches.GetRange(0, maxResults);
    }

    /// <summary>
    /// Reads the effective distance threshold for each route from its config key. The effective threshold is
    /// the per-route threshold capped by the router default, or the router default when no per-route threshold
    /// is stored. Every read is pipelined so a route set costs roughly one round trip.
    /// </summary>
    private async Task<Dictionary<string, double>> LoadRouteThresholdsAsync(
        IReadOnlyList<string> routeNames,
        CancellationToken cancellationToken)
    {
        var thresholds = new Dictionary<string, double>(routeNames.Count, StringComparer.Ordinal);
        if (routeNames.Count == 0)
        {
            return thresholds;
        }

        var values = await RedisBatch.RunAsync(
            routeNames,
            (routeName, _, token) => _database
                .HashGetAsync(CreateConfigKey(routeName), RouteThresholdFieldName)
                .WaitAsync(token),
            cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < routeNames.Count; index++)
        {
            thresholds[routeNames[index]] = ResolveEffectiveThreshold(values[index]);
        }

        return thresholds;
    }

    private double ResolveEffectiveThreshold(RedisValue thresholdValue) =>
        !thresholdValue.IsNull &&
        double.TryParse(thresholdValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
            ? Math.Min(threshold, DistanceThreshold)
            : DistanceThreshold;

    private async Task<(double? DistanceThreshold, IReadOnlyDictionary<string, object?>? Metadata)> LoadRouteConfigAsync(
        string routeName,
        CancellationToken cancellationToken)
    {
        var values = await _database
            .HashGetAsync(CreateConfigKey(routeName), [RouteThresholdFieldName, MetadataFieldName])
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        double? distanceThreshold = null;
        if (!values[0].IsNull &&
            double.TryParse(values[0].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedThreshold))
        {
            distanceThreshold = parsedThreshold;
        }

        var metadata = values[1].IsNull ? null : DeserializeMetadata(values[1].ToString());
        return (distanceThreshold, metadata);
    }

    private async Task<string> WriteReferenceAsync(
        string routeName,
        string reference,
        float[] embedding,
        CancellationToken cancellationToken)
    {
        ValidateEmbedding(embedding);

        var key = CreateKey(routeName, reference);
        var entries = new HashEntry[]
        {
            new(Options.RouteNameFieldName, routeName),
            new(Options.ReferenceFieldName, reference),
            new(Options.EmbeddingFieldName, EmbeddingsCache.EncodeFloat32(embedding))
        };

        await _database.HashSetAsync(key, entries).WaitAsync(cancellationToken).ConfigureAwait(false);
        return key!;
    }

    private async Task WriteRouteConfigAsync(
        string routeName,
        double? distanceThreshold,
        string? metadata,
        CancellationToken cancellationToken)
    {
        var key = CreateConfigKey(routeName);

        var entries = new List<HashEntry>(2);
        if (distanceThreshold is double threshold)
        {
            entries.Add(new HashEntry(RouteThresholdFieldName, threshold.ToString("G", CultureInfo.InvariantCulture)));
        }

        if (metadata is not null)
        {
            entries.Add(new HashEntry(MetadataFieldName, metadata));
        }

        // A route with neither an explicit threshold nor metadata has nothing to persist: delete any config
        // left by a previous add so re-adding a route without a threshold does not silently keep the old one.
        if (entries.Count == 0)
        {
            await _database.KeyDeleteAsync(key).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // Replace the config wholesale (DEL + HSET) inside one MULTI/EXEC so a dropped threshold or metadata
        // never lingers and a concurrent reader never observes the key mid-rewrite. Config keys carry no TTL.
        var transaction = _database.CreateTransaction();
        _ = transaction.KeyDeleteAsync(key);
        _ = transaction.HashSetAsync(key, entries.ToArray());
        await transaction.ExecuteAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ValidateEmbedding(float[] embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        // RediSearch silently rejects (and never indexes) a hash whose vector length does not match
        // the field's declared dimensions, so validate on write rather than storing a reference that
        // can never match a route. Query-side vectors are validated by the search command builder.
        if (embedding.Length != Options.EmbeddingFieldAttributes.Dimensions)
        {
            throw new ArgumentException(
                $"Semantic router embedding must contain exactly {Options.EmbeddingFieldAttributes.Dimensions} values.",
                nameof(embedding));
        }
    }

    internal RedisKey CreateKey(string routeName, string reference)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{routeName}\n{reference}"))).ToLowerInvariant();
        return $"{CreateKeyPrefix(Options)}{hash}";
    }

    internal RedisKey CreateConfigKey(string routeName)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(routeName))).ToLowerInvariant();
        return $"{CreateKeyPrefix(Options)}{ConfigKeyInfix}:{hash}";
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

    private sealed class RouteAccumulator
    {
        private double _sum;
        private double _min = double.PositiveInfinity;
        private int _count;

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

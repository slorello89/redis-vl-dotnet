using System.Globalization;
using RedisVL.Queries;
using RedisVL.Schema;

namespace RedisVL.Indexes;

internal static class SearchQueryCommandBuilder
{
    public static object[] BuildTextSearchArguments(SearchSchema schema, TextQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var arguments = new List<object>
        {
            schema.Index.Name,
            query.QueryString
        };

        if (query.ReturnFields.Count > 0)
        {
            arguments.Add("RETURN");
            arguments.Add(query.ReturnFields.Count.ToString(CultureInfo.InvariantCulture));
            arguments.AddRange(query.ReturnFields);
        }

        AppendLimit(arguments, query.Offset, query.Limit);
        arguments.Add("DIALECT");
        arguments.Add("2");

        return arguments.ToArray();
    }

    public static object[] BuildFilterSearchArguments(SearchSchema schema, FilterQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var arguments = new List<object>
        {
            schema.Index.Name,
            query.Filter?.ToQueryString() ?? "*"
        };

        if (query.ReturnFields.Count > 0)
        {
            arguments.Add("RETURN");
            arguments.Add(query.ReturnFields.Count.ToString(CultureInfo.InvariantCulture));
            arguments.AddRange(query.ReturnFields);
        }

        if (query.SortBy is not null)
        {
            arguments.Add("SORTBY");
            arguments.Add(query.SortBy.Field);
            arguments.Add(query.SortBy.Descending ? "DESC" : "ASC");
        }

        AppendLimit(arguments, query.Offset, query.Limit);
        arguments.Add("DIALECT");
        arguments.Add("2");

        return arguments.ToArray();
    }

    public static object[] BuildCountArguments(SearchSchema schema, CountQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        return
        [
            schema.Index.Name,
            query.Filter?.ToQueryString() ?? "*",
            "NOCONTENT",
            "LIMIT",
            "0",
            "0",
            "DIALECT",
            "2"
        ];
    }

    public static object[] BuildAggregateArguments(SearchSchema schema, AggregationQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var arguments = new List<object>
        {
            schema.Index.Name,
            query.QueryString
        };

        AppendAggregationPipeline(
            arguments,
            schema,
            query.LoadFields,
            query.ApplyClauses,
            query.GroupBy,
            query.SortBy,
            query.Offset,
            query.Limit);

        arguments.Add("DIALECT");
        arguments.Add("2");

        return arguments.ToArray();
    }

    public static object[] BuildAggregateHybridArguments(SearchSchema schema, AggregateHybridQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var vectorField = ResolveVectorField(schema, query.VectorFieldName);
        ValidateVectorPayload(vectorField.Attributes, query.VectorFieldName, query.Vector);
        ValidateRuntimeParameters(vectorField, query.VectorFieldName, query.RuntimeOptions);

        var arguments = new List<object>
        {
            schema.Index.Name,
            BuildHybridAggregateQuery(schema, vectorField, query)
        };
        arguments.AddRange(BuildVectorParams(query.Vector, CollectKnnRuntimeParams(query.RuntimeOptions)));

        AppendAggregationPipeline(
            arguments,
            schema,
            query.LoadFields,
            query.ApplyClauses,
            query.GroupBy,
            query.SortBy,
            query.Offset,
            query.Limit);

        arguments.Add("DIALECT");
        arguments.Add("2");

        return arguments.ToArray();
    }

    private static void AppendAggregationPipeline(
        List<object> arguments,
        SearchSchema schema,
        IReadOnlyList<string> loadFields,
        IReadOnlyList<AggregationApply> applyClauses,
        AggregationGroupBy? groupBy,
        AggregationSortBy? sortBy,
        int offset,
        int limit)
    {
        if (loadFields.Count > 0)
        {
            arguments.Add("LOAD");
            arguments.Add(loadFields.Count.ToString(CultureInfo.InvariantCulture));
            arguments.AddRange(loadFields.Select(field => (object)FormatAggregationPropertyReference(schema, field)));
        }

        foreach (var apply in applyClauses)
        {
            arguments.Add("APPLY");
            arguments.Add(apply.Expression);
            arguments.Add("AS");
            arguments.Add(apply.Alias);
        }

        if (groupBy is not null)
        {
            arguments.Add("GROUPBY");
            arguments.Add(groupBy.Properties.Count.ToString(CultureInfo.InvariantCulture));
            arguments.AddRange(groupBy.Properties.Select(property => (object)FormatAggregationPropertyReference(schema, property)));

            foreach (var reducer in groupBy.Reducers)
            {
                arguments.Add("REDUCE");
                arguments.Add(reducer.FunctionName);
                arguments.Add(reducer.Arguments.Count.ToString(CultureInfo.InvariantCulture));
                arguments.AddRange(reducer.Arguments.Select(argument => (object)FormatReducerArgument(schema, argument)));
                arguments.Add("AS");
                arguments.Add(reducer.Alias);
            }
        }

        if (sortBy is not null)
        {
            arguments.Add("SORTBY");
            arguments.Add((sortBy.Fields.Count * 2).ToString(CultureInfo.InvariantCulture));

            foreach (var field in sortBy.Fields)
            {
                arguments.Add(FormatAggregationPropertyReference(schema, field.Property));
                arguments.Add(field.Descending ? "DESC" : "ASC");
            }
        }

        AppendLimit(arguments, offset, limit);
    }

    public static object[] BuildVectorSearchArguments(SearchSchema schema, VectorQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var vectorField = ResolveVectorField(schema, query.FieldName);
        ValidateVectorPayload(vectorField, query);
        ValidateRuntimeParameters(vectorField, query.FieldName, query.RuntimeOptions);

        var arguments = new List<object>
        {
            schema.Index.Name,
            BuildVectorSearchQuery(schema, vectorField, query)
        };
        arguments.AddRange(BuildVectorParams(query.Vector, CollectKnnRuntimeParams(query.RuntimeOptions)));
        arguments.AddRange(["SORTBY", query.ScoreAlias, "ASC"]);

        // An empty return set means "unspecified" — omit RETURN so the server returns all stored
        // fields plus the yielded score, rather than narrowing to just the score alias.
        if (query.ReturnFields.Count > 0)
        {
            arguments.Add("RETURN");
            arguments.Add(query.ReturnFields.Count.ToString(CultureInfo.InvariantCulture));
            arguments.AddRange(query.ReturnFields);
        }

        AppendLimit(arguments, query.Offset, query.Limit);
        arguments.Add("DIALECT");
        arguments.Add("2");

        return arguments.ToArray();
    }

    public static IReadOnlyList<object[]> BuildMultiVectorSearchArguments(SearchSchema schema, MultiVectorQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var arguments = new List<object[]>(query.Vectors.Count);
        for (var index = 0; index < query.Vectors.Count; index++)
        {
            var vector = query.Vectors[index];
            var vectorField = ResolveVectorField(schema, vector.FieldName);
            ValidateCosineDistanceMetric(vectorField, vector.FieldName);

            var subQuery = new VectorQuery(
                vector.FieldName,
                vector.Vector,
                query.TopK,
                query.Filter,
                query.ProjectedFields,
                GetMultiVectorScoreAlias(index),
                query.RuntimeOptions,
                new QueryPagination(limit: query.TopK));

            arguments.Add(BuildVectorSearchArguments(schema, subQuery));
        }

        return arguments;
    }

    public static object[] BuildHybridSearchArguments(SearchSchema schema, HybridQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var vectorField = ResolveVectorField(schema, query.VectorFieldName);
        ValidateVectorPayload(vectorField.Attributes, query.VectorFieldName, query.Vector);
        ValidateRuntimeParameters(vectorField, query.VectorFieldName, query.RuntimeOptions);

        var arguments = new List<object>
        {
            schema.Index.Name,
            BuildHybridSearchQuery(schema, vectorField, query)
        };
        arguments.AddRange(BuildVectorParams(query.Vector, CollectKnnRuntimeParams(query.RuntimeOptions)));
        arguments.AddRange(
        [
            "SORTBY",
            query.ScoreAlias,
            "ASC",
            "RETURN",
            query.ReturnFields.Count.ToString(CultureInfo.InvariantCulture)
        ]);

        arguments.AddRange(query.ReturnFields);
        AppendLimit(arguments, query.Offset, query.Limit);
        arguments.Add("DIALECT");
        arguments.Add("2");

        return arguments.ToArray();
    }

    public static object[] BuildNativeHybridArguments(SearchSchema schema, HybridSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var vectorField = ResolveVectorField(schema, query.VectorFieldName);
        ValidateVectorPayload(vectorField.Attributes, query.VectorFieldName, query.Vector);
        ValidateRuntimeParameters(vectorField, query.VectorFieldName, query.RuntimeOptions);

        var arguments = new List<object>
        {
            schema.Index.Name,
            "SEARCH",
            query.TextQuery.ToQueryString(),
            "VSIM",
            $"@{GetQueryFieldName(schema, vectorField)}",
            "$vector"
        };

        AppendHybridKnnClause(arguments, query);

        if (query.VectorFilter is not null)
        {
            arguments.Add("FILTER");
            arguments.Add(query.VectorFilter.ToQueryString());
        }

        query.Combination?.AppendTo(arguments);

        AppendLimit(arguments, query.Offset, query.Limit);
        AppendHybridLoadClause(arguments, query);

        arguments.Add("PARAMS");
        arguments.Add("2");
        arguments.Add("vector");
        arguments.Add(query.Vector);

        return arguments.ToArray();
    }

    private static void AppendHybridKnnClause(List<object> arguments, HybridSearchQuery query)
    {
        var runtimeParams = CollectKnnRuntimeParams(query.RuntimeOptions);
        arguments.Add("KNN");
        arguments.Add((2 + runtimeParams.Count * 2).ToString(CultureInfo.InvariantCulture));
        arguments.Add("K");
        arguments.Add(query.TopK.ToString(CultureInfo.InvariantCulture));
        foreach (var (_, keyword, value) in runtimeParams)
        {
            arguments.Add(keyword);
            arguments.Add(value);
        }
    }

    private static void AppendHybridLoadClause(List<object> arguments, HybridSearchQuery query)
    {
        var fields = new List<object>
        {
            $"@{HybridSearchQuery.KeyField}",
            $"@{HybridSearchQuery.ScoreField}"
        };
        fields.AddRange(query.ReturnFields.Select(static field => (object)$"@{field}"));

        arguments.Add("LOAD");
        arguments.Add(fields.Count.ToString(CultureInfo.InvariantCulture));
        arguments.AddRange(fields);
    }

    public static object[] BuildVectorRangeArguments(SearchSchema schema, VectorRangeQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var vectorField = ResolveVectorField(schema, query.FieldName);
        ValidateVectorPayload(vectorField.Attributes, query.FieldName, query.Vector);
        ValidateRuntimeParameters(vectorField, query.FieldName, query.RuntimeOptions);

        var arguments = new List<object>
        {
            schema.Index.Name,
            BuildVectorRangeSearchQuery(schema, vectorField, query)
        };
        arguments.AddRange(BuildVectorParams(query.Vector, ("epsilon", query.RuntimeOptions?.Epsilon)));
        arguments.AddRange(
        [
            "SORTBY",
            query.ScoreAlias,
            "ASC",
            "RETURN",
            query.ReturnFields.Count.ToString(CultureInfo.InvariantCulture)
        ]);

        arguments.AddRange(query.ReturnFields);
        AppendLimit(arguments, query.Offset, query.Limit);
        arguments.Add("DIALECT");
        arguments.Add("2");

        return arguments.ToArray();
    }

    private static string BuildVectorSearchQuery(SearchSchema schema, VectorFieldDefinition field, VectorQuery query)
    {
        // The filter must be parenthesized so a compound filter (`&`/`|`/`!`) binds as the
        // pre-filter for the KNN clause; an unparenthesized compound filter makes the server
        // reject the query with a syntax error near `=>[`. This matches the hybrid builders.
        var filter = query.Filter?.ToQueryString() ?? "*";
        var runtimeClause = BuildKnnRuntimeClause(query.RuntimeOptions);
        return $"({filter})=>[KNN {query.TopK.ToString(CultureInfo.InvariantCulture)} @{GetQueryFieldName(schema, field)} $vector{runtimeClause} AS {query.ScoreAlias}]";
    }

    private static string BuildHybridSearchQuery(SearchSchema schema, VectorFieldDefinition field, HybridQuery query)
    {
        var filter = query.CombinedFilter.ToQueryString();
        var runtimeClause = BuildKnnRuntimeClause(query.RuntimeOptions);
        return $"({filter})=>[KNN {query.TopK.ToString(CultureInfo.InvariantCulture)} @{GetQueryFieldName(schema, field)} $vector{runtimeClause} AS {query.ScoreAlias}]";
    }

    private static string BuildHybridAggregateQuery(SearchSchema schema, VectorFieldDefinition field, AggregateHybridQuery query)
    {
        var filter = query.CombinedFilter.ToQueryString();
        var runtimeClause = BuildKnnRuntimeClause(query.RuntimeOptions);
        return $"({filter})=>[KNN {query.TopK.ToString(CultureInfo.InvariantCulture)} @{GetQueryFieldName(schema, field)} $vector{runtimeClause} AS {query.ScoreAlias}]";
    }

    private static string BuildVectorRangeSearchQuery(SearchSchema schema, VectorFieldDefinition field, VectorRangeQuery query)
    {
        var runtimeClause = query.RuntimeOptions?.Epsilon is double ? "; $EPSILON: $epsilon" : string.Empty;
        var vectorClause =
            $"@{GetQueryFieldName(schema, field)}:[VECTOR_RANGE {query.DistanceThreshold.ToString("G", CultureInfo.InvariantCulture)} $vector]=>{{$YIELD_DISTANCE_AS: {query.ScoreAlias}{runtimeClause}}}";
        var filter = query.Filter?.ToQueryString();

        return string.IsNullOrWhiteSpace(filter)
            ? vectorClause
            : $"({filter}) {vectorClause}";
    }

    private static VectorFieldDefinition ResolveVectorField(SearchSchema schema, string fieldName)
    {
        foreach (var field in schema.Fields)
        {
            if (!MatchesQueryField(schema, field, fieldName))
            {
                continue;
            }

            return field as VectorFieldDefinition
                ?? throw new InvalidOperationException($"Field '{fieldName}' is not configured as a vector field.");
        }

        throw new InvalidOperationException($"Vector field '{fieldName}' was not found in schema '{schema.Index.Name}'.");
    }

    private static bool MatchesQueryField(SearchSchema schema, FieldDefinition field, string fieldName)
    {
        if (string.Equals(field.Name, fieldName, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(field.Alias) &&
            string.Equals(field.Alias, fieldName, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(GetQueryFieldName(schema, field), fieldName, StringComparison.Ordinal);
    }

    private static string GetQueryFieldName(SearchSchema schema, FieldDefinition field)
    {
        if (schema.Index.StorageType == StorageType.Json)
        {
            if (!string.IsNullOrWhiteSpace(field.Alias))
            {
                return field.Alias!;
            }

            return field.Name.StartsWith("$", StringComparison.Ordinal)
                ? field.Name.TrimStart('$').TrimStart('.')
                : field.Name;
        }

        return field.Alias ?? field.Name;
    }

    private static string FormatReducerArgument(SearchSchema schema, AggregationReducerArgument argument) =>
        argument.IsPropertyReference
            ? FormatAggregationPropertyReference(schema, argument.Value)
            : argument.Value;

    private static string FormatAggregationPropertyReference(SearchSchema schema, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        var trimmed = property.Trim();
        var normalized = trimmed.TrimStart('@');

        foreach (var field in schema.Fields)
        {
            if (MatchesQueryField(schema, field, normalized) || MatchesQueryField(schema, field, trimmed))
            {
                return $"@{GetQueryFieldName(schema, field)}";
            }
        }

        return $"@{normalized}";
    }

    private static void ValidateVectorPayload(VectorFieldDefinition field, VectorQuery query)
    {
        ValidateVectorPayload(field.Attributes, query.FieldName, query.Vector);
    }

    private static void ValidateRuntimeParameters(
        VectorFieldDefinition field,
        string fieldName,
        VectorKnnRuntimeOptions? runtimeOptions)
    {
        if (runtimeOptions is null)
        {
            return;
        }

        var algorithm = field.Attributes.Algorithm;

        if (runtimeOptions.EfRuntime is not null && algorithm != VectorAlgorithm.Hnsw)
        {
            throw new InvalidOperationException(
                $"Field '{fieldName}' uses '{algorithm}' and does not support runtime parameter 'EF_RUNTIME'.");
        }

        if ((runtimeOptions.SearchWindowSize is not null
                || runtimeOptions.UseSearchHistory is not null
                || runtimeOptions.SearchBufferCapacity is not null)
            && algorithm != VectorAlgorithm.SvsVamana)
        {
            throw new InvalidOperationException(
                $"Field '{fieldName}' uses '{algorithm}' and does not support SVS-VAMANA runtime parameters such as 'SEARCH_WINDOW_SIZE'.");
        }
    }

    private static void ValidateRuntimeParameters(
        VectorFieldDefinition field,
        string fieldName,
        VectorRangeRuntimeOptions? runtimeOptions)
    {
        if (runtimeOptions?.Epsilon is null)
        {
            return;
        }

        if (field.Attributes.Algorithm != VectorAlgorithm.Hnsw && field.Attributes.Algorithm != VectorAlgorithm.SvsVamana)
        {
            throw new InvalidOperationException(
                $"Field '{fieldName}' uses '{field.Attributes.Algorithm}' and does not support runtime parameter 'EPSILON'.");
        }
    }

    private static void ValidateCosineDistanceMetric(VectorFieldDefinition field, string fieldName)
    {
        if (field.Attributes.DistanceMetric != VectorDistanceMetric.Cosine)
        {
            throw new InvalidOperationException(
                $"Multi-vector queries require cosine distance fields. Field '{fieldName}' uses '{field.Attributes.DistanceMetric}'.");
        }
    }

    private static void ValidateVectorPayload(VectorFieldAttributes attributes, string fieldName, byte[] vector)
    {
        var bytesPerDimension = attributes.DataType switch
        {
            VectorDataType.Float32 => sizeof(float),
            VectorDataType.Float64 => sizeof(double),
            VectorDataType.Float16 => 2,
            VectorDataType.BFloat16 => 2,
            VectorDataType.UInt8 => 1,
            VectorDataType.Int8 => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(attributes), attributes.DataType, "Unsupported vector data type.")
        };

        var expectedLength = attributes.Dimensions * bytesPerDimension;
        if (vector.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Vector payload for field '{fieldName}' must contain exactly {expectedLength} bytes.",
                nameof(vector));
        }
    }

    private static List<(string ParamName, string Keyword, string Value)> CollectKnnRuntimeParams(VectorKnnRuntimeOptions? options)
    {
        var collected = new List<(string ParamName, string Keyword, string Value)>();
        if (options is null)
        {
            return collected;
        }

        if (options.EfRuntime is int efRuntime)
        {
            collected.Add(("ef_runtime", "EF_RUNTIME", efRuntime.ToString(CultureInfo.InvariantCulture)));
        }

        if (options.SearchWindowSize is int searchWindowSize)
        {
            collected.Add(("search_window_size", "SEARCH_WINDOW_SIZE", searchWindowSize.ToString(CultureInfo.InvariantCulture)));
        }

        if (options.UseSearchHistory is SvsSearchHistory useSearchHistory)
        {
            collected.Add(("use_search_history", "USE_SEARCH_HISTORY", ToRedisKeyword(useSearchHistory)));
        }

        if (options.SearchBufferCapacity is int searchBufferCapacity)
        {
            collected.Add(("search_buffer_capacity", "SEARCH_BUFFER_CAPACITY", searchBufferCapacity.ToString(CultureInfo.InvariantCulture)));
        }

        return collected;
    }

    private static string BuildKnnRuntimeClause(VectorKnnRuntimeOptions? options) =>
        string.Concat(CollectKnnRuntimeParams(options).Select(static parameter => $" {parameter.Keyword} ${parameter.ParamName}"));

    private static string ToRedisKeyword(SvsSearchHistory value) =>
        value switch
        {
            SvsSearchHistory.Auto => "AUTO",
            SvsSearchHistory.On => "ON",
            SvsSearchHistory.Off => "OFF",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported search history value.")
        };

    private static object[] BuildVectorParams(byte[] vector, IReadOnlyList<(string ParamName, string Keyword, string Value)> runtimeParams) =>
        BuildVectorParams(vector, runtimeParams.Select(static parameter => (parameter.ParamName, (object?)parameter.Value)).ToArray());

    private static object[] BuildVectorParams(byte[] vector, params (string Name, object? Value)[] additionalParameters)
    {
        var parameters = new List<object> { "vector", vector };
        foreach (var (name, value) in additionalParameters)
        {
            if (value is null)
            {
                continue;
            }

            parameters.Add(name);
            parameters.Add(value switch
            {
                double number => number.ToString("G", CultureInfo.InvariantCulture),
                float number => number.ToString("G", CultureInfo.InvariantCulture),
                _ => value.ToString()!
            });
        }

        return
        [
            "PARAMS",
            parameters.Count.ToString(CultureInfo.InvariantCulture),
            .. parameters
        ];
    }

    internal static string GetMultiVectorScoreAlias(int index) =>
        $"__mv_score_{index.ToString(CultureInfo.InvariantCulture)}";

    private static void AppendLimit(List<object> arguments, int offset, int limit)
    {
        arguments.Add("LIMIT");
        arguments.Add(offset.ToString(CultureInfo.InvariantCulture));
        arguments.Add(limit.ToString(CultureInfo.InvariantCulture));
    }
}

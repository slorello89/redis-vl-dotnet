using System.Globalization;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;

namespace RedisVlDotNet.Indexes;

internal static class SearchQueryCommandBuilder
{
    public static object[] BuildTextSearchArguments(SearchSchema schema, TextQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var arguments = new List<object>
        {
            schema.Index.Name,
            query.Text
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
        arguments.AddRange(BuildVectorParams(query.Vector, ("ef_runtime", query.RuntimeOptions?.EfRuntime)));

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
        arguments.AddRange(BuildVectorParams(query.Vector, ("ef_runtime", query.RuntimeOptions?.EfRuntime)));
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
                query.Pagination);

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
        arguments.AddRange(BuildVectorParams(query.Vector, ("ef_runtime", query.RuntimeOptions?.EfRuntime)));
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
        var filter = query.Filter?.ToQueryString() ?? "*";
        var runtimeClause = query.RuntimeOptions?.EfRuntime is int ? " EF_RUNTIME $ef_runtime" : string.Empty;
        return $"{filter}=>[KNN {query.TopK.ToString(CultureInfo.InvariantCulture)} @{GetQueryFieldName(schema, field)} $vector{runtimeClause} AS {query.ScoreAlias}]";
    }

    private static string BuildHybridSearchQuery(SearchSchema schema, VectorFieldDefinition field, HybridQuery query)
    {
        var filter = query.CombinedFilter.ToQueryString();
        var runtimeClause = query.RuntimeOptions?.EfRuntime is int ? " EF_RUNTIME $ef_runtime" : string.Empty;
        return $"({filter})=>[KNN {query.TopK.ToString(CultureInfo.InvariantCulture)} @{GetQueryFieldName(schema, field)} $vector{runtimeClause} AS {query.ScoreAlias}]";
    }

    private static string BuildHybridAggregateQuery(SearchSchema schema, VectorFieldDefinition field, AggregateHybridQuery query)
    {
        var filter = query.CombinedFilter.ToQueryString();
        var runtimeClause = query.RuntimeOptions?.EfRuntime is int ? " EF_RUNTIME $ef_runtime" : string.Empty;
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
            : $"{filter} {vectorClause}";
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
        if (runtimeOptions?.EfRuntime is null)
        {
            return;
        }

        if (field.Attributes.Algorithm != VectorAlgorithm.Hnsw)
        {
            throw new InvalidOperationException(
                $"Field '{fieldName}' uses '{field.Attributes.Algorithm}' and does not support runtime parameter 'EF_RUNTIME'.");
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

        if (field.Attributes.Algorithm != VectorAlgorithm.Hnsw)
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

using System.Globalization;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;

namespace RedisVlDotNet.Indexes;

internal static class SearchQueryCommandBuilder
{
    public static object[] BuildVectorSearchArguments(SearchSchema schema, VectorQuery query)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(query);

        var vectorField = ResolveVectorField(schema, query.FieldName);
        ValidateVectorPayload(vectorField, query);

        var arguments = new List<object>
        {
            schema.Index.Name,
            BuildVectorSearchQuery(schema, vectorField, query),
            "PARAMS",
            "2",
            "vector",
            query.Vector,
            "SORTBY",
            query.ScoreAlias,
            "ASC",
            "RETURN",
            query.ReturnFields.Count.ToString(CultureInfo.InvariantCulture)
        };

        arguments.AddRange(query.ReturnFields);
        arguments.Add("LIMIT");
        arguments.Add("0");
        arguments.Add(query.TopK.ToString(CultureInfo.InvariantCulture));
        arguments.Add("DIALECT");
        arguments.Add("2");

        return arguments.ToArray();
    }

    private static string BuildVectorSearchQuery(SearchSchema schema, VectorFieldDefinition field, VectorQuery query)
    {
        var filter = query.Filter?.ToQueryString() ?? "*";
        return $"{filter}=>[KNN {query.TopK.ToString(CultureInfo.InvariantCulture)} @{GetQueryFieldName(schema, field)} $vector AS {query.ScoreAlias}]";
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

    private static void ValidateVectorPayload(VectorFieldDefinition field, VectorQuery query)
    {
        var bytesPerDimension = field.Attributes.DataType switch
        {
            VectorDataType.Float32 => sizeof(float),
            VectorDataType.Float64 => sizeof(double),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field.Attributes.DataType, "Unsupported vector data type.")
        };

        var expectedLength = field.Attributes.Dimensions * bytesPerDimension;
        if (query.Vector.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Vector payload for field '{query.FieldName}' must contain exactly {expectedLength} bytes.",
                nameof(query));
        }
    }
}

using Microsoft.Extensions.VectorData;
using RedisVL.Connectors.VectorData.Mapping;
using RedisVL.Schema;

namespace RedisVL.Tests.Connectors.VectorData;

public sealed class RedisVLRecordModelTests
{
    [Fact]
    public void Build_MapsKeyDataAndVectorProperties()
    {
        var model = RedisVLRecordModel.Build(typeof(ConnectorMovie), definition: null);

        Assert.Equal("id", model.Key.JsonName);
        Assert.Equal(RedisVLFieldKind.Key, model.Key.Kind);

        Assert.Equal(RedisVLFieldKind.Text, model.ByClrName["Title"].Kind);
        Assert.Equal(RedisVLFieldKind.Tag, model.ByClrName["Genre"].Kind);
        Assert.Equal(RedisVLFieldKind.Numeric, model.ByClrName["Year"].Kind);
        Assert.Equal(RedisVLFieldKind.Unindexed, model.ByClrName["Summary"].Kind);

        var vector = Assert.Single(model.Vectors);
        Assert.Equal("embedding", vector.JsonName);
        Assert.Equal(4, vector.Dimensions);
        Assert.Equal(VectorDistanceMetric.Cosine, vector.Metric);
        Assert.Equal(VectorAlgorithm.Hnsw, vector.Algorithm);
        Assert.Equal(VectorDataType.Float32, vector.DataType);
    }

    [Fact]
    public void BuildSchema_ProducesJsonIndexWithExpectedFields()
    {
        var model = RedisVLRecordModel.Build(typeof(ConnectorMovie), definition: null);

        var schema = model.BuildSchema("movies-idx", "movies:");

        Assert.Equal("movies-idx", schema.Index.Name);
        Assert.Equal(StorageType.Json, schema.Index.StorageType);
        Assert.Contains("movies:", schema.Index.Prefixes);

        // Unindexed data properties are stored in JSON but not added to the search schema.
        Assert.DoesNotContain(schema.Fields, f => f.Name == "summary");
        Assert.Contains(schema.Fields, f => f is TagFieldDefinition && f.Name == "genre");
        Assert.Contains(schema.Fields, f => f is TextFieldDefinition && f.Name == "title");
        Assert.Contains(schema.Fields, f => f is NumericFieldDefinition && f.Name == "year");
        Assert.Contains(schema.Fields, f => f is VectorFieldDefinition && f.Name == "embedding");
    }

    [Fact]
    public void Build_NonStringKey_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            RedisVLRecordModel.Build(typeof(IntKeyedRecord), definition: null));
    }

    [Fact]
    public void Build_MissingKey_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RedisVLRecordModel.Build(typeof(KeylessRecord), definition: null));
    }

    private sealed class IntKeyedRecord
    {
        [VectorStoreKey]
        public int Id { get; set; }
    }

    private sealed class KeylessRecord
    {
        [VectorStoreData(IsIndexed = true)]
        public string Genre { get; set; } = string.Empty;
    }
}

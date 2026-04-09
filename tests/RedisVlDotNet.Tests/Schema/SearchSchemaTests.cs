using RedisVlDotNet.Schema;

namespace RedisVlDotNet.Tests.Schema;

public sealed class SearchSchemaTests
{
    [Fact]
    public void CreatesSchemaWithTypedIndexMetadata()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "docs:", StorageType.Json),
            Array.Empty<FieldDefinition>());

        Assert.Equal("docs-idx", schema.Index.Name);
        Assert.Equal("docs:", schema.Index.Prefix);
        Assert.Equal(StorageType.Json, schema.Index.StorageType);
        Assert.Empty(schema.Fields);
    }

    [Fact]
    public void PreservesConfiguredFieldDefinitions()
    {
        var vectorAttributes = new VectorFieldAttributes(
            VectorAlgorithm.Hnsw,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            1536,
            InitialCapacity: 1000,
            M: 16,
            EfConstruction: 200,
            EfRuntime: 10);

        FieldDefinition[] fields =
        [
            new TextFieldDefinition("title", alias: "title_text", sortable: true, noStem: true),
            new TagFieldDefinition("genre", separator: ';', caseSensitive: true),
            new NumericFieldDefinition("rating", sortable: true),
            new GeoFieldDefinition("location"),
            new VectorFieldDefinition("embedding", vectorAttributes, alias: "embedding_vector")
        ];

        var schema = new SearchSchema(
            new IndexDefinition("media-idx", "media:", StorageType.Hash),
            fields);

        Assert.Collection(
            schema.Fields,
            field =>
            {
                var textField = Assert.IsType<TextFieldDefinition>(field);
                Assert.Equal("title", textField.Name);
                Assert.Equal("title_text", textField.Alias);
                Assert.True(textField.Sortable);
                Assert.True(textField.NoStem);
                Assert.False(textField.PhoneticMatch);
            },
            field =>
            {
                var tagField = Assert.IsType<TagFieldDefinition>(field);
                Assert.Equal("genre", tagField.Name);
                Assert.Equal(';', tagField.Separator);
                Assert.True(tagField.CaseSensitive);
            },
            field =>
            {
                var numericField = Assert.IsType<NumericFieldDefinition>(field);
                Assert.Equal("rating", numericField.Name);
                Assert.True(numericField.Sortable);
            },
            field =>
            {
                var geoField = Assert.IsType<GeoFieldDefinition>(field);
                Assert.Equal("location", geoField.Name);
            },
            field =>
            {
                var vectorField = Assert.IsType<VectorFieldDefinition>(field);
                Assert.Equal("embedding", vectorField.Name);
                Assert.Equal("embedding_vector", vectorField.Alias);
                Assert.Equal(VectorAlgorithm.Hnsw, vectorField.Attributes.Algorithm);
                Assert.Equal(VectorDataType.Float32, vectorField.Attributes.DataType);
                Assert.Equal(VectorDistanceMetric.Cosine, vectorField.Attributes.DistanceMetric);
                Assert.Equal(1536, vectorField.Attributes.Dimensions);
                Assert.Equal(1000, vectorField.Attributes.InitialCapacity);
                Assert.Equal(16, vectorField.Attributes.M);
                Assert.Equal(200, vectorField.Attributes.EfConstruction);
                Assert.Equal(10, vectorField.Attributes.EfRuntime);
            });
    }

    [Fact]
    public void RejectsBlankIndexAndFieldNames()
    {
        Assert.Throws<ArgumentException>(() => new IndexDefinition("", "docs:", StorageType.Json));
        Assert.Throws<ArgumentException>(() => new TextFieldDefinition(" "));
    }
}

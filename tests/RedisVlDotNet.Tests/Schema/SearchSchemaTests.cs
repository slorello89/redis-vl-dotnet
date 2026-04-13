using RedisVlDotNet.Schema;

namespace RedisVlDotNet.Tests.Schema;

public sealed class SearchSchemaTests
{
    [Fact]
    public void LoadsSchemaFromYaml()
    {
        const string yaml = """
            index:
              name: docs-idx
              prefix: "docs:"
              storage_type: json
            fields:
              - name: title
                type: text
                alias: title_text
                sortable: true
                no_stem: true
              - name: genre
                type: tag
                separator: ;
                case_sensitive: true
              - name: rating
                type: numeric
                sortable: true
              - name: location
                type: geo
              - name: embedding
                type: vector
                alias: embedding_vector
                attrs:
                  algorithm: hnsw
                  datatype: float32
                  dims: 1536
                  distance_metric: cosine
                  initial_capacity: 1000
                  m: 16
                  ef_construction: 200
                  ef_runtime: 10
            """;

        var schema = SearchSchema.FromYaml(yaml);

        Assert.Equal("docs-idx", schema.Index.Name);
        Assert.Equal("docs:", schema.Index.Prefix);
        Assert.Equal(StorageType.Json, schema.Index.StorageType);

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
    public void LoadsSchemaFromYamlFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.schema.yaml");

        try
        {
            File.WriteAllText(path, """
                index:
                  name: media-idx
                  prefix: "media:"
                  storage_type: hash
                fields:
                  - name: title
                    type: text
                """);

            var schema = SearchSchema.FromYamlFile(path);

            Assert.Equal("media-idx", schema.Index.Name);
            Assert.Equal(StorageType.Hash, schema.Index.StorageType);
            Assert.Single(schema.Fields);
            Assert.IsType<TextFieldDefinition>(schema.Fields[0]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void RejectsYamlWithoutFields()
    {
        const string yaml = """
            index:
              name: docs-idx
              prefix: "docs:"
              storage_type: json
            """;

        var exception = Assert.Throws<ArgumentException>(() => SearchSchema.FromYaml(yaml));

        Assert.Equal("Fields", exception.ParamName);
    }

    [Fact]
    public void RejectsYamlWithInvalidVectorValidation()
    {
        const string yaml = """
            index:
              name: docs-idx
              prefix: "docs:"
              storage_type: json
            fields:
              - name: embedding
                type: vector
                attrs:
                  algorithm: flat
                  datatype: float32
                  dims: 128
                  distance_metric: cosine
                  m: 16
            """;

        var exception = Assert.Throws<ArgumentException>(() => SearchSchema.FromYaml(yaml));

        Assert.Equal("algorithm", exception.ParamName);
        Assert.Contains("FLAT", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsYamlWithUnsupportedFieldType()
    {
        const string yaml = """
            index:
              name: docs-idx
              prefix: "docs:"
              storage_type: json
            fields:
              - name: title
                type: unsupported
            """;

        var exception = Assert.Throws<ArgumentException>(() => SearchSchema.FromYaml(yaml));

        Assert.Equal("Type", exception.ParamName);
        Assert.Contains("unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatesSchemaWithTypedIndexMetadata()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "docs:", StorageType.Json),
            Array.Empty<FieldDefinition>());

        Assert.Equal("docs-idx", schema.Index.Name);
        Assert.Equal("docs:", schema.Index.Prefix);
        Assert.Equal(["docs:"], schema.Index.Prefixes);
        Assert.Equal(StorageType.Json, schema.Index.StorageType);
        Assert.Empty(schema.Fields);
    }

    [Fact]
    public void CreatesSchemaWithMultiplePrefixes()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", ["docs:", "archive:"], StorageType.Json),
            Array.Empty<FieldDefinition>());

        Assert.Equal("docs-idx", schema.Index.Name);
        Assert.Equal("docs:", schema.Index.Prefix);
        Assert.Equal(["docs:", "archive:"], schema.Index.Prefixes);
        Assert.Equal(StorageType.Json, schema.Index.StorageType);
    }

    [Fact]
    public void PreservesConfiguredFieldDefinitions()
    {
        var vectorAttributes = new VectorFieldAttributes(
            VectorAlgorithm.Hnsw,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            1536,
            initialCapacity: 1000,
            m: 16,
            efConstruction: 200,
            efRuntime: 10);

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
        Assert.Throws<ArgumentException>(() => new IndexDefinition("docs-idx", Array.Empty<string>(), StorageType.Json));
        Assert.Throws<ArgumentException>(() => new IndexDefinition("docs-idx", ["docs:", " "], StorageType.Json));
        Assert.Throws<ArgumentException>(() => new TextFieldDefinition(" "));
    }

    [Fact]
    public void CreatesVectorFieldAttributesForSupportedConfigurations()
    {
        var flatAttributes = new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.L2,
            768,
            initialCapacity: 100,
            blockSize: 64);

        var hnswAttributes = new VectorFieldAttributes(
            VectorAlgorithm.Hnsw,
            VectorDataType.Float64,
            VectorDistanceMetric.InnerProduct,
            384,
            m: 16,
            efConstruction: 200,
            efRuntime: 32);

        Assert.Equal(VectorAlgorithm.Flat, flatAttributes.Algorithm);
        Assert.Equal(64, flatAttributes.BlockSize);
        Assert.Equal(VectorAlgorithm.Hnsw, hnswAttributes.Algorithm);
        Assert.Equal(16, hnswAttributes.M);
        Assert.Equal(200, hnswAttributes.EfConstruction);
        Assert.Equal(32, hnswAttributes.EfRuntime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveVectorDimensions(int dimensions)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new VectorFieldAttributes(
            VectorAlgorithm.Hnsw,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            dimensions));

        Assert.Equal("dimensions", exception.ParamName);
    }

    [Theory]
    [InlineData((VectorAlgorithm)99, VectorDataType.Float32, VectorDistanceMetric.Cosine, "algorithm")]
    [InlineData(VectorAlgorithm.Flat, (VectorDataType)99, VectorDistanceMetric.Cosine, "dataType")]
    [InlineData(VectorAlgorithm.Flat, VectorDataType.Float32, (VectorDistanceMetric)99, "distanceMetric")]
    public void RejectsUnsupportedVectorEnumValues(
        VectorAlgorithm algorithm,
        VectorDataType dataType,
        VectorDistanceMetric distanceMetric,
        string expectedParamName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new VectorFieldAttributes(
            algorithm,
            dataType,
            distanceMetric,
            128));

        Assert.Equal(expectedParamName, exception.ParamName);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, "initialCapacity")]
    [InlineData(0, -1, 0, 0, "blockSize")]
    [InlineData(0, 0, -1, 0, "m")]
    [InlineData(0, 0, 0, -1, "efConstruction")]
    public void RejectsNegativeVectorTuningValues(
        int initialCapacity,
        int blockSize,
        int m,
        int efConstruction,
        string expectedParamName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new VectorFieldAttributes(
            VectorAlgorithm.Hnsw,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            256,
            initialCapacity: initialCapacity,
            blockSize: blockSize,
            m: m,
            efConstruction: efConstruction));

        Assert.Equal(expectedParamName, exception.ParamName);
    }

    [Fact]
    public void RejectsNegativeEfRuntime()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new VectorFieldAttributes(
            VectorAlgorithm.Hnsw,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            256,
            efRuntime: -1));

        Assert.Equal("efRuntime", exception.ParamName);
    }

    [Fact]
    public void RejectsHnswOptionsForFlatVectorFields()
    {
        var exception = Assert.Throws<ArgumentException>(() => new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            256,
            m: 16,
            efConstruction: 200,
            efRuntime: 32));

        Assert.Equal("algorithm", exception.ParamName);
        Assert.Contains("FLAT", exception.Message, StringComparison.Ordinal);
    }
}

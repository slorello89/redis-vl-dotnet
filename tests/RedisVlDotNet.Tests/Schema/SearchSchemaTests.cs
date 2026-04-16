using RedisVl.Schema;

namespace RedisVl.Tests.Schema;

public sealed class SearchSchemaTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Schema", "Fixtures");

    [Fact]
    public void LoadsSchemaFromYaml()
    {
        const string yaml = """
            index:
              name: docs-idx
              prefix: "docs:"
              key_separator: "|"
              storage_type: json
              stopwords:
                - the
                - a
                - an
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
        Assert.Equal('|', schema.Index.KeySeparator);
        Assert.Equal(StorageType.Json, schema.Index.StorageType);
        Assert.Equal(["the", "a", "an"], schema.Index.Stopwords);

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
        var schema = SearchSchema.FromYamlFile(Path.Combine(FixtureDirectory, "basic-schema.yaml"));

        Assert.Equal("media-idx", schema.Index.Name);
        Assert.Equal(StorageType.Hash, schema.Index.StorageType);
        Assert.Single(schema.Fields);
        Assert.IsType<TextFieldDefinition>(schema.Fields[0]);
    }

    [Fact]
    public void LoadsAdvancedSchemaFromYamlFile()
    {
        var schema = SearchSchema.FromYamlFile(Path.Combine(FixtureDirectory, "advanced-schema.yaml"));

        Assert.Equal("advanced-docs-idx", schema.Index.Name);
        Assert.Equal("docs:", schema.Index.Prefix);
        Assert.Equal(["docs:", "archive:"], schema.Index.Prefixes);
        Assert.Equal('|', schema.Index.KeySeparator);
        Assert.Equal(StorageType.Json, schema.Index.StorageType);
        Assert.Empty(schema.Index.Stopwords!);
        Assert.True(schema.Index.MaxTextFields);
        Assert.Equal(900, schema.Index.TemporarySeconds);
        Assert.True(schema.Index.NoOffsets);
        Assert.True(schema.Index.NoHighlight);
        Assert.True(schema.Index.NoFields);
        Assert.True(schema.Index.NoFrequencies);
        Assert.True(schema.Index.SkipInitialScan);

        Assert.Collection(
            schema.Fields,
            field =>
            {
                var textField = Assert.IsType<TextFieldDefinition>(field);
                Assert.Equal(2.5, textField.Weight);
                Assert.True(textField.WithSuffixTrie);
                Assert.True(textField.IndexMissing);
                Assert.True(textField.IndexEmpty);
                Assert.True(textField.UnNormalizedForm);
            },
            field =>
            {
                var tagField = Assert.IsType<TagFieldDefinition>(field);
                Assert.Equal(';', tagField.Separator);
                Assert.True(tagField.CaseSensitive);
                Assert.True(tagField.WithSuffixTrie);
                Assert.True(tagField.IndexMissing);
                Assert.True(tagField.IndexEmpty);
                Assert.True(tagField.NoIndex);
            },
            field =>
            {
                var numericField = Assert.IsType<NumericFieldDefinition>(field);
                Assert.True(numericField.IndexMissing);
                Assert.True(numericField.NoIndex);
                Assert.True(numericField.UnNormalizedForm);
            },
            field =>
            {
                var geoField = Assert.IsType<GeoFieldDefinition>(field);
                Assert.True(geoField.IndexMissing);
                Assert.True(geoField.NoIndex);
            },
            field =>
            {
                var vectorField = Assert.IsType<VectorFieldDefinition>(field);
                Assert.True(vectorField.IndexMissing);
                Assert.Equal(384, vectorField.Attributes.Dimensions);
                Assert.Equal(32, vectorField.Attributes.M);
                Assert.Equal(200, vectorField.Attributes.EfConstruction);
                Assert.Equal(24, vectorField.Attributes.EfRuntime);
            });
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
        Assert.Equal(':', schema.Index.KeySeparator);
        Assert.Null(schema.Index.Stopwords);
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
        Assert.Equal(':', schema.Index.KeySeparator);
        Assert.Null(schema.Index.Stopwords);
        Assert.Equal(StorageType.Json, schema.Index.StorageType);
    }

    [Fact]
    public void CreatesSchemaWithKeySeparatorAndDisabledStopwords()
    {
        var schema = new SearchSchema(
            new IndexDefinition("docs-idx", "docs:", StorageType.Json, keySeparator: '|', stopwords: []),
            Array.Empty<FieldDefinition>());

        Assert.Equal('|', schema.Index.KeySeparator);
        Assert.NotNull(schema.Index.Stopwords);
        Assert.Empty(schema.Index.Stopwords!);
    }

    [Fact]
    public void CreatesSchemaWithAdvancedFieldAndIndexOptions()
    {
        var schema = new SearchSchema(
            new IndexDefinition(
                "docs-idx",
                "docs:",
                StorageType.Json,
                maxTextFields: true,
                temporarySeconds: 600,
                noOffsets: true,
                noHighlight: true,
                noFields: true,
                noFrequencies: true,
                skipInitialScan: true),
            [
                new TextFieldDefinition(
                    "title",
                    sortable: true,
                    weight: 2.5,
                    withSuffixTrie: true,
                    indexMissing: true,
                    indexEmpty: true,
                    unNormalizedForm: true),
                new TagFieldDefinition(
                    "genre",
                    sortable: true,
                    separator: ';',
                    caseSensitive: true,
                    withSuffixTrie: true,
                    indexMissing: true,
                    indexEmpty: true,
                    noIndex: true),
                new NumericFieldDefinition("rating", sortable: true, indexMissing: true, noIndex: true, unNormalizedForm: true),
                new GeoFieldDefinition("location", sortable: true, indexMissing: true, noIndex: true),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(VectorAlgorithm.Hnsw, VectorDataType.Float32, VectorDistanceMetric.Cosine, 1536),
                    indexMissing: true)
            ]);

        Assert.True(schema.Index.MaxTextFields);
        Assert.Equal(600, schema.Index.TemporarySeconds);
        Assert.True(schema.Index.NoOffsets);
        Assert.True(schema.Index.NoHighlight);
        Assert.True(schema.Index.NoFields);
        Assert.True(schema.Index.NoFrequencies);
        Assert.True(schema.Index.SkipInitialScan);

        Assert.Collection(
            schema.Fields,
            field =>
            {
                var textField = Assert.IsType<TextFieldDefinition>(field);
                Assert.Equal(2.5, textField.Weight);
                Assert.True(textField.WithSuffixTrie);
                Assert.True(textField.IndexMissing);
                Assert.True(textField.IndexEmpty);
                Assert.True(textField.UnNormalizedForm);
            },
            field =>
            {
                var tagField = Assert.IsType<TagFieldDefinition>(field);
                Assert.True(tagField.WithSuffixTrie);
                Assert.True(tagField.IndexMissing);
                Assert.True(tagField.IndexEmpty);
                Assert.True(tagField.NoIndex);
            },
            field =>
            {
                var numericField = Assert.IsType<NumericFieldDefinition>(field);
                Assert.True(numericField.IndexMissing);
                Assert.True(numericField.NoIndex);
                Assert.True(numericField.UnNormalizedForm);
            },
            field =>
            {
                var geoField = Assert.IsType<GeoFieldDefinition>(field);
                Assert.True(geoField.IndexMissing);
                Assert.True(geoField.NoIndex);
            },
            field =>
            {
                var vectorField = Assert.IsType<VectorFieldDefinition>(field);
                Assert.True(vectorField.IndexMissing);
            });
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
        Assert.Throws<ArgumentException>(() => new IndexDefinition("docs-idx", "docs:", StorageType.Json, keySeparator: ' '));
        Assert.Throws<ArgumentException>(() => new IndexDefinition("docs-idx", "docs:", StorageType.Json, stopwords: ["the", " "]));
        Assert.Throws<ArgumentException>(() => new TextFieldDefinition(" "));
    }

    [Fact]
    public void RejectsInvalidAdvancedFieldAndIndexConfigurations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IndexDefinition("docs-idx", "docs:", StorageType.Json, temporarySeconds: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextFieldDefinition("title", weight: 0));
        Assert.Throws<ArgumentException>(() => new TextFieldDefinition("title", noIndex: true));
        Assert.Throws<ArgumentException>(() => new TextFieldDefinition("title", unNormalizedForm: true));
        Assert.Throws<ArgumentException>(() => new TagFieldDefinition("genre", separator: ' '));
        Assert.Throws<ArgumentException>(() => new TagFieldDefinition("genre", noIndex: true));
        Assert.Throws<ArgumentException>(() => new NumericFieldDefinition("rating", noIndex: true));
        Assert.Throws<ArgumentException>(() => new NumericFieldDefinition("rating", unNormalizedForm: true));
        Assert.Throws<ArgumentException>(() => new GeoFieldDefinition("location", noIndex: true));
    }

    [Fact]
    public void RejectsYamlWithInvalidIndexMetadata()
    {
        const string yaml = """
            index:
              name: docs-idx
              prefix: "docs:"
              key_separator: "::"
              storage_type: json
              stopwords:
                - the
                - " "
            fields:
              - name: title
                type: text
            """;

        Assert.Throws<ArgumentException>(() => SearchSchema.FromYaml(yaml));
    }

    [Fact]
    public void RejectsYamlWithUnsupportedProperties()
    {
        var path = Path.Combine(FixtureDirectory, "unsupported-schema.yaml");

        var exception = Assert.Throws<ArgumentException>(() => SearchSchema.FromYamlFile(path));

        Assert.Contains("could not be parsed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void RejectsYamlWithAmbiguousPrefixConfiguration()
    {
        const string yaml = """
            index:
              name: docs-idx
              prefix: "docs:"
              prefixes:
                - "docs:"
                - "archive:"
              storage_type: json
            fields:
              - name: title
                type: text
            """;

        var exception = Assert.Throws<ArgumentException>(() => SearchSchema.FromYaml(yaml));

        Assert.Equal("index", exception.ParamName);
        Assert.Contains("either index.prefix or index.prefixes", exception.Message, StringComparison.Ordinal);
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

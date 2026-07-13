using System.Buffers.Binary;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;

namespace RedisVL.Tests.Queries;

/// <summary>
/// Asserts the exact byte payload the <c>FromFloat32</c>/<c>FromFloat64</c> factories produce.
/// RediSearch expects vector query blobs as little-endian IEEE-754 (<c>FLOAT32</c>/<c>FLOAT64</c>),
/// and the command-builder suites render these blobs as the opaque token <c>&lt;binary&gt;</c>, so an
/// endianness or element-width (dtype) regression would otherwise be invisible. Expected bytes are
/// built with <see cref="BinaryPrimitives" /> as an independent oracle rather than the same
/// <c>MemoryMarshal</c> path the production code uses.
/// </summary>
public sealed class VectorByteEncodingTests
{
    private static readonly float[] Float32Vector = [1.5f, -2.0f, 3.25f];
    private static readonly double[] Float64Vector = [1.5d, -2.0d, 3.25d];

    private static byte[] ExpectedFloat32LittleEndian(float[] values)
    {
        var bytes = new byte[values.Length * sizeof(float)];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), values[i]);
        }

        return bytes;
    }

    private static byte[] ExpectedFloat64LittleEndian(double[] values)
    {
        var bytes = new byte[values.Length * sizeof(double)];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(bytes.AsSpan(i * sizeof(double)), values[i]);
        }

        return bytes;
    }

    [Fact]
    public void VectorQuery_FromFloat32_EncodesLittleEndianFloat32()
    {
        var query = VectorQuery.FromFloat32("embedding", Float32Vector, topK: 3);

        Assert.Equal(ExpectedFloat32LittleEndian(Float32Vector), query.Vector);
        Assert.Equal(Float32Vector.Length * sizeof(float), query.Vector.Length);
    }

    [Fact]
    public void VectorQuery_FromFloat64_EncodesLittleEndianFloat64()
    {
        var query = VectorQuery.FromFloat64("embedding", Float64Vector, topK: 3);

        Assert.Equal(ExpectedFloat64LittleEndian(Float64Vector), query.Vector);
        Assert.Equal(Float64Vector.Length * sizeof(double), query.Vector.Length);
    }

    [Fact]
    public void VectorRangeQuery_FromFloat32_EncodesLittleEndianFloat32()
    {
        var query = VectorRangeQuery.FromFloat32("embedding", Float32Vector, distanceThreshold: 0.3);

        Assert.Equal(ExpectedFloat32LittleEndian(Float32Vector), query.Vector);
    }

    [Fact]
    public void VectorRangeQuery_FromFloat64_EncodesLittleEndianFloat64()
    {
        var query = VectorRangeQuery.FromFloat64("embedding", Float64Vector, distanceThreshold: 0.3);

        Assert.Equal(ExpectedFloat64LittleEndian(Float64Vector), query.Vector);
    }

    [Fact]
    public void MultiVectorInput_FromFloat32_EncodesLittleEndianFloat32()
    {
        var input = MultiVectorInput.FromFloat32("embedding", Float32Vector);

        Assert.Equal(ExpectedFloat32LittleEndian(Float32Vector), input.Vector);
    }

    [Fact]
    public void MultiVectorInput_FromFloat64_EncodesLittleEndianFloat64()
    {
        var input = MultiVectorInput.FromFloat64("embedding", Float64Vector);

        Assert.Equal(ExpectedFloat64LittleEndian(Float64Vector), input.Vector);
    }

    [Fact]
    public void Float32AndFloat64_ProduceDistinctWidths()
    {
        // Guards against a dtype regression that marshals FLOAT64 through the FLOAT32 path (or vice
        // versa): the same logical vector must occupy 4 bytes/element as FLOAT32 and 8 as FLOAT64.
        var f32 = VectorQuery.FromFloat32("embedding", Float32Vector, topK: 3);
        var f64 = VectorQuery.FromFloat64("embedding", Float64Vector, topK: 3);

        Assert.Equal(f64.Vector.Length, f32.Vector.Length * 2);
    }

    [Fact]
    public void BuildVectorSearchArguments_EmitsLittleEndianVectorBlobAsParam()
    {
        // End-to-end: the byte blob the command builder places after PARAMS ... vector must be the
        // exact little-endian payload, not merely "some byte[]". Covers the <binary> blind spot in
        // SearchQueryCommandBuilderTests.
        var schema = new SearchSchema(
            new IndexDefinition("movies-idx", "movie:", StorageType.Hash),
            [
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        Float32Vector.Length,
                        m: 16,
                        efConstruction: 200))
            ]);
        var query = VectorQuery.FromFloat32("embedding", Float32Vector, topK: 3);

        var arguments = SearchQueryCommandBuilder.BuildVectorSearchArguments(schema, query);

        var blob = Assert.IsType<byte[]>(FindVectorParam(arguments));
        Assert.Equal(ExpectedFloat32LittleEndian(Float32Vector), blob);
    }

    // The PARAMS payload is [..., "PARAMS", count, "vector", <byte[]>, ...]; return the byte[] that
    // immediately follows the "vector" parameter name.
    private static object FindVectorParam(object[] arguments)
    {
        for (var index = 0; index + 1 < arguments.Length; index++)
        {
            if (arguments[index] is "vector")
            {
                return arguments[index + 1];
            }
        }

        throw new Xunit.Sdk.XunitException("No 'vector' PARAMS entry found in the built arguments.");
    }
}

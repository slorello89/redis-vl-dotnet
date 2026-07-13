using System.Runtime.InteropServices;

namespace RedisVL.Queries;

/// <summary>
/// Encodes floating-point query vectors into the little-endian byte payloads RediSearch expects.
/// </summary>
internal static class VectorEncoding
{
    /// <summary>Encodes a single-precision (<c>FLOAT32</c>) vector as bytes.</summary>
    /// <param name="vector">The query vector; must not be <see langword="null"/>.</param>
    /// <returns>The encoded bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="vector"/> is <see langword="null"/>.</exception>
    public static byte[] ToBytes(float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return MemoryMarshal.AsBytes<float>(vector.AsSpan()).ToArray();
    }

    /// <summary>Encodes a double-precision (<c>FLOAT64</c>) vector as bytes.</summary>
    /// <param name="vector">The query vector; must not be <see langword="null"/>.</param>
    /// <returns>The encoded bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="vector"/> is <see langword="null"/>.</exception>
    public static byte[] ToBytes(double[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return MemoryMarshal.AsBytes<double>(vector.AsSpan()).ToArray();
    }
}

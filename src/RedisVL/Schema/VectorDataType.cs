namespace RedisVL.Schema;

/// <summary>The numeric type of each element stored in a vector field (<c>TYPE</c>).</summary>
public enum VectorDataType
{
    /// <summary>32-bit IEEE floating point (<c>FLOAT32</c>).</summary>
    Float32 = 0,

    /// <summary>64-bit IEEE floating point (<c>FLOAT64</c>).</summary>
    Float64 = 1,

    /// <summary>16-bit IEEE half-precision floating point (<c>FLOAT16</c>).</summary>
    Float16 = 2,

    /// <summary>16-bit brain floating point (<c>BFLOAT16</c>).</summary>
    BFloat16 = 3,

    /// <summary>8-bit unsigned integer (<c>UINT8</c>).</summary>
    UInt8 = 4,

    /// <summary>8-bit signed integer (<c>INT8</c>).</summary>
    Int8 = 5
}

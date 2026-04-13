using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Indexes;

internal static class SearchIndexSeedData
{
    public static IReadOnlyList<HashSeedDocument> FilterMovies { get; } =
    [
        new(
            "1",
            [
                new HashEntry("title", "Heat"),
                new HashEntry("genre", "crime"),
                new HashEntry("year", 1995),
                new HashEntry("location", "-118.2437,34.0522")
            ]),
        new(
            "2",
            [
                new HashEntry("title", "Thief"),
                new HashEntry("genre", "crime"),
                new HashEntry("year", 1981),
                new HashEntry("location", "-87.6298,41.8781")
            ]),
        new(
            "3",
            [
                new HashEntry("title", "Arrival"),
                new HashEntry("genre", "science-fiction"),
                new HashEntry("year", 2016),
                new HashEntry("location", "-73.5673,45.5017")
            ])
    ];

    public static IReadOnlyList<HashSeedDocument> VectorMovies { get; } =
    [
        new(
            "1",
            [
                new HashEntry("title", "Heat"),
                new HashEntry("genre", "crime"),
                new HashEntry("embedding", EncodeFloat32([1f, 0f]))
            ]),
        new(
            "2",
            [
                new HashEntry("title", "Thief"),
                new HashEntry("genre", "crime"),
                new HashEntry("embedding", EncodeFloat32([0.8f, 0.2f]))
            ]),
        new(
            "3",
            [
                new HashEntry("title", "Arrival"),
                new HashEntry("genre", "science-fiction"),
                new HashEntry("embedding", EncodeFloat32([0f, 1f]))
            ])
    ];

    public static IReadOnlyList<HashSeedDocument> HybridMovies { get; } =
    [
        new(
            "1",
            [
                new HashEntry("title", "Heat"),
                new HashEntry("genre", "crime"),
                new HashEntry("embedding", EncodeFloat32([1f, 0f]))
            ]),
        new(
            "2",
            [
                new HashEntry("title", "Heatwave"),
                new HashEntry("genre", "crime"),
                new HashEntry("embedding", EncodeFloat32([0.8f, 0.2f]))
            ]),
        new(
            "3",
            [
                new HashEntry("title", "Arrival"),
                new HashEntry("genre", "science-fiction"),
                new HashEntry("embedding", EncodeFloat32([0f, 1f]))
            ])
    ];

    public static IReadOnlyList<HashSeedDocument> TextQueryMovies { get; } =
    [
        new(
            "1",
            [
                new HashEntry("title", "Heat Heat"),
                new HashEntry("genre", "crime"),
                new HashEntry("year", 1995)
            ]),
        new(
            "2",
            [
                new HashEntry("title", "Heat"),
                new HashEntry("genre", "crime"),
                new HashEntry("year", 1981)
            ]),
        new(
            "3",
            [
                new HashEntry("title", "Arrival"),
                new HashEntry("genre", "science-fiction"),
                new HashEntry("year", 2016)
            ])
    ];

    public static IReadOnlyList<HashSeedDocument> AggregationMovies { get; } =
    [
        new(
            "1",
            [
                new HashEntry("title", "Heat"),
                new HashEntry("genre", "crime"),
                new HashEntry("year", 1995)
            ]),
        new(
            "2",
            [
                new HashEntry("title", "Thief"),
                new HashEntry("genre", "crime"),
                new HashEntry("year", 1981)
            ]),
        new(
            "3",
            [
                new HashEntry("title", "Arrival"),
                new HashEntry("genre", "science-fiction"),
                new HashEntry("year", 2016)
            ])
    ];

    public static IReadOnlyList<HashSeedDocument> AggregateHybridMovies { get; } =
    [
        new(
            "1",
            [
                new HashEntry("title", "Heat"),
                new HashEntry("genre", "crime"),
                new HashEntry("embedding", EncodeFloat32([1f, 0f]))
            ]),
        new(
            "2",
            [
                new HashEntry("title", "Heatwave"),
                new HashEntry("genre", "crime"),
                new HashEntry("embedding", EncodeFloat32([0.8f, 0.2f]))
            ]),
        new(
            "3",
            [
                new HashEntry("title", "Arrival"),
                new HashEntry("genre", "science-fiction"),
                new HashEntry("embedding", EncodeFloat32([0.3f, 0.7f]))
            ]),
        new(
            "4",
            [
                new HashEntry("title", "Argo"),
                new HashEntry("genre", "thriller"),
                new HashEntry("embedding", EncodeFloat32([0.1f, 0.9f]))
            ])
    ];

    public sealed record HashSeedDocument(string Id, HashEntry[] Entries);

    private static byte[] EncodeFloat32(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}

using System.Collections.Concurrent;
using System.Text.Json;
using RedisVL.Indexes;
using StackExchange.Redis;

namespace RedisVL.Tests.Indexes;

public sealed class HashDocumentMapperTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ConvertsDocumentIntoCamelCaseHashEntries()
    {
        var entries = HashDocumentMapper.ToHashEntries(new HashMovieDocument("movie-1", "Heat", 1995, "crime"), SerializerOptions);

        Assert.Equal(
            [
                new HashEntry("id", "movie-1"),
                new HashEntry("title", "Heat"),
                new HashEntry("year", "1995"),
                new HashEntry("genre", "crime")
            ],
            entries);
    }

    [Fact]
    public void MaterializesTypedDocumentFromHashEntries()
    {
        var document = HashDocumentMapper.FromHashEntries<HashMovieDocument>(
            [
                new HashEntry("id", "movie-2"),
                new HashEntry("title", "Alien"),
                new HashEntry("year", "1979"),
                new HashEntry("genre", "sci-fi")
            ],
            SerializerOptions);

        Assert.NotNull(document);
        Assert.Equal("movie-2", document!.Id);
        Assert.Equal(1979, document.Year);
    }

    [Fact]
    public void ReturnsDefaultForMissingHashEntries()
    {
        var document = HashDocumentMapper.FromHashEntries<HashMovieDocument>([], SerializerOptions);

        Assert.Null(document);
    }

    [Fact]
    public void MaterializesConcurrentlyWithoutRacingOnCachedPropertyTypes()
    {
        // FromHashEntries caches per-type property metadata; materializing a cold-cache type from many
        // threads at once exercises the concurrent cache build and must stay correct with no exceptions.
        const int documentCount = 4_000;
        var entrySets = Enumerable.Range(0, documentCount)
            .Select(i => new[]
            {
                new HashEntry("id", $"movie-{i}"),
                new HashEntry("title", $"Movie {i}"),
                new HashEntry("year", (1900 + (i % 200)).ToString()),
                new HashEntry("genre", "crime")
            })
            .ToArray();

        var mapped = new HashConcurrentDocument?[documentCount];
        var failures = new ConcurrentQueue<Exception>();

        Parallel.For(0, documentCount, index =>
        {
            try
            {
                mapped[index] = HashDocumentMapper.FromHashEntries<HashConcurrentDocument>(entrySets[index], SerializerOptions);
            }
            catch (Exception exception)
            {
                failures.Enqueue(exception);
            }
        });

        Assert.Empty(failures);
        for (var i = 0; i < documentCount; i++)
        {
            Assert.NotNull(mapped[i]);
            Assert.Equal($"movie-{i}", mapped[i]!.Id);
            Assert.Equal(1900 + (i % 200), mapped[i]!.Year);
        }
    }

    private sealed record HashMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record HashConcurrentDocument(string Id, string Title, int Year, string Genre);
}

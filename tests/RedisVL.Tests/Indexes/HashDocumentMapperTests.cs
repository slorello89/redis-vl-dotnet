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

    private sealed record HashMovieDocument(string Id, string Title, int Year, string Genre);
}

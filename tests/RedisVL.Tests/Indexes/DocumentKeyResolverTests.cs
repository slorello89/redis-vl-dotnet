using RedisVL.Indexes;
using RedisVL.Schema;

namespace RedisVL.Tests.Indexes;

public sealed class DocumentKeyResolverTests
{
    private static readonly SearchSchema JsonSchema = new(
        new IndexDefinition("docs-idx", "doc:", StorageType.Json),
        [new TextFieldDefinition("title")]);

    [Fact]
    public void ResolvesKeyFromDocumentIdPropertyByDefault()
    {
        var key = DocumentKeyResolver.ResolveKey(JsonSchema, new MovieDocument("movie-1", "Heat"));

        Assert.Equal("doc:movie-1", key);
    }

    [Fact]
    public void ResolvesKeyFromExplicitIdSelectorForBatchLoads()
    {
        var key = DocumentKeyResolver.ResolveKeyForSelectors(
            JsonSchema,
            new AlternateMovieDocument("movie-2", "Alien"),
            idSelector: static document => document.ExternalId);

        Assert.Equal("doc:movie-2", key);
    }

    [Fact]
    public void ResolvesKeyFromExplicitKeyWhenProvided()
    {
        var key = DocumentKeyResolver.ResolveKey(JsonSchema, new MovieDocument("movie-1", "Heat"), key: "custom:key");

        Assert.Equal("custom:key", key);
    }

    [Fact]
    public void RejectsConflictingExplicitKeyAndId()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DocumentKeyResolver.ResolveKey(JsonSchema, new MovieDocument("movie-1", "Heat"), key: "custom:key", id: "movie-1"));

        Assert.Contains("Only one of key or id", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsDocumentWithoutResolvableKey()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DocumentKeyResolver.ResolveKey(JsonSchema, new TitleOnlyDocument("Heat")));

        Assert.Contains("could not be resolved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record MovieDocument(string Id, string Title);

    private sealed record AlternateMovieDocument(string ExternalId, string Title);

    private sealed record TitleOnlyDocument(string Title);
}

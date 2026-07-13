namespace RedisVL.Indexes;

/// <summary>Describes a single partial update to a JSON document, setting the value at a JSONPath location.</summary>
/// <param name="Path">The absolute JSONPath expression to update (for example <c>$.title</c> or <c>$.items[0]</c>).</param>
/// <param name="Value">The value to serialize and write at <paramref name="Path"/>.</param>
public readonly record struct JsonPartialUpdate(string Path, object? Value);

namespace RedisVL.Indexes;

public readonly record struct JsonPartialUpdate(string Path, object? Value);

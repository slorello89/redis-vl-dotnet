namespace RedisVl.Indexes;

public readonly record struct JsonPartialUpdate(string Path, object? Value);

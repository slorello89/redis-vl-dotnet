namespace RedisVL.Indexes;

/// <summary>Describes a single partial update to a hash document, setting the value of one hash field.</summary>
/// <param name="Field">The hash field name to update.</param>
/// <param name="Value">The value to write for <paramref name="Field"/>; must not be <see langword="null"/>.</param>
public readonly record struct HashPartialUpdate(string Field, object? Value);

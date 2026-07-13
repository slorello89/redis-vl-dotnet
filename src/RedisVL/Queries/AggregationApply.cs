namespace RedisVL.Queries;

/// <summary>
/// Represents an <c>APPLY</c> stage of an <c>FT.AGGREGATE</c> pipeline, which evaluates an expression
/// and emits the result under an alias.
/// </summary>
public sealed class AggregationApply
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationApply"/> class.
    /// </summary>
    /// <param name="expression">The expression to evaluate (for example <c>@price * @quantity</c>).</param>
    /// <param name="alias">The alias under which the computed value is emitted (a leading <c>@</c> is stripped).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="expression"/> or <paramref name="alias"/> is null or whitespace.</exception>
    public AggregationApply(string expression, string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        Expression = expression.Trim();
        Alias = alias.Trim().TrimStart('@');
    }

    /// <summary>
    /// Gets the expression evaluated by this <c>APPLY</c> stage.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Gets the alias under which the computed value is emitted.
    /// </summary>
    public string Alias { get; }
}

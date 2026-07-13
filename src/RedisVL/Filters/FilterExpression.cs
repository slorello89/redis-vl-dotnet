using System.Globalization;
using System.Text;

namespace RedisVL.Filters;

/// <summary>
/// The base type for a query filter expression that renders to a RediSearch query-string fragment.
/// Instances are produced by the field builders and combined with the boolean operators or the
/// <see cref="Filter"/> combinators.
/// </summary>
public abstract class FilterExpression
{
    /// <summary>Combines two expressions with logical AND (intersection).</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The combined expression.</returns>
    public static FilterExpression operator &(FilterExpression left, FilterExpression right) =>
        Filter.And(left, right);

    /// <summary>Combines two expressions with logical OR (union).</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The combined expression.</returns>
    public static FilterExpression operator |(FilterExpression left, FilterExpression right) =>
        Filter.Or(left, right);

    /// <summary>Negates an expression with logical NOT.</summary>
    /// <param name="expression">The expression to negate.</param>
    /// <returns>The negated expression.</returns>
    public static FilterExpression operator !(FilterExpression expression) =>
        Filter.Not(expression);

    /// <summary>Returns the rendered RediSearch query string for this expression.</summary>
    /// <returns>The query-string representation.</returns>
    public sealed override string ToString() => ToQueryString();

    /// <summary>Renders this expression to its RediSearch query-string fragment.</summary>
    /// <returns>The query-string representation.</returns>
    public string ToQueryString() => Render(grouped: false);

    internal abstract string Render(bool grouped);

    internal static string NormalizeFieldName(string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        var normalized = fieldName.Trim();
        if (normalized.StartsWith("@", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        return normalized;
    }

    internal static string FormatNumber(double value) =>
        value switch
        {
            double.NegativeInfinity => "-inf",
            double.PositiveInfinity => "+inf",
            _ => value.ToString("G", CultureInfo.InvariantCulture)
        };

    internal static string EscapeTagValue(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '_' )
            {
                builder.Append(character);
                continue;
            }

            builder.Append('\\');
            builder.Append(character);
        }

        return builder.ToString();
    }

    internal static string EscapeTextTerm(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        // Escape '*' so an exact-term match (`Match`) stays a literal term rather than silently
        // becoming a prefix wildcard, and so `Prefix` (which appends its own '*') cannot emit an
        // invalid double wildcard like `foo**`. Wildcard patterns go through the `w'...'` path instead.
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '_')
            {
                builder.Append(character);
                continue;
            }

            builder.Append('\\');
            builder.Append(character);
        }

        return builder.ToString();
    }

    internal static string EscapePhrase(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    internal static string EscapeWildcardPattern(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        // Inside a w'...' wildcard pattern only the backslash and the quote
        // delimiter are control characters; * and ? stay as wildcards.
        return value.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
    }

}

internal sealed class TagFilterExpression(string fieldName, IReadOnlyList<string> values, bool preserveWildcards = false) : FilterExpression
{
    private readonly string _fieldName = NormalizeFieldName(fieldName);
    private readonly IReadOnlyList<string> _values = values;
    private readonly bool _preserveWildcards = preserveWildcards;

    internal override string Render(bool grouped)
    {
        // Plain `{...}` tag syntax only treats `*` as a (prefix) wildcard and takes `?` literally, so
        // wildcard patterns are rendered with the `w'...'` form, which supports both `*` and `?`.
        Func<string, string> render = _preserveWildcards
            ? static value => $"w'{EscapeWildcardPattern(value)}'"
            : EscapeTagValue;
        var valueExpression = string.Join("|", _values.Select(render));
        return $"@{_fieldName}:{{{valueExpression}}}";
    }
}

internal sealed class NumericFilterExpression(
    string fieldName,
    double minimum,
    double maximum,
    bool inclusiveMinimum,
    bool inclusiveMaximum) : FilterExpression
{
    private readonly string _fieldName = NormalizeFieldName(fieldName);
    private readonly double _minimum = minimum;
    private readonly double _maximum = maximum;
    private readonly bool _inclusiveMinimum = inclusiveMinimum;
    private readonly bool _inclusiveMaximum = inclusiveMaximum;

    internal override string Render(bool grouped)
    {
        var minimum = _inclusiveMinimum ? FormatNumber(_minimum) : $"({FormatNumber(_minimum)}";
        var maximum = _inclusiveMaximum ? FormatNumber(_maximum) : $"({FormatNumber(_maximum)}";
        return $"@{_fieldName}:[{minimum} {maximum}]";
    }
}

internal sealed class TextFilterExpression(string fieldName, string query) : FilterExpression
{
    private readonly string _fieldName = NormalizeFieldName(fieldName);
    private readonly string _query = query;

    internal override string Render(bool grouped) => $"@{_fieldName}:{_query}";
}

internal sealed class GeoFilterExpression(
    string fieldName,
    double longitude,
    double latitude,
    double radius,
    GeoUnit unit) : FilterExpression
{
    private readonly string _fieldName = NormalizeFieldName(fieldName);
    private readonly double _longitude = longitude;
    private readonly double _latitude = latitude;
    private readonly double _radius = radius;
    private readonly GeoUnit _unit = unit;

    internal override string Render(bool grouped)
    {
        return $"@{_fieldName}:[{FormatNumber(_longitude)} {FormatNumber(_latitude)} {FormatNumber(_radius)} {ToRedisToken(_unit)}]";
    }

    private static string ToRedisToken(GeoUnit unit) =>
        unit switch
        {
            GeoUnit.Feet => "ft",
            GeoUnit.Kilometers => "km",
            GeoUnit.Meters => "m",
            GeoUnit.Miles => "mi",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported geo distance unit.")
        };
}

internal sealed class GeoBoxFilterExpression(
    string fieldName,
    double minLongitude,
    double minLatitude,
    double maxLongitude,
    double maxLatitude) : FilterExpression
{
    private readonly string _fieldName = NormalizeFieldName(fieldName);
    private readonly double _minLongitude = minLongitude;
    private readonly double _minLatitude = minLatitude;
    private readonly double _maxLongitude = maxLongitude;
    private readonly double _maxLatitude = maxLatitude;

    internal override string Render(bool grouped)
    {
        return $"@{_fieldName}:[{FormatNumber(_minLongitude)} {FormatNumber(_minLatitude)} {FormatNumber(_maxLongitude)} {FormatNumber(_maxLatitude)}]";
    }
}

internal enum LogicalOperator
{
    And,
    Or
}

internal sealed class LogicalFilterExpression(LogicalOperator operation, IReadOnlyList<FilterExpression> expressions) : FilterExpression
{
    internal LogicalOperator Operation { get; } = operation;

    internal IReadOnlyList<FilterExpression> Expressions { get; } = expressions;

    internal override string Render(bool grouped)
    {
        var separator = Operation == LogicalOperator.And ? " " : " | ";
        var rendered = string.Join(separator, Expressions.Select(static expression => expression.Render(grouped: true)));
        return grouped ? $"({rendered})" : rendered;
    }
}

internal sealed class NotFilterExpression(FilterExpression expression) : FilterExpression
{
    internal FilterExpression Expression { get; } = expression;

    internal override string Render(bool grouped)
    {
        return Expression switch
        {
            LogicalFilterExpression => $"-{Expression.Render(grouped: true)}",
            _ => $"-{Expression.Render(grouped: false)}"
        };
    }
}

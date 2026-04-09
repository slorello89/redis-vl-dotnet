using System.Globalization;
using System.Text;

namespace RedisVlDotNet.Filters;

public abstract class FilterExpression
{
    public static FilterExpression operator &(FilterExpression left, FilterExpression right) =>
        Filter.And(left, right);

    public static FilterExpression operator |(FilterExpression left, FilterExpression right) =>
        Filter.Or(left, right);

    public static FilterExpression operator !(FilterExpression expression) =>
        Filter.Not(expression);

    public sealed override string ToString() => ToQueryString();

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

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '*')
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
}

internal sealed class TagFilterExpression(string fieldName, IReadOnlyList<string> values) : FilterExpression
{
    private readonly string _fieldName = NormalizeFieldName(fieldName);
    private readonly IReadOnlyList<string> _values = values;

    internal override string Render(bool grouped)
    {
        var valueExpression = string.Join("|", _values.Select(EscapeTagValue));
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

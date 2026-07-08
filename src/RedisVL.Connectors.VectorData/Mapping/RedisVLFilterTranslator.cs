using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using RedisVL.Filters;

namespace RedisVL.Connectors.VectorData.Mapping;

/// <summary>
/// Translates a Microsoft.Extensions.VectorData LINQ filter
/// (<see cref="Expression{TDelegate}"/> of <c>Func&lt;TRecord, bool&gt;</c>) into a RedisVL
/// <see cref="FilterExpression"/>.
/// </summary>
internal sealed class RedisVLFilterTranslator
{
    private readonly RedisVLRecordModel _model;
    private ParameterExpression? _recordParameter;

    public RedisVLFilterTranslator(RedisVLRecordModel model)
    {
        _model = model;
    }

    public FilterExpression Translate(LambdaExpression filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _recordParameter = filter.Parameters[0];
        return Visit(filter.Body);
    }

    private FilterExpression Visit(Expression node) =>
        node switch
        {
            BinaryExpression binary => VisitBinary(binary),
            UnaryExpression { NodeType: ExpressionType.Not } not => Filter.Not(Visit(not.Operand)),
            MethodCallExpression call => VisitMethodCall(call),
            MemberExpression member when member.Type == typeof(bool) => MapBool(member, expected: true),
            UnaryExpression { NodeType: ExpressionType.Convert } convert => Visit(convert.Operand),
            _ => throw Unsupported(node),
        };

    private FilterExpression VisitBinary(BinaryExpression binary)
    {
        switch (binary.NodeType)
        {
            case ExpressionType.AndAlso:
                return Filter.And(Visit(binary.Left), Visit(binary.Right));
            case ExpressionType.OrElse:
                return Filter.Or(Visit(binary.Left), Visit(binary.Right));
        }

        // Comparison: one side is a record member, the other a constant/captured value.
        var (property, value, fieldOnLeft) = ResolveComparison(binary.Left, binary.Right);

        return binary.NodeType switch
        {
            ExpressionType.Equal => MapEquality(property, value, negate: false),
            ExpressionType.NotEqual => MapEquality(property, value, negate: true),
            ExpressionType.GreaterThan => MapNumeric(property, value, fieldOnLeft ? ExpressionType.GreaterThan : ExpressionType.LessThan),
            ExpressionType.GreaterThanOrEqual => MapNumeric(property, value, fieldOnLeft ? ExpressionType.GreaterThanOrEqual : ExpressionType.LessThanOrEqual),
            ExpressionType.LessThan => MapNumeric(property, value, fieldOnLeft ? ExpressionType.LessThan : ExpressionType.GreaterThan),
            ExpressionType.LessThanOrEqual => MapNumeric(property, value, fieldOnLeft ? ExpressionType.LessThanOrEqual : ExpressionType.GreaterThanOrEqual),
            _ => throw Unsupported(binary),
        };
    }

    private FilterExpression VisitMethodCall(MethodCallExpression call)
    {
        // Collection membership: values.Contains(record.Field)  OR  record.TagField.Contains(value)
        if (call.Method.Name == "Contains")
        {
            // Enumerable.Contains(source, item) or instanceList.Contains(item)
            var (source, item) = call.Object is null
                ? (call.Arguments[0], call.Arguments[1])
                : (call.Object, call.Arguments[0]);

            // On .NET 10, `array.Contains(x)` binds to MemoryExtensions.Contains, which wraps the
            // collection in an implicit ReadOnlySpan<T> conversion. That span is a ref struct we
            // cannot evaluate, so peel the conversion back to the underlying collection expression.
            source = UnwrapSpanConversion(source);

            // record.TagField.Contains(constant) -> membership in the tag set.
            if (TryGetProperty(source, out var collectionProperty))
            {
                var memberValue = Evaluate(item);
                return Filter.Tag(collectionProperty.JsonName).Eq(FormatTag(memberValue));
            }

            // constants.Contains(record.Field) -> field IN (values).
            if (TryGetProperty(item, out var property))
            {
                var values = Evaluate(source) as IEnumerable
                    ?? throw Unsupported(call);
                var formatted = values.Cast<object?>().Select(FormatTag).ToArray();
                return Filter.Tag(property.JsonName).In(formatted);
            }
        }

        if (call.Method.Name == "Equals" && call.Object is not null && call.Arguments.Count == 1)
        {
            var (property, value, _) = ResolveComparison(call.Object, call.Arguments[0]);
            return MapEquality(property, value, negate: false);
        }

        throw Unsupported(call);
    }

    private (RedisVLProperty Property, object? Value, bool FieldOnLeft) ResolveComparison(Expression left, Expression right)
    {
        if (TryGetProperty(left, out var leftProperty))
        {
            return (leftProperty, Evaluate(right), true);
        }

        if (TryGetProperty(right, out var rightProperty))
        {
            return (rightProperty, Evaluate(left), false);
        }

        throw new NotSupportedException("Filter comparison must reference a record property on one side.");
    }

    private FilterExpression MapBool(MemberExpression member, bool expected)
    {
        if (!TryGetProperty(member, out var property))
        {
            throw Unsupported(member);
        }

        return MapEquality(property, expected, negate: false);
    }

    private static FilterExpression MapEquality(RedisVLProperty property, object? value, bool negate)
    {
        FilterExpression expression = property.Kind switch
        {
            RedisVLFieldKind.Numeric => Filter.Numeric(property.JsonName).Eq(ToDouble(value)),
            RedisVLFieldKind.Tag => Filter.Tag(property.JsonName).Eq(FormatTag(value)),
            RedisVLFieldKind.Text => Filter.Text(property.JsonName).Match(FormatTag(value)),
            _ => throw new NotSupportedException(
                $"Equality filtering is not supported for property '{property.Property.Name}'."),
        };

        return negate ? Filter.Not(expression) : expression;
    }

    private static FilterExpression MapNumeric(RedisVLProperty property, object? value, ExpressionType comparison)
    {
        if (property.Kind != RedisVLFieldKind.Numeric)
        {
            throw new NotSupportedException(
                $"Range comparisons are only supported for numeric properties (property '{property.Property.Name}').");
        }

        var field = Filter.Numeric(property.JsonName);
        var number = ToDouble(value);
        return comparison switch
        {
            ExpressionType.GreaterThan => field.GreaterThan(number),
            ExpressionType.GreaterThanOrEqual => field.GreaterThanOrEqualTo(number),
            ExpressionType.LessThan => field.LessThan(number),
            ExpressionType.LessThanOrEqual => field.LessThanOrEqualTo(number),
            _ => throw new NotSupportedException($"Unsupported numeric comparison '{comparison}'."),
        };
    }

    private bool TryGetProperty(Expression expression, out RedisVLProperty property)
    {
        var node = Unwrap(expression);
        if (node is MemberExpression member
            && member.Expression is not null
            && IsRecordParameter(member.Expression)
            && member.Member is PropertyInfo propertyInfo
            && _model.ByClrName.TryGetValue(propertyInfo.Name, out var resolved))
        {
            property = resolved;
            return true;
        }

        property = null!;
        return false;
    }

    private bool IsRecordParameter(Expression expression) =>
        Unwrap(expression) is ParameterExpression parameter && parameter == _recordParameter;

    private static Expression Unwrap(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert
            ? Unwrap(convert.Operand)
            : expression;

    // Peels an implicit/explicit conversion to ReadOnlySpan<T>/Span<T> (e.g. the string[] ->
    // ReadOnlySpan<string> conversion .NET 10 inserts for MemoryExtensions.Contains) back to the
    // underlying collection expression so it can be evaluated as an IEnumerable.
    private static Expression UnwrapSpanConversion(Expression expression) =>
        expression is MethodCallExpression { Method.IsSpecialName: true, Arguments.Count: 1 } call
        && call.Method.Name is "op_Implicit" or "op_Explicit"
        && IsSpanType(call.Type)
            ? call.Arguments[0]
            : expression;

    private static bool IsSpanType(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>)
            || type.GetGenericTypeDefinition() == typeof(Span<>));

    private object? Evaluate(Expression expression)
    {
        var node = Unwrap(expression);
        if (node is ConstantExpression constant)
        {
            return constant.Value;
        }

        return Expression.Lambda(node).Compile().DynamicInvoke();
    }

    private static double ToDouble(object? value)
    {
        if (value is null)
        {
            throw new NotSupportedException("Numeric filter values cannot be null.");
        }

        if (value is bool boolean)
        {
            return boolean ? 1 : 0;
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private static string FormatTag(object? value) =>
        value switch
        {
            null => throw new NotSupportedException("Tag filter values cannot be null."),
            bool boolean => boolean ? "true" : "false",
            Enum enumeration => Convert.ToInt64(enumeration, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? throw new NotSupportedException("Tag filter value could not be formatted."),
        };

    private static NotSupportedException Unsupported(Expression node) =>
        new($"The filter expression node '{node.NodeType}' is not supported by the RedisVL connector.");
}

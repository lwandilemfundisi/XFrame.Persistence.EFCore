using System.Linq.Expressions;
using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore
{
    internal static class EntityFrameworkFilterExpressionBuilder
    {
        internal static Expression BuildExpression<TDomain>(BaseFilter filter, ParameterExpression parameter) where TDomain : class
        {
            if (filter is LogicalFilter logicalFilter)
            {
                var children = logicalFilter.Filters
                    .Select(child => BuildExpression<TDomain>(child, parameter))
                    .ToArray();

                return logicalFilter.Operator switch
                {
                    FilterLogicalOperator.And => children.Aggregate(Expression.AndAlso),
                    FilterLogicalOperator.Or => children.Aggregate(Expression.OrElse),
                    FilterLogicalOperator.Not => Expression.Not(children[0]),
                    _ => throw new NotSupportedException($"Unsupported logical operator: {logicalFilter.Operator}.")
                };
            }

            if (filter is AndFilter andFilter)
            {
                return Expression.AndAlso(
                    BuildExpression<TDomain>(andFilter.LeftFilter, parameter),
                    BuildExpression<TDomain>(andFilter.RightFilter, parameter));
            }

            if (filter is AnyFilter anyFilter)
            {
                var collection = GetProperty(parameter, anyFilter.Property);
                var elementType = collection.Type.IsArray
                    ? collection.Type.GetElementType()!
                    : collection.Type.GetInterfaces().Append(collection.Type)
                        .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0]
                        ?? throw new InvalidOperationException($"Property '{anyFilter.Property}' is not a generic enumerable navigation.");
                var elementParameter = Expression.Parameter(elementType, "item");
                var predicate = BuildExpression<object>(anyFilter.Predicate, elementParameter);
                var lambda = Expression.Lambda(predicate, elementParameter);
                var anyMethod = typeof(Enumerable).GetMethods()
                    .Single(method => method.Name == nameof(Enumerable.Any) && method.GetParameters().Length == 2)
                    .MakeGenericMethod(elementType);
                return Expression.Call(anyMethod, collection, lambda);
            }

            if (filter is EqualityFilter equalityFilter)
                return ResolveEqualityFilter(equalityFilter, parameter);

            if (filter is NullFilter nullFilter)
                return NullCheck(GetProperty(parameter, nullFilter.Property), isNull: true);

            throw new NotSupportedException($"Filter type '{filter.GetType().FullName}' is not supported by the EF Core expression builder.");
        }

        private static Expression ResolveEqualityFilter(EqualityFilter filter, ParameterExpression parameter)
        {
            var member = GetProperty(parameter, filter.Property);
            var value = filter.Value;

            switch (filter.Filter)
            {
                case FilterType.Equal:
                    return value is null ? NullCheck(member, isNull: true) : Expression.Equal(member, CreateTypedConstant(value, member.Type));
                case FilterType.NotEqual:
                    return value is null ? NullCheck(member, isNull: false) : Expression.NotEqual(member, CreateTypedConstant(value, member.Type));
                case FilterType.Null:
                    return NullCheck(member, isNull: true);
                case FilterType.NotNull:
                    return NullCheck(member, isNull: false);
                case FilterType.NotEmpty:
                    if (member.Type != typeof(string))
                        throw new InvalidOperationException("NotEmpty can only be applied to string properties.");
                    return Expression.AndAlso(
                        Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
                        Expression.NotEqual(member, Expression.Constant(string.Empty)));
                case FilterType.Like:
                    return StringCall(member, nameof(string.Contains), value);
                case FilterType.StartsWith:
                    return StringCall(member, nameof(string.StartsWith), value);
                case FilterType.GreaterThanOrEqualTo:
                    return Expression.GreaterThanOrEqual(member, CreateTypedConstant(value, member.Type));
                case FilterType.GreaterThan:
                    return Expression.GreaterThan(member, CreateTypedConstant(value, member.Type));
                case FilterType.LessThanOrEqualTo:
                    return Expression.LessThanOrEqual(member, CreateTypedConstant(value, member.Type));
                case FilterType.LessThan:
                    return Expression.LessThan(member, CreateTypedConstant(value, member.Type));
                case FilterType.In:
                    return CollectionContains(member, value, negate: false);
                case FilterType.NotIn:
                    return CollectionContains(member, value, negate: true);
                default:
                    throw new NotSupportedException($"Filter operator '{filter.Filter}' is not supported.");
            }
        }

        private static Expression GetProperty(Expression parameter, string propertyPath)
        {
            Expression current = parameter;
            foreach (var segment in propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
                current = Expression.PropertyOrField(current, segment);
            return current;
        }

        private static Expression NullCheck(Expression member, bool isNull)
        {
            var nullable = !member.Type.IsValueType || Nullable.GetUnderlyingType(member.Type) is not null;
            if (!nullable)
                return Expression.Constant(!isNull);

            var comparison = Expression.Equal(member, Expression.Constant(null, member.Type));
            return isNull ? comparison : Expression.Not(comparison);
        }

        private static Expression CreateTypedConstant(object value, Type targetType)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            var conversionType = underlyingType ?? targetType;
            var converted = value;

            if (!conversionType.IsInstanceOfType(value))
            {
                if (conversionType.IsEnum)
                    converted = value is string text
                        ? Enum.Parse(conversionType, text, ignoreCase: true)
                        : Enum.ToObject(conversionType, value);
                else
                    converted = Convert.ChangeType(value, conversionType, System.Globalization.CultureInfo.InvariantCulture);
            }

            var constant = Expression.Constant(converted, conversionType);
            return underlyingType is null ? constant : Expression.Convert(constant, targetType);
        }

        private static Expression StringCall(Expression member, string methodName, object value)
        {
            if (member.Type != typeof(string))
                throw new InvalidOperationException($"String operator '{methodName}' requires a string property.");
            if (value is not string text)
                throw new ArgumentException("A string filter requires a non-null string value.", nameof(value));

            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) });
            var call = Expression.Call(member, method, Expression.Constant(text));
            return Expression.AndAlso(
                Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
                call);
        }

        private static Expression CollectionContains(Expression member, object values, bool negate)
        {
            if (values is not System.Collections.IEnumerable enumerable || values is string)
                throw new ArgumentException("IN/NOT IN requires a non-string enumerable value.", nameof(values));

            var elementType = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
            var items = enumerable.Cast<object?>().ToArray();
            var includesNull = items.Any(item => item is null);
            var nonNullItems = items.Where(item => item is not null).ToArray();
            var typedValues = Array.CreateInstance(elementType, nonNullItems.Length);
            for (var index = 0; index < nonNullItems.Length; index++)
                typedValues.SetValue(ConvertCollectionValue(nonNullItems[index]!, elementType), index);

            Expression contains = Expression.Constant(false);
            if (nonNullItems.Length > 0)
            {
                var containsMethod = typeof(Enumerable).GetMethods()
                    .Single(method => method.Name == nameof(Enumerable.Contains) && method.GetParameters().Length == 2)
                    .MakeGenericMethod(elementType);

                Expression searchedMember = Nullable.GetUnderlyingType(member.Type) is null
                    ? member
                    : Expression.Property(member, "Value");
                contains = Expression.Call(containsMethod, Expression.Constant(typedValues), searchedMember);
                if (Nullable.GetUnderlyingType(member.Type) is not null)
                    contains = Expression.AndAlso(Expression.Property(member, "HasValue"), contains);
            }

            if (includesNull && (!member.Type.IsValueType || Nullable.GetUnderlyingType(member.Type) is not null))
                contains = Expression.OrElse(contains, NullCheck(member, isNull: true));

            return negate ? Expression.Not(contains) : contains;
        }

        private static object ConvertCollectionValue(object value, Type elementType)
        {
            if (elementType.IsInstanceOfType(value)) return value;
            if (elementType.IsEnum)
                return value is string text ? Enum.Parse(elementType, text, true) : Enum.ToObject(elementType, value);
            return Convert.ChangeType(value, elementType, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}


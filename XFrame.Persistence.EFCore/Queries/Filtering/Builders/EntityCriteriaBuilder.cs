using System.Linq.Expressions;
using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Queries.Filtering.Builders;

/// <summary>Strongly typed filter builder using property expressions.</summary>
public sealed class EntityCriteriaBuilder<TEntity> where TEntity : class
{
    private readonly DomainCriteriaBuilder _builder = new();

    public EntityCriteriaBuilder<TEntity> Where(BaseFilter filter) { _builder.Where(filter); return this; }
    public EntityCriteriaBuilder<TEntity> Equal<T>(Expression<Func<TEntity, T>> property, T value) => Add(property, value, FilterType.Equal);
    public EntityCriteriaBuilder<TEntity> NotEqual<T>(Expression<Func<TEntity, T>> property, T value) => Add(property, value, FilterType.NotEqual);
    public EntityCriteriaBuilder<TEntity> In<T>(Expression<Func<TEntity, T>> property, IEnumerable<T> values) => Add(property, values?.ToArray(), FilterType.In);
    public EntityCriteriaBuilder<TEntity> NotIn<T>(Expression<Func<TEntity, T>> property, IEnumerable<T> values) => Add(property, values?.ToArray(), FilterType.NotIn);
    public EntityCriteriaBuilder<TEntity> GreaterThan<T>(Expression<Func<TEntity, T>> property, T value) => Add(property, value, FilterType.GreaterThan);
    public EntityCriteriaBuilder<TEntity> GreaterThanOrEqual<T>(Expression<Func<TEntity, T>> property, T value) => Add(property, value, FilterType.GreaterThanOrEqualTo);
    public EntityCriteriaBuilder<TEntity> LessThan<T>(Expression<Func<TEntity, T>> property, T value) => Add(property, value, FilterType.LessThan);
    public EntityCriteriaBuilder<TEntity> LessThanOrEqual<T>(Expression<Func<TEntity, T>> property, T value) => Add(property, value, FilterType.LessThanOrEqualTo);
    public EntityCriteriaBuilder<TEntity> Contains(Expression<Func<TEntity, string>> property, string value) => Add(property, value, FilterType.Like);
    public EntityCriteriaBuilder<TEntity> StartsWith(Expression<Func<TEntity, string>> property, string value) => Add(property, value, FilterType.StartsWith);
    public EntityCriteriaBuilder<TEntity> IsNull<T>(Expression<Func<TEntity, T>> property) => Add(property, null, FilterType.Null);
    public EntityCriteriaBuilder<TEntity> IsNotNull<T>(Expression<Func<TEntity, T>> property) => Add(property, null, FilterType.NotNull);
    public EntityCriteriaBuilder<TEntity> IsNotEmpty(Expression<Func<TEntity, string>> property) => Add(property, null, FilterType.NotEmpty);

    public EntityCriteriaBuilder<TEntity> When(bool condition, Action<EntityCriteriaBuilder<TEntity>> configure)
    { ArgumentNullException.ThrowIfNull(configure); if (condition) configure(this); return this; }
    public EntityCriteriaBuilder<TEntity> Unless(bool condition, Action<EntityCriteriaBuilder<TEntity>> configure) => When(!condition, configure);
    public EntityCriteriaBuilder<TEntity> And(Action<EntityCriteriaBuilder<TEntity>> configure) => Group(FilterLogicalOperator.And, configure);
    public EntityCriteriaBuilder<TEntity> Or(Action<EntityCriteriaBuilder<TEntity>> configure) => Group(FilterLogicalOperator.Or, configure);
    public EntityCriteriaBuilder<TEntity> Not(Action<EntityCriteriaBuilder<TEntity>> configure) => Group(FilterLogicalOperator.Not, configure);

    public EntityCriteriaBuilder<TEntity> Any<TElement>(Expression<Func<TEntity, IEnumerable<TElement>>> navigation,
        Action<EntityCriteriaBuilder<TElement>> configure) where TElement : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        var child = new EntityCriteriaBuilder<TElement>();
        configure(child);
        var predicate = child.Build();
        if (predicate is not null) _builder.Where(FilterTree.CreateAny(PropertyPath(navigation), predicate));
        return this;
    }

    internal BaseFilter Build(FilterLogicalOperator logicalOperator = FilterLogicalOperator.And) => _builder.Build(logicalOperator);

    private EntityCriteriaBuilder<TEntity> Add<T>(Expression<Func<TEntity, T>> property, object value, FilterType type)
    { _builder.Where(new EqualityFilter(PropertyPath(property), value, type)); return this; }

    private EntityCriteriaBuilder<TEntity> Group(FilterLogicalOperator op, Action<EntityCriteriaBuilder<TEntity>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var child = new EntityCriteriaBuilder<TEntity>();
        configure(child);
        var filter = child.Build(op == FilterLogicalOperator.Not ? FilterLogicalOperator.And : op);
        if (filter is not null) _builder.Where(op == FilterLogicalOperator.Not ? FilterTree.CreateGroup(op, filter) : filter);
        return this;
    }

    internal static string PropertyPath<T>(Expression<Func<TEntity, T>> expression) => PropertyPath((LambdaExpression)expression);
    private static string PropertyPath(LambdaExpression expression)
    {
        Expression node = expression.Body;
        while (node is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked)) node = unary.Operand;
        var names = new Stack<string>();
        while (node is MemberExpression member)
        { names.Push(member.Member.Name); node = member.Expression!; }
        if (node != expression.Parameters[0] || names.Count == 0)
            throw new ArgumentException("A property selector must be a direct property path.", nameof(expression));
        return string.Join('.', names);
    }
}

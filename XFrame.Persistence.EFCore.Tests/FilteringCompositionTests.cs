using System.Linq.Expressions;
using XFrame.Persistence.EFCore;
using XFrame.Persistence.EFCore.Extensions;
using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.EFCore.Queries.Filtering.Builders;
using XFrame.Persistence.Queries.Filterings;
using Xunit;

namespace XFrame.Persistence.EFCore.Tests;

public sealed class FilteringCompositionTests
{
    [Fact]
    public void TypedBuilder_TranslatesCollectionAnyAndNestedPropertySelectors()
    {
        var criteria = new EFCoreDomainCriteria();
        criteria.BuildCriteria<Order>(filter => filter
            .Equal(order => order.Customer.Name, "Ada")
            .Any(order => order.Lines, line => line
                .Equal(line => line.ProductId, 42)
                .GreaterThan(line => line.Quantity, 0)));

        var predicate = Compile(criteria.Filter);
        var match = new Order
        {
            Customer = new Customer { Name = "Ada" },
            Lines = [new OrderLine { ProductId = 42, Quantity = 2 }]
        };
        var wrongProduct = new Order
        {
            Customer = new Customer { Name = "Ada" },
            Lines = [new OrderLine { ProductId = 7, Quantity = 2 }]
        };
        var nonPositiveQuantity = new Order
        {
            Customer = new Customer { Name = "Ada" },
            Lines = [new OrderLine { ProductId = 42, Quantity = 0 }]
        };
        var wrongCustomer = new Order
        {
            Customer = new Customer { Name = "Grace" },
            Lines = [new OrderLine { ProductId = 42, Quantity = 2 }]
        };

        Assert.True(predicate(match));
        Assert.False(predicate(wrongProduct));
        Assert.False(predicate(nonPositiveQuantity));
        Assert.False(predicate(new Order { Customer = new Customer { Name = "Ada" } }));
        Assert.False(predicate(wrongCustomer));

        var arrayCriteria = new EFCoreDomainCriteria();
        arrayCriteria.BuildCriteria<Order>(filter => filter.Any(order => order.LineArray,
            line => line.Equal(line => line.ProductId, 42)));
        Assert.True(Compile(arrayCriteria.Filter)(new Order { LineArray = [new OrderLine { ProductId = 42 }] }));
    }

    [Fact]
    public void TypedBuilder_SupportsNestedAnyAndLogicalGroups()
    {
        var criteria = new EFCoreDomainCriteria();
        criteria.BuildCriteria<Order>(filter => filter
            .Or(group => group
                .Equal(order => order.Status, "Priority")
                .Any(order => order.Lines, line => line
                    .Any(line => line.Discounts, discount => discount.Equal(discount => discount.Code, "VIP"))))
            .Not(group => group.Equal(order => order.IsArchived, true)));

        var predicate = Compile(criteria.Filter);
        Assert.True(predicate(new Order
        {
            IsActive = false,
            Status = "Standard",
            Lines = [new OrderLine { Discounts = [new Discount { Code = "VIP" }] }]
        }));
        Assert.False(predicate(new Order
        {
            IsActive = false,
            Status = "Priority",
            IsArchived = true
        }));
        Assert.False(predicate(new Order { IsActive = false, Status = "Standard" }));
    }

    [Fact]
    public void TypedBuilder_ConditionalClausesAndEmptyBuilderBehaveAsExpected()
    {
        var criteria = new EFCoreDomainCriteria();
        criteria.BuildCriteria<Order>(filter => filter
            .When(false, x => x.Equal(order => order.Status, "Ignored"))
            .Unless(false, x => x.Equal(order => order.IsActive, true)));

        Assert.True(Compile(criteria.Filter)(new Order { IsActive = true }));
        Assert.False(Compile(criteria.Filter)(new Order { IsActive = false }));

        var emptyCriteria = new EFCoreDomainCriteria();
        emptyCriteria.BuildCriteria<Order>(_ => { });
        Assert.Null(emptyCriteria.Filter);
    }

    [Fact]
    public void BuildCriteria_PreservesExistingFiltersWithAndSemantics()
    {
        var criteria = new EFCoreDomainCriteria
        {
            Filter = new AndFilter(
                new EqualityFilter(nameof(Order.IsActive), true),
                new NullFilter(nameof(Order.ArchivedAt)))
        };
        criteria.BuildCriteria<Order>(filter => filter.Equal(order => order.Status, "Open"));

        var predicate = Compile(criteria.Filter);
        Assert.True(predicate(new Order { IsActive = true, Status = "Open" }));
        Assert.True(predicate(new Order { IsActive = true, Status = "Open", ArchivedAt = null }));
        Assert.False(predicate(new Order { IsActive = false, Status = "Open" }));
        Assert.False(predicate(new Order { IsActive = true, Status = "Closed" }));
        Assert.False(predicate(new Order { IsActive = true, Status = "Open", ArchivedAt = DateTime.Today }));
    }

    [Fact]
    public void CriteriaExtensions_ValidateInputsAndAppendExistingFilters()
    {
        var criteria = new EFCoreDomainCriteria();
        var added = new EqualityFilter(nameof(Order.IsActive), true);
        Assert.Same(criteria, criteria.And(added));
        Assert.Same(added, criteria.Filter);
        Assert.Throws<ArgumentNullException>(() => EFCoreDomainCriteriaExtensions.BuildCriteria<Order>(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => EFCoreDomainCriteriaExtensions.BuildCriteria<Order>(criteria, null!));
        Assert.Throws<ArgumentNullException>(() => EFCoreDomainCriteriaExtensions.BuildCriteria(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => EFCoreDomainCriteriaExtensions.BuildCriteria(criteria, null!));
        Assert.Throws<ArgumentNullException>(() => criteria.And(null!));
        Assert.Throws<ArgumentNullException>(() => EFCoreDomainCriteriaExtensions.And(null!, added));
    }

    [Fact]
    public void TypedBuilder_RejectsComputedPropertySelectors()
    {
        var builder = new EntityCriteriaBuilder<Order>();
        Assert.Throws<ArgumentException>(() => builder.Equal(order => order.Total + 1, 10m));
        Assert.Throws<ArgumentException>(() => builder.Equal<Order>(order => order, new Order()));
    }

    [Fact]
    public void StringBuilder_ContinuesToTranslateLegacyAndFluentFilters()
    {
        var criteria = new EFCoreDomainCriteria();
        criteria.BuildCriteria(filter => filter
            .Equal(nameof(Order.IsActive), true)
            .GreaterThan(nameof(Order.Total), 20m));

        var predicate = Compile(criteria.Filter);
        Assert.True(predicate(new Order { IsActive = true, Total = 30m }));
        Assert.False(predicate(new Order { IsActive = true, Total = 10m }));
    }

    [Fact]
    public void LogicalAndAnyFilterNodes_RejectInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new LogicalFilter(FilterLogicalOperator.And, null!));
        Assert.Throws<ArgumentException>(() => new LogicalFilter(FilterLogicalOperator.And));
        Assert.Throws<ArgumentException>(() => new LogicalFilter(FilterLogicalOperator.Not,
            new EqualityFilter(nameof(Order.IsActive), true), new EqualityFilter(nameof(Order.Status), "Open")));
        Assert.Throws<ArgumentException>(() => new LogicalFilter(FilterLogicalOperator.And,
            new BaseFilter[] { null! }));
        Assert.Throws<ArgumentException>(() => new AnyFilter(" ", new EqualityFilter(nameof(Order.IsActive), true)));
        Assert.Throws<ArgumentNullException>(() => new AnyFilter(nameof(Order.Lines), null!));

        var invalidOperator = new LogicalFilter((FilterLogicalOperator)999,
            new EqualityFilter(nameof(Order.IsActive), true));
        Assert.Throws<NotSupportedException>(() => Compile(invalidOperator));
        Assert.Throws<InvalidOperationException>(() => Compile(new AnyFilter(nameof(Order.Total),
            new EqualityFilter(nameof(Order.IsActive), true))));
    }

    [Fact]
    public void DomainBuilder_ValidatesInputsAndSkipsEmptyGroups()
    {
        var builder = new DomainCriteriaBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.When(true, null!));
        Assert.Throws<ArgumentNullException>(() => builder.Where(null!));
        Assert.Throws<ArgumentNullException>(() => builder.And(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Or(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Not(null!));
        Assert.Throws<ArgumentException>(() => builder.Equal(" ", "x"));
        Assert.Throws<ArgumentNullException>(() => builder.In<string>(nameof(Order.Status), null!));
        Assert.Throws<ArgumentNullException>(() => builder.NotIn<string>(nameof(Order.Status), null!));

        var noOp = builder.When(false, _ => throw new InvalidOperationException())
            .Unless(true, _ => throw new InvalidOperationException())
            .And(_ => { }).Or(_ => { }).Not(_ => { });
        Assert.Null(noOp.Build());

        var single = new DomainCriteriaBuilder().Equal(nameof(Order.Status), "Open").Build();
        Assert.IsType<EqualityFilter>(single);
        var grouped = new DomainCriteriaBuilder()
            .Equal(nameof(Order.IsActive), true)
            .Equal(nameof(Order.Status), "Open")
            .Build();
        Assert.Equal(FilterLogicalOperator.And, Assert.IsType<LogicalFilter>(grouped).Operator);
    }

    [Fact]
    public void EntityBuilder_ValidatesCallbacksSelectorsAndCollections()
    {
        var builder = new EntityCriteriaBuilder<Order>();
        Assert.Throws<ArgumentNullException>(() => builder.When(true, null!));
        Assert.Throws<ArgumentNullException>(() => builder.And(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Or(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Not(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Where(null!));
        Assert.Throws<ArgumentNullException>(() => builder.In(order => order.Status, null!));
        Assert.Throws<ArgumentNullException>(() => builder.NotIn(order => order.Status, null!));
        Assert.Throws<ArgumentNullException>(() => builder.Any(order => order.Lines, null!));
        Assert.Throws<ArgumentNullException>(() => builder.Any<OrderLine>(null!, line => line.Equal(x => x.ProductId, 1)));
        Assert.Throws<ArgumentNullException>(() => builder.Equal<bool>(null!, true));
        Assert.Throws<ArgumentException>(() => builder.Equal(order => order.Total + 1, 10m));

        var boxed = new EntityCriteriaBuilder<Order>().Equal(order => (object)order.IsActive, true).Build();
        Assert.True(Compile(boxed)(new Order { IsActive = true }));
        Assert.Null(new EntityCriteriaBuilder<Order>().Any(order => order.Lines, _ => { }).Build());
    }

    [Fact]
    public void ExpressionBuilder_TranslatesAllComparisonAndStringOperators()
    {
        Assert.True(Compile(new EqualityFilter(nameof(Order.Total), 10, FilterType.Equal))(new Order { Total = 10m }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.OptionalNumber), 4, FilterType.Equal))(new Order { OptionalNumber = 4 }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Total), 9m, FilterType.NotEqual))(new Order { Total = 10m }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.OptionalStatus), null, FilterType.NotEqual))(new Order { OptionalStatus = "Ready" }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Total), 10m, FilterType.GreaterThanOrEqualTo))(new Order { Total = 10m }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Total), 9m, FilterType.GreaterThan))(new Order { Total = 10m }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Total), 10m, FilterType.LessThanOrEqualTo))(new Order { Total = 10m }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Total), 11m, FilterType.LessThan))(new Order { Total = 10m }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Status), "pen", FilterType.Like))(new Order { Status = "Open" }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Status), "Op", FilterType.StartsWith))(new Order { Status = "Open" }));
        Assert.False(Compile(new EqualityFilter(nameof(Order.Status), "pen", FilterType.Like))(new Order { Status = null! }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.OptionalStatus), null, FilterType.Equal))(new Order()));
        Assert.True(Compile(new EqualityFilter(nameof(Order.OptionalStatus), null, FilterType.NotEqual))(new Order { OptionalStatus = "Ready" }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Status), null, FilterType.NotEmpty))(new Order { Status = "Open" }));
        Assert.False(Compile(new EqualityFilter(nameof(Order.Status), null, FilterType.NotEmpty))(new Order { Status = string.Empty }));
        Assert.False(Compile(new EqualityFilter(nameof(Order.Total), null, FilterType.Null))(new Order { Total = 10m }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Total), null, FilterType.NotNull))(new Order { Total = 10m }));
        Assert.False(Compile(new EqualityFilter(nameof(Order.Total), null, FilterType.Null))(new Order { Total = 10m }));
        Assert.Throws<InvalidOperationException>(() => Compile(new EqualityFilter(nameof(Order.Total), null, FilterType.NotEmpty)));
        Assert.Throws<ArgumentException>(() => Compile(new EqualityFilter(nameof(Order.Status), null, FilterType.Like)));
        Assert.False(Compile(new EqualityFilter(nameof(Order.Status), null, FilterType.NotEmpty))(new Order { Status = null! }));
    }

    [Fact]
    public void ExpressionBuilder_HandlesMembershipNullsEnumsAndInvalidOperators()
    {
        var contains = Compile(new EqualityFilter(nameof(Order.Status), new[] { "Open", "Ready" }, FilterType.In));
        Assert.True(contains(new Order { Status = "Ready" }));
        Assert.False(contains(new Order { Status = "Closed" }));
        Assert.False(Compile(new EqualityFilter(nameof(Order.Status), Array.Empty<string>(), FilterType.In))(new Order { Status = "Open" }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.OptionalStatus), new string?[] { null }, FilterType.In))(new Order()));
        Assert.False(Compile(new EqualityFilter(nameof(Order.OptionalStatus), new string?[] { null }, FilterType.NotIn))(new Order()));
        Assert.True(Compile(new EqualityFilter(nameof(Order.OptionalStatus), new string?[] { null, "Open" }, FilterType.In))(new Order { OptionalStatus = "Open" }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Status), new[] { "Closed" }, FilterType.NotIn))(new Order { Status = "Open" }));
        Assert.Throws<ArgumentException>(() => Compile(new EqualityFilter(nameof(Order.Total), 5, FilterType.In)));
        Assert.True(Compile(new EqualityFilter(nameof(Order.OptionalNumber), new int?[] { 4 }, FilterType.In))(new Order { OptionalNumber = 4 }));
        Assert.False(Compile(new EqualityFilter(nameof(Order.OptionalNumber), new int?[] { 4 }, FilterType.In))(new Order()));
        Assert.True(Compile(new EqualityFilter(nameof(Order.OptionalNumber), new int?[] { null }, FilterType.In))(new Order()));
        Assert.False(Compile(new EqualityFilter(nameof(Order.IsActive), new bool?[] { null }, FilterType.In))(new Order { IsActive = true }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.State), "Ready", FilterType.Equal))(new Order { State = OrderState.Ready }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.State), 1, FilterType.Equal))(new Order { State = OrderState.Ready }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.State), new[] { "Ready" }, FilterType.In))(new Order { State = OrderState.Ready }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.State), new[] { 1 }, FilterType.In))(new Order { State = OrderState.Ready }));
        Assert.True(Compile(new EqualityFilter(nameof(Order.Total), new[] { 10 }, FilterType.In))(new Order { Total = 10m }));
        Assert.Throws<ArgumentException>(() => Compile(new EqualityFilter(nameof(Order.Status), "Open", FilterType.In)));
        Assert.Throws<InvalidOperationException>(() => Compile(new EqualityFilter(nameof(Order.Total), null, FilterType.Like)));
        Assert.Throws<InvalidOperationException>(() => Compile(new EqualityFilter(nameof(Order.Total), null, FilterType.NotEmpty)));
        Assert.Throws<NotSupportedException>(() => Compile(new EqualityFilter(nameof(Order.Status), "x", (FilterType)999)));
        Assert.Throws<NotSupportedException>(() => Compile(new UnsupportedFilter()));
    }

    [Fact]
    public void BothBuilders_ExposeTheFullFluentCompositionSurface()
    {
        var existing = new EqualityFilter(nameof(Order.Status), "Open");
        var stringBuilder = new DomainCriteriaBuilder()
            .Where(existing)
            .When(true, x => x.NotEqual(nameof(Order.Status), "Closed"))
            .Unless(false, x => x.In(nameof(Order.Status), new List<string> { "Open", "Ready" }))
            .NotIn(nameof(Order.Status), new[] { "Closed" })
            .GreaterThan(nameof(Order.Total), 1m)
            .GreaterThanOrEqual(nameof(Order.Total), 1m)
            .LessThan(nameof(Order.Total), 100m)
            .LessThanOrEqual(nameof(Order.Total), 100m)
            .Contains(nameof(Order.Status), "p")
            .StartsWith(nameof(Order.Status), "O")
            .IsNull(nameof(Order.OptionalStatus))
            .IsNotNull(nameof(Order.Status))
            .IsNotEmpty(nameof(Order.Status))
            .And(x => x.Equal(nameof(Order.IsActive), true).Equal(nameof(Order.IsArchived), false))
            .Or(x => x.Equal(nameof(Order.Status), "Open").Equal(nameof(Order.Status), "Ready"))
            .Not(x => x.IsNull(nameof(Order.OptionalStatus)));
        Assert.IsType<LogicalFilter>(stringBuilder.Build());

        var expressionBuilder = new EntityCriteriaBuilder<Order>()
            .Where(existing)
            .When(true, x => x.NotEqual(order => order.Status, "Closed"))
            .Unless(false, x => x.In(order => order.Status, new[] { "Open", "Ready" }))
            .NotIn(order => order.Status, new[] { "Closed" })
            .GreaterThan(order => order.Total, 1m)
            .GreaterThanOrEqual(order => order.Total, 1m)
            .LessThan(order => order.Total, 100m)
            .LessThanOrEqual(order => order.Total, 100m)
            .Contains(order => order.Status, "p")
            .StartsWith(order => order.Status, "O")
            .IsNull(order => order.OptionalStatus)
            .IsNotNull(order => order.Status)
            .IsNotEmpty(order => order.Status)
            .And(x => x.Equal(order => order.IsActive, true).Equal(order => order.IsArchived, false))
            .Or(x => x.Equal(order => order.Status, "Open").Equal(order => order.Status, "Ready"))
            .Not(x => x.IsNull(order => order.OptionalStatus));
        Assert.IsType<LogicalFilter>(expressionBuilder.Build());
    }

    private static Func<Order, bool> Compile(BaseFilter filter)
    {
        var parameter = Expression.Parameter(typeof(Order), "entity");
        var body = EntityFrameworkFilterExpressionBuilder.BuildExpression<Order>(filter, parameter);
        return Expression.Lambda<Func<Order, bool>>(body, parameter).Compile();
    }

    private sealed class Order
    {
        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? OptionalStatus { get; set; }
        public int? OptionalNumber { get; set; }
        public OrderState State { get; set; }
        public decimal Total { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public Customer Customer { get; set; } = new();
        public List<OrderLine> Lines { get; set; } = [];
        public OrderLine[] LineArray { get; set; } = [];
    }

    private sealed class Customer { public string Name { get; set; } = string.Empty; }
    private sealed class OrderLine
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public List<Discount> Discounts { get; set; } = [];
    }
    private sealed class Discount { public string Code { get; set; } = string.Empty; }
    private enum OrderState { Draft, Ready }
    private sealed class UnsupportedFilter : BaseFilter { }
}

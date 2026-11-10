using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using XFrame.Common.Extensions;
using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore
{
    internal static class EntityFrameworkExpressionBuilder
    {
        public static IQueryable<TDomain> BuildCriteria<TDomain, TDomainCriteria>(DbSet<TDomain> dbSet,
            TDomainCriteria domainCriteria)
            where TDomain : class
            where TDomainCriteria : DomainCriteria
        {
            IQueryable<TDomain> queryableData = dbSet.AsQueryable<TDomain>();

            Expression expression = null;
            ParameterExpression parameter = Expression.Parameter(typeof(TDomain), "pe");

            if (domainCriteria.Filter.IsNotNull())
            {
                expression = EntityFrameworkFilterExpressionBuilder.BuildExpression<TDomain>(domainCriteria.Filter, parameter);
            }

            if (expression != null)
            {
                expression = Expression.Call(
                typeof(Queryable),
                "Where",
                new Type[] { queryableData.ElementType },
                queryableData.Expression,
                Expression.Lambda<Func<TDomain, bool>>(expression, new ParameterExpression[] { parameter }));
            }

            expression = AddOrder(expression ?? queryableData.Expression, domainCriteria, queryableData, parameter);

            expression = Include(expression ?? queryableData.Expression, domainCriteria, queryableData, parameter);

            return dbSet.AsQueryable<TDomain>().Provider.CreateQuery<TDomain>(expression);
        }

        private static Expression Include<TDomain, TDomainCriteria>(Expression filterExpression,
            TDomainCriteria domainCriteria,
            IQueryable<TDomain> queryableData,
            ParameterExpression parameter) 
            where TDomain : class
            where TDomainCriteria : DomainCriteria
        {
            var efdomainCriteria = domainCriteria as EFCoreDomainCriteria;

            if(efdomainCriteria.IsNotNull())
            {
                if (efdomainCriteria.Include.IsNotNull())
                {
                    var includeExpression = Expression.Lambda(
                        Expression.PropertyOrField(parameter, efdomainCriteria.Include.PropertyName),
                        parameter);

                    return Expression.Call(
                        typeof(EntityFrameworkQueryableExtensions),
                        "Include",
                        new Type[] { queryableData.ElementType, includeExpression.ReturnType },
                        filterExpression,
                        Expression.Quote(includeExpression));
                }
            }

            return filterExpression;
        }

        private static Expression AddOrder<TDomain>(Expression filterExpression,
            DomainCriteria domainCriteria,
            IQueryable<TDomain> queryableData,
            ParameterExpression parameter) where TDomain : class
        {
            if (domainCriteria.SortOrder.IsNotNull())
            {
                Expression propertyToOrderByExpression = Expression.Convert(Expression.Property(parameter, domainCriteria.SortOrder.PropertyName), typeof(object));

                switch (domainCriteria.SortOrder.SortOrderType)
                {
                    case SortOrderType.Ascending:
                        {
                            var orderByExpression = Expression.Lambda(propertyToOrderByExpression, parameter);

                            return Expression.Call(
                                typeof(Queryable),
                                "OrderBy",
                                new Type[] { queryableData.ElementType, orderByExpression.ReturnType },
                                filterExpression,
                                Expression.Quote(orderByExpression));
                        }
                    case SortOrderType.Descending:
                        {
                            var orderByExpression = Expression.Lambda(propertyToOrderByExpression, parameter);

                            return Expression.Call(
                                typeof(Queryable),
                                "OrderByDescending",
                                new Type[] { queryableData.ElementType, orderByExpression.ReturnType },
                                filterExpression,
                                Expression.Quote(orderByExpression));
                        }
                }
            }

            return filterExpression;
        }

    }
}


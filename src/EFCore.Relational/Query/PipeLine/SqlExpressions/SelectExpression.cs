// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.PipeLine;

namespace Microsoft.EntityFrameworkCore.Relational.Query.PipeLine.SqlExpressions
{
    public class SelectExpression : TableExpressionBase
    {
        private IDictionary<ProjectionMember, Expression> _projectionMapping
            = new Dictionary<ProjectionMember, Expression>();

        private List<TableExpressionBase> _tables = new List<TableExpressionBase>();
        private readonly List<SqlExpression> _projection = new List<SqlExpression>();
        private List<OrderingExpression> _orderings = new List<OrderingExpression>();

        public IReadOnlyList<SqlExpression> Projection => _projection;
        public IReadOnlyList<TableExpressionBase> Tables => _tables;
        public IReadOnlyList<OrderingExpression> Orderings => _orderings;
        public SqlExpression Predicate { get; private set; }
        public SqlExpression Limit { get; private set; }
        public SqlExpression Offset { get; private set; }
        public bool IsDistinct { get; private set; }

        public SelectExpression(
            IDictionary<ProjectionMember, Expression> projectionMapping,
            List<TableExpressionBase> tables)
            : base("")
        {
            _projectionMapping = projectionMapping;
            _tables = tables;
        }

        public SelectExpression(IEntityType entityType)
            : base("")
        {
            var tableExpression = new TableExpression(
                entityType.Relational().TableName,
                entityType.Relational().Schema,
                entityType.Relational().TableName.ToLower().Substring(0, 1));

            _tables.Add(tableExpression);

            _projectionMapping[new ProjectionMember()] = new EntityProjectionExpression(entityType, tableExpression);
        }

        public SqlExpression BindProperty(Expression projectionExpression, IProperty property)
        {
            var member = (projectionExpression as ProjectionBindingExpression).ProjectionMember;

            return ((EntityProjectionExpression)_projectionMapping[member]).GetProperty(property);
        }

        public void ApplyProjection()
        {
            var index = 0;
            var result = new Dictionary<ProjectionMember, Expression>();
            foreach (var keyValuePair in _projectionMapping)
            {
                result[keyValuePair.Key] = Constant(index);
                if (keyValuePair.Value is EntityProjectionExpression entityProjection)
                {
                    foreach (var property in entityProjection.EntityType.GetProperties())
                    {
                        _projection.Add(entityProjection.GetProperty(property));
                        index++;
                    }
                }
                else
                {
                    _projection.Add((SqlExpression)keyValuePair.Value);
                    index++;
                }
            }

            _projectionMapping = result;
        }

        public void ApplyPredicate(SqlExpression expression)
        {
            if (expression is SqlConstantExpression sqlConstant
                && (bool)sqlConstant.Value)
            {
                return;
            }

            if (Predicate == null)
            {
                Predicate = expression;
            }
            else
            {
                Predicate = new SqlBinaryExpression(
                    ExpressionType.AndAlso,
                    Predicate,
                    expression,
                    typeof(bool),
                    expression.TypeMapping);
            }
        }

        public override ExpressionType NodeType => ExpressionType.Extension;

        public void ApplyProjection(IDictionary<ProjectionMember, Expression> projectionMapping)
        {
            _projectionMapping.Clear();

            foreach (var kvp in projectionMapping)
            {
                _projectionMapping[kvp.Key] = kvp.Value;
            }
        }

        public Expression GetProjectionExpression(ProjectionMember projectionMember)
        {
            return _projectionMapping[projectionMember];
        }

        public void ApplyOrderBy(OrderingExpression orderingExpression)
        {
            _orderings.Clear();
            _orderings.Add(orderingExpression);
        }

        public void ApplyThenBy(OrderingExpression orderingExpression)
        {
            if (!_orderings.Contains(orderingExpression))
            {
                _orderings.Add(orderingExpression);
            }
        }

        public void ApplyLimit(SqlExpression sqlExpression)
        {
            Limit = sqlExpression;
        }

        public void ApplyOffset(SqlExpression sqlExpression)
        {
            Offset = sqlExpression;
        }

        public void Reverse()
        {
            var existingOrdering = _orderings.ToArray();

            _orderings.Clear();

            for (var i = 0; i < existingOrdering.Length; i++)
            {
                _orderings.Add(
                    new OrderingExpression(
                        existingOrdering[i].Expression,
                        !existingOrdering[i].Ascending));
            }
        }

        public void ApplyDistinct()
        {
            IsDistinct = true;
            ClearOrdering();
        }

        public void ClearOrdering()
        {
            _orderings.Clear();
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var changed = false;
            var projectionMapping = new Dictionary<ProjectionMember, Expression>();
            foreach (var mapping in _projectionMapping)
            {
                var newProjection = visitor.Visit(mapping.Value);
                changed |= newProjection != mapping.Value;

                projectionMapping[mapping.Key] = newProjection;
            }

            var tables = new List<TableExpressionBase>();
            foreach (var table in _tables)
            {
                var newTable = (TableExpressionBase)visitor.Visit(table);
                changed |= newTable != table;
                tables.Add(newTable);
            }

            var predicate = (SqlExpression)visitor.Visit(Predicate);
            changed |= predicate != Predicate;

            var orderings = new List<OrderingExpression>();
            foreach (var ordering in orderings)
            {
                var newOrderingExpression = (SqlExpression)visitor.Visit(ordering.Expression);
                changed |= newOrderingExpression != ordering.Expression;
                orderings.Add(new OrderingExpression(newOrderingExpression, ordering.Ascending));
            }

            var offset = (SqlExpression)visitor.Visit(Offset);
            changed |= offset != Offset;

            var limit = (SqlExpression)visitor.Visit(Limit);
            changed |= limit != Limit;

            if (changed)
            {
                var newSelectExpression = new SelectExpression(projectionMapping, tables)
                {
                    Predicate = predicate,
                    _orderings = orderings,
                    Offset = offset,
                    Limit = limit
                };

                return newSelectExpression;

            }

            return this;
        }

        public override bool Equals(object obj)
            => obj != null
            && (ReferenceEquals(this, obj)
                || obj is SelectExpression selectExpression
                    && Equals(selectExpression));

        private bool Equals(SelectExpression selectExpression)
        {
            if (!base.Equals(selectExpression))
            {
                return false;
            }

            foreach (var projectionMapping in _projectionMapping)
            {
                if (!selectExpression._projectionMapping.TryGetValue(projectionMapping.Key, out var projection))
                {
                    return false;
                }

                if (!projectionMapping.Value.Equals(projection))
                {
                    return false;
                }
            }

            if (!_tables.SequenceEqual(selectExpression._tables))
            {
                return false;
            }

            if (!(Predicate == null && selectExpression.Predicate == null
                || Predicate != null && Predicate.Equals(selectExpression.Predicate)))
            {
                return false;
            }

            if (!_orderings.SequenceEqual(selectExpression._orderings))
            {
                return false;
            }

            if (!(Offset == null && selectExpression.Offset == null
                || Offset != null && Offset.Equals(selectExpression.Offset)))
            {
                return false;
            }

            if (!(Limit == null && selectExpression.Limit == null
                || Limit != null && Limit.Equals(selectExpression.Limit)))
            {
                return false;
            }

            return IsDistinct == selectExpression.IsDistinct;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = base.GetHashCode();
                foreach (var projectionMapping in _projectionMapping)
                {
                    hashCode = (hashCode * 397) ^ projectionMapping.Key.GetHashCode();
                    hashCode = (hashCode * 397) ^ projectionMapping.Value.GetHashCode();
                }

                hashCode = (hashCode * 397) ^ _tables.Aggregate(
                    0, (current, value) => current + ((current * 397) ^ value.GetHashCode()));

                hashCode = (hashCode * 397) ^ (Predicate?.GetHashCode() ?? 0);

                hashCode = (hashCode * 397) ^ _orderings.Aggregate(
                    0, (current, value) => current + ((current * 397) ^ value.GetHashCode()));

                hashCode = (hashCode * 397) ^ (Offset?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Limit?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ IsDistinct.GetHashCode();

                return hashCode;
            }
        }
    }
}

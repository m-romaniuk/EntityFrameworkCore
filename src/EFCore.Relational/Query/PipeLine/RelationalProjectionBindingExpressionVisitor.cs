// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.PipeLine;
using Microsoft.EntityFrameworkCore.Relational.Query.PipeLine.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Relational.Query.PipeLine
{
    public class RelationalProjectionBindingExpressionVisitor : ExpressionVisitor
    {
        private readonly RelationalSqlTranslatingExpressionVisitor _sqlTranslator;

        private SelectExpression _selectExpression;
        private readonly IDictionary<ProjectionMember, Expression> _projectionMapping
            = new Dictionary<ProjectionMember, Expression>();

        private readonly Stack<ProjectionMember> _projectionMembers = new Stack<ProjectionMember>();

        public RelationalProjectionBindingExpressionVisitor(
            RelationalSqlTranslatingExpressionVisitor sqlTranslatingExpressionVisitor)
        {
            _sqlTranslator = sqlTranslatingExpressionVisitor;
        }

        public Expression Translate(SelectExpression selectExpression, Expression expression)
        {
            _selectExpression = selectExpression;

            _projectionMembers.Push(new ProjectionMember());

            var result = Visit(expression);

            _selectExpression.ApplyProjection(_projectionMapping);

            _selectExpression = null;
            _projectionMembers.Clear();
            _projectionMapping.Clear();

            return result;
        }

        public override Expression Visit(Expression expression)
        {
            if (expression == null)
            {
                return null;
            }

            if (!(expression is NewExpression
                  || expression is EntityShaperExpression))
            {

                var translation = _sqlTranslator.Translate(_selectExpression, expression);

                _projectionMapping[_projectionMembers.Peek()] = translation ?? throw new InvalidOperationException();

                return new ProjectionBindingExpression(_selectExpression, _projectionMembers.Peek(), expression.Type);
            }

            return base.Visit(expression);
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is EntityShaperExpression entityShaperExpression)
            {
                _projectionMapping[_projectionMembers.Peek()]
                    = _selectExpression.GetProjectionExpression(
                        entityShaperExpression.ValueBufferExpression.ProjectionMember);

                return new EntityShaperExpression(
                    entityShaperExpression.EntityType,
                    new ProjectionBindingExpression(
                        _selectExpression,
                        _projectionMembers.Peek(),
                        typeof(ValueBuffer)));
            }

            throw new InvalidOperationException();
        }

        protected override Expression VisitNew(NewExpression newExpression)
        {
            var newArguments = new Expression[newExpression.Arguments.Count];
            for (var i = 0; i < newExpression.Arguments.Count; i++)
            {
                // TODO: Members can be null????
                var projectionMember = _projectionMembers.Peek().AddMember(newExpression.Members[i]);
                _projectionMembers.Push(projectionMember);

                newArguments[i] = Visit(newExpression.Arguments[i]);
            }

            return newExpression.Update(newArguments);
        }
    }
}

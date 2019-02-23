// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Relational.Query.PipeLine.SqlExpressions
{
    public class SqlFunctionExpression : SqlExpression
    {
        public SqlFunctionExpression(
            Expression instance,
            string functionName,
            string schema,
            IEnumerable<SqlExpression> arguments,
            Type type,
            RelationalTypeMapping typeMapping,
            bool condition)
            : base(type, typeMapping, condition, !condition)
        {
            Instance = instance;
            FunctionName = functionName;
            Schema = schema;
            Arguments = (arguments ?? Array.Empty<SqlExpression>()).ToList();
        }

        private SqlFunctionExpression(
            Expression instance,
            string functionName,
            string schema,
            IEnumerable<SqlExpression> arguments,
            Type type,
            RelationalTypeMapping typeMapping,
            bool condition,
            bool treatAsValue)
            : base(type, typeMapping, condition, treatAsValue)
        {
            Instance = instance;
            FunctionName = functionName;
            Schema = schema;
            Arguments = (arguments ?? Array.Empty<SqlExpression>()).ToList();
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var changed = false;
            var instance = (SqlExpression)visitor.Visit(Instance);
            changed |= instance != Instance;
            var arguments = new SqlExpression[Arguments.Count];
            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = (SqlExpression)visitor.Visit(Arguments[i]);
                changed |= arguments[i] != Arguments[i];
            }

            return changed
                ? new SqlFunctionExpression(
                    instance,
                    FunctionName,
                    Schema,
                    arguments,
                    Type,
                    TypeMapping,
                    IsCondition,
                    ShouldBeValue)
                : this;
        }

        public override SqlExpression ConvertToValue(bool treatAsValue)
        {
            return new SqlFunctionExpression(
                Instance,
                FunctionName,
                Schema,
                Arguments,
                Type,
                TypeMapping,
                IsCondition,
                treatAsValue);
        }

        public string FunctionName { get; }
        public string Schema { get; }
        public IReadOnlyList<SqlExpression> Arguments { get; }
        public Expression Instance { get; }

        public override bool Equals(object obj)
            => obj != null
            && (ReferenceEquals(this, obj)
                || obj is SqlFunctionExpression sqlFunctionExpression
                    && Equals(sqlFunctionExpression));

        private bool Equals(SqlFunctionExpression sqlFunctionExpression)
            => base.Equals(sqlFunctionExpression)
            && string.Equals(FunctionName, sqlFunctionExpression.FunctionName)
            && string.Equals(Schema, sqlFunctionExpression.Schema)
            && ((Instance == null && sqlFunctionExpression.Instance == null)
                || (Instance != null && Instance.Equals(sqlFunctionExpression.Instance)))
            && Arguments.SequenceEqual(sqlFunctionExpression.Arguments);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ FunctionName.GetHashCode();
                hashCode = (hashCode * 397) ^ (Schema?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Instance?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ Arguments.Aggregate(
                    0, (current, value) => current + ((current * 397) ^ value.GetHashCode()));


                return hashCode;
            }
        }
    }
}

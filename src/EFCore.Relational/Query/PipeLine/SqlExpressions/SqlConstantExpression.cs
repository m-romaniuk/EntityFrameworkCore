// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Relational.Query.PipeLine.SqlExpressions
{
    public class SqlConstantExpression : SqlExpression
    {
        private readonly ConstantExpression _constantExpression;

        public SqlConstantExpression(ConstantExpression constantExpression, RelationalTypeMapping typeMapping)
            : base(constantExpression.Type, typeMapping, false, true)
        {
            _constantExpression = constantExpression;
        }

        private SqlConstantExpression(ConstantExpression constantExpression, RelationalTypeMapping typeMapping, bool treatAsValue)
            : base(constantExpression.Type, typeMapping, false, treatAsValue)
        {
            _constantExpression = constantExpression;
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            return this;
        }

        public override SqlExpression ConvertToValue(bool treatAsValue)
        {
            return new SqlConstantExpression(_constantExpression, TypeMapping, treatAsValue);
        }

        public object Value => _constantExpression.Value;
    }
}

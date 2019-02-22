// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Relational.Query.PipeLine.SqlExpressions
{
    public class SqlNotExpression : SqlExpression
    {
        public SqlNotExpression(
            SqlExpression operand,
            RelationalTypeMapping typeMapping)
            : base(typeof(bool), typeMapping, true, false)
        {
            Check.NotNull(operand, nameof(operand));

            Operand = operand.ConvertToValue(false);
        }

        private SqlNotExpression(
            SqlExpression operand,
            RelationalTypeMapping typeMapping,
            bool treatAsValue)
            : base(typeof(bool), typeMapping, true, treatAsValue)
        {
            Check.NotNull(operand, nameof(operand));

            Operand = operand;
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var operand = (SqlExpression)visitor.Visit(Operand);

            return operand != Operand
                ? new SqlNotExpression(operand, TypeMapping)
                : this;
        }

        public override SqlExpression ConvertToValue(bool treatAsValue)
        {
            return new SqlNotExpression(Operand, TypeMapping, treatAsValue);
        }

        public SqlExpression Operand { get; }
    }
}

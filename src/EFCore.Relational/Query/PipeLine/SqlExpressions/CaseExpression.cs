// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Relational.Query.PipeLine.SqlExpressions
{
    public class CaseExpression : SqlExpression
    {
        public CaseExpression(
            IEnumerable<CaseWhenClause> whenClauses, SqlExpression elseResult,
            Type type, RelationalTypeMapping typeMapping)
            // TODO: actually infer conditional & type
            : base(type, typeMapping, false, true)
        {
            WhenClauses = whenClauses;
            ElseResult = elseResult.ConvertToValue(true);
        }

        private CaseExpression(
            IEnumerable<CaseWhenClause> whenClauses,
            SqlExpression elseResult,
            Type type,
            RelationalTypeMapping typeMapping,
            bool treatAsValue)
            // TODO: actually infer conditional & type
            : base(type, typeMapping, false, treatAsValue)
        {
            WhenClauses = whenClauses;
            ElseResult = elseResult;
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var changed = false;
            var whenClauses = new List<CaseWhenClause>();
            foreach (var whenClause in WhenClauses)
            {
                var test = (SqlExpression)visitor.Visit(whenClause.Test);
                var result = (SqlExpression)visitor.Visit(whenClause.Result);

                if (test != whenClause.Test || result != whenClause.Result)
                {
                    changed = true;
                    whenClauses.Add(new CaseWhenClause(test, result));
                }
            }

            var elseResult = (SqlExpression)visitor.Visit(ElseResult);

            return changed || elseResult != ElseResult
                ? new CaseExpression(whenClauses, elseResult, Type, TypeMapping, ShouldBeValue)
                : this;
        }

        public override SqlExpression ConvertToValue(bool treatAsValue)
        {
            return new CaseExpression(WhenClauses, ElseResult, Type, TypeMapping, treatAsValue);
        }

        public IEnumerable<CaseWhenClause> WhenClauses { get; }
        public SqlExpression ElseResult { get; }

        public override bool Equals(object obj)
            => obj != null
            && (ReferenceEquals(this, obj)
                || obj is CaseExpression caseExpression
                    && Equals(caseExpression));

        private bool Equals(CaseExpression caseExpression)
            => base.Equals(caseExpression)
            && WhenClauses.SequenceEqual(caseExpression.WhenClauses)
            && ElseResult.Equals(caseExpression.ElseResult);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ WhenClauses.Aggregate(
                    0, (current, value) => current + ((current * 397) ^ value.GetHashCode()));
                hashCode = (hashCode * 397) ^ ElseResult.GetHashCode();

                return hashCode;
            }
        }
    }
}

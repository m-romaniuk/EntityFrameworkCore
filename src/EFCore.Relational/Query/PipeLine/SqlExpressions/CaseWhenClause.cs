// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.EntityFrameworkCore.Relational.Query.PipeLine.SqlExpressions
{
    public class CaseWhenClause
    {
        public CaseWhenClause(SqlExpression test, SqlExpression result)
        {
            Test = test.ConvertToValue(false);
            Result = result.ConvertToValue(true);
        }

        public SqlExpression Test { get; }
        public SqlExpression Result { get; }
    }
}

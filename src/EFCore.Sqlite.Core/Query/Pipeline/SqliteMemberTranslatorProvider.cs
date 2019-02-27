// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore.Relational.Query.PipeLine;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Sqlite.Query.Pipeline
{
    public class SqliteMemberTranslatorProvider : RelationalMemberTranslatorProvider
    {
        public SqliteMemberTranslatorProvider(
            IRelationalTypeMappingSource typeMappingSource)
        {
            AddTranslators(
                new IMemberTranslator[]
                {
                    new SqliteDateTimeMemberTranslator(typeMappingSource),
                    new SqliteStringLengthTranslator(typeMappingSource)
                });
        }
    }
}

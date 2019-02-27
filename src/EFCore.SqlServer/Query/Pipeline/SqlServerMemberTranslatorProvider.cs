﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Relational.Query.Pipeline;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.Pipeline
{
    public class SqlServerMemberTranslatorProvider : RelationalMemberTranslatorProvider
    {
        public SqlServerMemberTranslatorProvider(
            IRelationalTypeMappingSource typeMappingSource,
            IEnumerable<IMemberTranslatorPlugin> plugins)
            : base(plugins)
        {
            AddTranslators(
                new IMemberTranslator[] {
                    new SqlServerDateTimeMemberTranslator(typeMappingSource),
                    new SqlServerStringMemberTranslator(typeMappingSource)
                });
        }
    }
}

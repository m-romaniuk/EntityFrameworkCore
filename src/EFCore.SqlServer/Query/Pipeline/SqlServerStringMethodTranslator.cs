// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Relational.Query.PipeLine;
using Microsoft.EntityFrameworkCore.Relational.Query.PipeLine.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.Pipeline
{
    public class SqlServerStringMethodTranslator : IMethodCallTranslator
    {
        private static readonly MethodInfo _indexOfMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), new[] { typeof(string) });

        private static readonly MethodInfo _replaceMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Replace), new[] { typeof(string), typeof(string) });


        private static readonly MethodInfo _toLowerMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.ToLower), Array.Empty<Type>());

        private static readonly MethodInfo _toUpperMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.ToUpper), Array.Empty<Type>());

        private static readonly MethodInfo _substringMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Substring), new[] { typeof(int), typeof(int) });

        private static readonly MethodInfo _startsWithMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.StartsWith), new[] { typeof(string) });

        private static readonly MethodInfo _concat
            = typeof(string).GetRuntimeMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) });

        private readonly IRelationalTypeMappingSource _typeMappingSource;
        private readonly ITypeMappingApplyingExpressionVisitor _typeMappingApplyingExpressionVisitor;
        private readonly RelationalTypeMapping _intTypeMapping;

        public SqlServerStringMethodTranslator(IRelationalTypeMappingSource typeMappingSource,
            ITypeMappingApplyingExpressionVisitor typeMappingApplyingExpressionVisitor)
        {
            _typeMappingSource = typeMappingSource;
            _typeMappingApplyingExpressionVisitor = typeMappingApplyingExpressionVisitor;
            _intTypeMapping = _typeMappingSource.FindMapping(typeof(int));
        }

        public SqlExpression Translate(SqlExpression instance, MethodInfo method, IList<SqlExpression> arguments)
        {
            if (_indexOfMethodInfo.Equals(method))
            {
                var argument = arguments[0];
                var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, argument);
                argument = _typeMappingApplyingExpressionVisitor.ApplyTypeMapping(argument, stringTypeMapping);

                var charIndexExpression =
                    new SqlBinaryExpression(
                        ExpressionType.Subtract,
                        new SqlFunctionExpression(
                            null,
                            "CHARINDEX",
                            null,
                            new[]
                            {
                                _typeMappingApplyingExpressionVisitor.ApplyTypeMapping(instance, stringTypeMapping),
                                argument
                            },
                            method.ReturnType,
                            stringTypeMapping,
                            false),
                        MakeSqlConstant(1),
                        method.ReturnType,
                        _intTypeMapping);

                return new CaseExpression(
                    new[]
                    {
                        new CaseWhenClause(
                            new SqlBinaryExpression(
                                ExpressionType.Equal,
                                argument,
                                new SqlConstantExpression(Expression.Constant(string.Empty), stringTypeMapping),
                                typeof(bool),
                                _typeMappingSource.FindMapping(typeof(bool))),
                            MakeSqlConstant(0))
                    },
                    charIndexExpression,
                    charIndexExpression.Type,
                    charIndexExpression.TypeMapping);
            }

            if (_replaceMethodInfo.Equals(method))
            {
                var firstArgument = arguments[0];
                var secondArgument = arguments[1];
                var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, firstArgument, secondArgument);

                instance = _typeMappingApplyingExpressionVisitor.ApplyTypeMapping(instance, stringTypeMapping);
                firstArgument = _typeMappingApplyingExpressionVisitor.ApplyTypeMapping(firstArgument, stringTypeMapping);
                secondArgument = _typeMappingApplyingExpressionVisitor.ApplyTypeMapping(secondArgument, stringTypeMapping);

                return new SqlFunctionExpression(
                    null,
                    "REPLACE",
                    null,
                    new[]
                    {
                        instance,
                        firstArgument,
                        secondArgument
                    },
                    method.ReturnType,
                    stringTypeMapping,
                    false);
            }

            if (_toLowerMethodInfo.Equals(method)
                || _toUpperMethodInfo.Equals(method))
            {
                return new SqlFunctionExpression(
                    null,
                    _toLowerMethodInfo.Equals(method) ? "LOWER" : "UPPER",
                    null,
                    new[] { instance },
                    method.ReturnType,
                    instance.TypeMapping,
                    false);
            }

            if (_substringMethodInfo.Equals(method))
            {
                return new SqlFunctionExpression(
                    null,
                    "SUBSTRING",
                    null,
                    new[]
                    {
                        instance,
                        new SqlBinaryExpression(
                            ExpressionType.Add,
                            _typeMappingApplyingExpressionVisitor.ApplyTypeMapping(arguments[0], _intTypeMapping),
                            MakeSqlConstant(1),
                            typeof(int),
                            _intTypeMapping),
                        _typeMappingApplyingExpressionVisitor.ApplyTypeMapping(arguments[1], _intTypeMapping),
                    },
                    method.ReturnType,
                    instance.TypeMapping,
                    false);
            }

            if (_startsWithMethodInfo.Equals(method))
            {

            }

            return null;
        }

        private SqlExpression MakeSqlConstant(int value)
        {
            return new SqlConstantExpression(Expression.Constant(value), _intTypeMapping);
        }
    }
}

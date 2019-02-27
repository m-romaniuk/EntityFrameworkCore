﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.Pipeline
{
    public abstract class QueryableMethodTranslatingExpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression extensionExpression)
        {
            if (extensionExpression is ShapedQueryExpression)
            {
                return extensionExpression;
            }

            return base.VisitExtension(extensionExpression);
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Method.DeclaringType == typeof(Queryable))
            {
                var source = Visit(methodCallExpression.Arguments[0]);
                if (source is ShapedQueryExpression shapedQueryExpression)
                {
                    var argumentCount = methodCallExpression.Arguments.Count;
                    switch (methodCallExpression.Method.Name)
                    {
                        case nameof(Queryable.Aggregate):
                            // Don't know
                            break;

                        case nameof(Queryable.All):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateAll(
                                shapedQueryExpression,
                                UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]));

                        case nameof(Queryable.Any):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateAny(
                                shapedQueryExpression,
                                methodCallExpression.Arguments.Count == 2
                                ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                : null);

                        case nameof(Queryable.AsQueryable):
                            // Don't know
                            break;

                        case nameof(Queryable.Average):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateAverage(
                               shapedQueryExpression,
                               methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                               methodCallExpression.Type);

                        case nameof(Queryable.Cast):
                            return TranslateCast(shapedQueryExpression, methodCallExpression.Method.GetGenericArguments()[0]);

                        case nameof(Queryable.Concat):
                            {
                                var source2 = Visit(methodCallExpression.Arguments[1]);
                                if (source2 is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return TranslateConcat(
                                        shapedQueryExpression,
                                        innerShapedQueryExpression);
                                }
                            }

                            break;

                        case nameof(Queryable.Contains)
                            when argumentCount == 2:
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateContains(shapedQueryExpression, methodCallExpression.Arguments[1]);

                        case nameof(Queryable.Count):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateCount(
                                shapedQueryExpression,
                                methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null);

                        case nameof(Queryable.DefaultIfEmpty):
                            return TranslateDefaultIfEmpty(
                                shapedQueryExpression,
                                methodCallExpression.Arguments.Count == 2
                                    ? methodCallExpression.Arguments[1]
                                    : null);

                        case nameof(Queryable.Distinct)
                        when argumentCount == 1:
                            return TranslateDistinct(shapedQueryExpression);

                        case nameof(Queryable.ElementAt):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateElementAtOrDefault(shapedQueryExpression, methodCallExpression.Arguments[1], false);

                        case nameof(Queryable.ElementAtOrDefault):
                            shapedQueryExpression.ResultType = ResultType.SingleWithDefault;
                            return TranslateElementAtOrDefault(shapedQueryExpression, methodCallExpression.Arguments[1], true);

                        case nameof(Queryable.Except)
                        when argumentCount == 2:
                            {
                                var source2 = Visit(methodCallExpression.Arguments[1]);
                                if (source2 is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return TranslateExcept(
                                        shapedQueryExpression,
                                        innerShapedQueryExpression);
                                }
                            }

                            break;

                        case nameof(Queryable.First):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateFirstOrDefault(
                                shapedQueryExpression,
                                methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                                false);

                        case nameof(Queryable.FirstOrDefault):
                            shapedQueryExpression.ResultType = ResultType.SingleWithDefault;
                            return TranslateFirstOrDefault(
                                shapedQueryExpression,
                                methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                                true);

                        case nameof(Queryable.GroupBy):
                            {
                                var keySelector = UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]);
                                if (methodCallExpression.Arguments[argumentCount - 1] is ConstantExpression)
                                {
                                    // This means last argument is EqualityComparer on key
                                    // which is not supported
                                    break;
                                }

                                switch (argumentCount)
                                {
                                    case 2:
                                        return TranslateGroupBy(
                                            shapedQueryExpression,
                                            keySelector,
                                            null,
                                            null);

                                    case 3:
                                        var lambda = UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[2]);
                                        if (lambda.Parameters.Count == 1)
                                        {
                                            return TranslateGroupBy(
                                                shapedQueryExpression,
                                                keySelector,
                                                lambda,
                                                null);
                                        }
                                        else
                                        {
                                            return TranslateGroupBy(
                                                shapedQueryExpression,
                                                keySelector,
                                                null,
                                                lambda);
                                        }

                                    case 4:
                                        return TranslateGroupBy(
                                            shapedQueryExpression,
                                            keySelector,
                                            UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[2]),
                                            UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[3]));
                                }
                            }

                            break;

                        case nameof(Queryable.GroupJoin)
                        when argumentCount == 5:
                            {
                                var innerSource = Visit(methodCallExpression.Arguments[1]);
                                if (innerSource is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return TranslateGroupJoin(
                                        shapedQueryExpression,
                                        innerShapedQueryExpression,
                                        UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[2]),
                                        UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[3]),
                                        UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[4]));
                                }
                            }

                            break;

                        case nameof(Queryable.Intersect)
                        when argumentCount == 2:
                            {
                                var source2 = Visit(methodCallExpression.Arguments[1]);
                                if (source2 is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return TranslateIntersect(
                                        shapedQueryExpression,
                                        innerShapedQueryExpression);
                                }
                            }

                            break;

                        case nameof(Queryable.Join)
                        when argumentCount == 5:
                            {
                                var innerSource = Visit(methodCallExpression.Arguments[1]);
                                if (innerSource is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return TranslateJoin(
                                        shapedQueryExpression,
                                        innerShapedQueryExpression,
                                        UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[2]),
                                        UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[3]),
                                        UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[4]));
                                }
                            }

                            break;

                        case nameof(Queryable.Last):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateLastOrDefault(
                                shapedQueryExpression,
                                methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                                false);

                        case nameof(Queryable.LastOrDefault):
                            shapedQueryExpression.ResultType = ResultType.SingleWithDefault;
                            return TranslateLastOrDefault(
                                shapedQueryExpression,
                                methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                                true);

                        case nameof(Queryable.LongCount):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateLongCount(
                               shapedQueryExpression,
                               methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null);

                        case nameof(Queryable.Max):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateMax(
                               shapedQueryExpression,
                               methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                               methodCallExpression.Type);

                        case nameof(Queryable.Min):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateMin(
                               shapedQueryExpression,
                               methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                               methodCallExpression.Type);

                        case nameof(Queryable.OfType):
                            return TranslateOfType(shapedQueryExpression, methodCallExpression.Method.GetGenericArguments()[0]);

                        case nameof(Queryable.OrderBy)
                        when argumentCount == 2:
                            return TranslateOrderBy(
                                shapedQueryExpression,
                                UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]),
                                true);

                        case nameof(Queryable.OrderByDescending)
                        when argumentCount == 2:
                            return TranslateOrderBy(
                                shapedQueryExpression,
                                UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]),
                                false);

                        case nameof(Queryable.Reverse):
                            return TranslateReverse(shapedQueryExpression);

                        case nameof(Queryable.Select):
                            return TranslateSelect(
                                shapedQueryExpression,
                                UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]));

                        case nameof(Queryable.SelectMany):
                            return methodCallExpression.Arguments.Count == 2
                                ? TranslateSelectMany(
                                    shapedQueryExpression,
                                    UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]))
                                : TranslateSelectMany(
                                    shapedQueryExpression,
                                    UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]),
                                    UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[2]));

                        case nameof(Queryable.SequenceEqual):
                            // don't know
                            break;

                        case nameof(Queryable.Single):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateSingleOrDefault(
                                shapedQueryExpression,
                                methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                                false);

                        case nameof(Queryable.SingleOrDefault):
                            shapedQueryExpression.ResultType = ResultType.SingleWithDefault;
                            return TranslateSingleOrDefault(
                                shapedQueryExpression,
                                methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                                true);

                        case nameof(Queryable.Skip):
                            return TranslateSkip(shapedQueryExpression, methodCallExpression.Arguments[1]);

                        case nameof(Queryable.SkipWhile):
                            return TranslateSkipWhile(
                                shapedQueryExpression,
                                UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]));

                        case nameof(Queryable.Sum):
                            shapedQueryExpression.ResultType = ResultType.Single;
                            return TranslateSum(
                               shapedQueryExpression,
                               methodCallExpression.Arguments.Count == 2
                                   ? UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1])
                                   : null,
                               methodCallExpression.Type);

                        case nameof(Queryable.Take):
                            return TranslateTake(shapedQueryExpression, methodCallExpression.Arguments[1]);

                        case nameof(Queryable.TakeWhile):
                            return TranslateTakeWhile(
                                shapedQueryExpression,
                                UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]));

                        case nameof(Queryable.ThenBy)
                        when argumentCount == 2:
                            return TranslateThenBy(
                                shapedQueryExpression,
                                UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]),
                                true);

                        case nameof(Queryable.ThenByDescending)
                        when argumentCount == 2:
                            return TranslateThenBy(
                                shapedQueryExpression,
                                UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]),
                                false);

                        case nameof(Queryable.Union)
                        when argumentCount == 2:
                            {
                                var source2 = Visit(methodCallExpression.Arguments[1]);
                                if (source2 is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return TranslateUnion(
                                        shapedQueryExpression,
                                        innerShapedQueryExpression);
                                }
                            }

                            break;

                        case nameof(Queryable.Where):
                            return TranslateWhere(
                                shapedQueryExpression,
                                UnwrapLambdaFromQuoteExpression(methodCallExpression.Arguments[1]));

                        case nameof(Queryable.Zip):
                            // Don't know
                            break;
                    }
                }

                throw new NotImplementedException();
            }

            return base.VisitMethodCall(methodCallExpression);
        }

        private LambdaExpression UnwrapLambdaFromQuoteExpression(Expression expression)
            => (LambdaExpression)((UnaryExpression)expression).Operand;

        protected abstract ShapedQueryExpression TranslateAll(ShapedQueryExpression source, LambdaExpression predicate);
        protected abstract ShapedQueryExpression TranslateAny(ShapedQueryExpression source, LambdaExpression predicate);
        protected abstract ShapedQueryExpression TranslateAverage(ShapedQueryExpression source, LambdaExpression selector, Type resultType);
        protected abstract ShapedQueryExpression TranslateCast(ShapedQueryExpression source, Type resultType);
        protected abstract ShapedQueryExpression TranslateConcat(ShapedQueryExpression source1, ShapedQueryExpression source2);
        protected abstract ShapedQueryExpression TranslateContains(ShapedQueryExpression source, Expression item);
        protected abstract ShapedQueryExpression TranslateCount(ShapedQueryExpression source, LambdaExpression predicate);
        protected abstract ShapedQueryExpression TranslateDefaultIfEmpty(ShapedQueryExpression source, Expression defaultValue);
        protected abstract ShapedQueryExpression TranslateDistinct(ShapedQueryExpression source);
        protected abstract ShapedQueryExpression TranslateElementAtOrDefault(ShapedQueryExpression source, Expression index, bool returnDefault);
        protected abstract ShapedQueryExpression TranslateExcept(ShapedQueryExpression source1, ShapedQueryExpression source2);
        protected abstract ShapedQueryExpression TranslateFirstOrDefault(ShapedQueryExpression source, LambdaExpression predicate, bool returnDefault);
        protected abstract ShapedQueryExpression TranslateGroupBy(ShapedQueryExpression source, LambdaExpression keySelector, LambdaExpression elementSelector, LambdaExpression resultSelector);
        protected abstract ShapedQueryExpression TranslateGroupJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector);
        protected abstract ShapedQueryExpression TranslateIntersect(ShapedQueryExpression source1, ShapedQueryExpression source2);
        protected abstract ShapedQueryExpression TranslateJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector);
        protected abstract ShapedQueryExpression TranslateLastOrDefault(ShapedQueryExpression source, LambdaExpression predicate, bool returnDefault);
        protected abstract ShapedQueryExpression TranslateLongCount(ShapedQueryExpression source, LambdaExpression predicate);
        protected abstract ShapedQueryExpression TranslateMax(ShapedQueryExpression source, LambdaExpression selector, Type resultType);
        protected abstract ShapedQueryExpression TranslateMin(ShapedQueryExpression source, LambdaExpression selector, Type resultType);
        protected abstract ShapedQueryExpression TranslateOfType(ShapedQueryExpression source, Type resultType);
        protected abstract ShapedQueryExpression TranslateOrderBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending);
        protected abstract ShapedQueryExpression TranslateReverse(ShapedQueryExpression source);
        protected abstract ShapedQueryExpression TranslateSelect(ShapedQueryExpression source, LambdaExpression selector);
        protected abstract ShapedQueryExpression TranslateSelectMany(ShapedQueryExpression source, LambdaExpression collectionSelector, LambdaExpression resultSelector);
        protected abstract ShapedQueryExpression TranslateSelectMany(ShapedQueryExpression source, LambdaExpression selector);
        protected abstract ShapedQueryExpression TranslateSingleOrDefault(ShapedQueryExpression source, LambdaExpression predicate, bool returnDefault);
        protected abstract ShapedQueryExpression TranslateSkip(ShapedQueryExpression source, Expression count);
        protected abstract ShapedQueryExpression TranslateSkipWhile(ShapedQueryExpression source, LambdaExpression predicate);
        protected abstract ShapedQueryExpression TranslateSum(ShapedQueryExpression source, LambdaExpression selector, Type resultType);
        protected abstract ShapedQueryExpression TranslateTake(ShapedQueryExpression source, Expression count);
        protected abstract ShapedQueryExpression TranslateTakeWhile(ShapedQueryExpression source, LambdaExpression predicate);
        protected abstract ShapedQueryExpression TranslateThenBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending);
        protected abstract ShapedQueryExpression TranslateUnion(ShapedQueryExpression source1, ShapedQueryExpression source2);
        protected abstract ShapedQueryExpression TranslateWhere(ShapedQueryExpression source, LambdaExpression predicate);

    }

}

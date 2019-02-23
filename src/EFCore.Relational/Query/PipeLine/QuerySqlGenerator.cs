﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Relational.Query.PipeLine.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Relational.Query.PipeLine
{
    public class QuerySqlGenerator : SqlExpressionVisitor
    {
        private readonly IRelationalCommandBuilderFactory _relationalCommandBuilderFactory;
        private readonly ISqlGenerationHelper _sqlGenerationHelper;
        private IRelationalCommandBuilder _relationalCommandBuilder;
        private IReadOnlyDictionary<string, object> _parametersValues;
        //private ParameterNameGenerator _parameterNameGenerator;

        private static readonly Dictionary<ExpressionType, string> _operatorMap = new Dictionary<ExpressionType, string>
        {
            { ExpressionType.Equal, " = " },
            { ExpressionType.NotEqual, " <> " },
            { ExpressionType.GreaterThan, " > " },
            { ExpressionType.GreaterThanOrEqual, " >= " },
            { ExpressionType.LessThan, " < " },
            { ExpressionType.LessThanOrEqual, " <= " },
            { ExpressionType.AndAlso, " AND " },
            { ExpressionType.OrElse, " OR " },
            { ExpressionType.Add, " + " },
            { ExpressionType.Subtract, " - " },
            { ExpressionType.Multiply, " * " },
            { ExpressionType.Divide, " / " },
            { ExpressionType.Modulo, " % " },
            { ExpressionType.And, " & " },
            { ExpressionType.Or, " | " }
        };

        public QuerySqlGenerator(IRelationalCommandBuilderFactory relationalCommandBuilderFactory,
            ISqlGenerationHelper sqlGenerationHelper)
        {
            _relationalCommandBuilderFactory = relationalCommandBuilderFactory;
            _sqlGenerationHelper = sqlGenerationHelper;
        }

        public virtual IRelationalCommand GetCommand(
            SelectExpression selectExpression,
            IReadOnlyDictionary<string, object> parameterValues,
            IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger)
        {
            _relationalCommandBuilder = _relationalCommandBuilderFactory.Create(commandLogger);

            //_parameterNameGenerator = Dependencies.ParameterNameGeneratorFactory.Create();

            _parametersValues = parameterValues;

            VisitSelect(selectExpression);

            return _relationalCommandBuilder.Build();
        }

        protected override Expression VisitSqlFragment(SqlFragmentExpression sqlFragmentExpression)
        {
            _relationalCommandBuilder.Append(sqlFragmentExpression.Sql);

            return sqlFragmentExpression;
        }

        protected override Expression VisitSelect(SelectExpression selectExpression)
        {
            _relationalCommandBuilder.Append("SELECT ");

            if (selectExpression.Limit != null
                && selectExpression.Offset == null)
            {
                _relationalCommandBuilder.Append("TOP(");

                Visit(selectExpression.Limit);

                _relationalCommandBuilder.Append(") ");
            }

            if (selectExpression.IsDistinct)
            {
                _relationalCommandBuilder.Append("DISTINCT ");
            }

            if (selectExpression.Projection.Any())
            {
                GenerateList(selectExpression.Projection, e => Visit(e));
            }
            else
            {
                _relationalCommandBuilder.Append("1");
            }

            if (selectExpression.Tables.Any())
            {
                _relationalCommandBuilder.AppendLine()
                    .Append("FROM ");

                GenerateList(selectExpression.Tables, e => Visit(e), sql => sql.AppendLine());
            }

            if (selectExpression.Predicate != null)
            {
                _relationalCommandBuilder.AppendLine()
                    .Append("WHERE ");

                Visit(selectExpression.Predicate);
            }

            if (selectExpression.Orderings.Any())
            {
                _relationalCommandBuilder.AppendLine()
                    .Append("ORDER BY ");

                GenerateList(selectExpression.Orderings, e => Visit(e));
            }

            if (selectExpression.Offset != null)
            {
                _relationalCommandBuilder.AppendLine()
                    .Append("OFFSET ");

                Visit(selectExpression.Offset);

                _relationalCommandBuilder.Append(" ROWS");

                if (selectExpression.Limit != null)
                {
                    _relationalCommandBuilder.Append(" FETCH NEXT ");

                    Visit(selectExpression.Limit);

                    _relationalCommandBuilder.Append(" ROWS ONLY");
                }
            }

            return selectExpression;
        }


        protected override Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression)
        {
            if (sqlFunctionExpression.Schema != null)
            {
                _relationalCommandBuilder
                    .Append(_sqlGenerationHelper.DelimitIdentifier(sqlFunctionExpression.Schema))
                    .Append(".")
                    .Append(_sqlGenerationHelper.DelimitIdentifier(sqlFunctionExpression.FunctionName));
            }
            else
            {
                _relationalCommandBuilder.Append(sqlFunctionExpression.FunctionName);
            }

            _relationalCommandBuilder.Append("(");

            GenerateList(sqlFunctionExpression.Arguments, e => Visit(e));

            _relationalCommandBuilder.Append(")");

            return sqlFunctionExpression;
        }

        protected override Expression VisitColumn(ColumnExpression columnExpression)
        {
            _relationalCommandBuilder
                .Append(_sqlGenerationHelper.DelimitIdentifier(columnExpression.Table.Alias))
                .Append(".")
                .Append(_sqlGenerationHelper.DelimitIdentifier(columnExpression.Name));

            return columnExpression;
        }

        protected override Expression VisitTable(TableExpression tableExpression)
        {
            _relationalCommandBuilder
                .Append(_sqlGenerationHelper.DelimitIdentifier(tableExpression.Table, tableExpression.Schema))
                .Append(" AS ")
                .Append(_sqlGenerationHelper.DelimitIdentifier(tableExpression.Alias));

            return tableExpression;
        }

        protected override Expression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression)
        {
            if (sqlBinaryExpression.OperatorType == ExpressionType.Coalesce)
            {
                    _relationalCommandBuilder.Append("COALESCE(");
                    Visit(sqlBinaryExpression.Left);
                    _relationalCommandBuilder.Append(", ");
                    Visit(sqlBinaryExpression.Right);
                    _relationalCommandBuilder.Append(")");
            }
            else
            {
                var needsParenthesis = RequiresBrackets(sqlBinaryExpression.Left);

                if (needsParenthesis)
                {
                    _relationalCommandBuilder.Append("(");
                }

                Visit(sqlBinaryExpression.Left);

                if (needsParenthesis)
                {
                    _relationalCommandBuilder.Append(")");
                }

                _relationalCommandBuilder.Append(_operatorMap[sqlBinaryExpression.OperatorType]);

                needsParenthesis = RequiresBrackets(sqlBinaryExpression.Right);

                if (needsParenthesis)
                {
                    _relationalCommandBuilder.Append("(");
                }

                Visit(sqlBinaryExpression.Right);

                if (needsParenthesis)
                {
                    _relationalCommandBuilder.Append(")");
                }


            }

            return sqlBinaryExpression;
        }

        private bool RequiresBrackets(SqlExpression expression)
        {
            return expression is SqlBinaryExpression sqlBinary
                && sqlBinary.OperatorType != ExpressionType.Coalesce;
        }

        protected override Expression VisitSqlConstant(SqlConstantExpression sqlConstantExpression)
        {
            _relationalCommandBuilder
                .Append(sqlConstantExpression.TypeMapping.GenerateSqlLiteral(sqlConstantExpression.Value));

            return sqlConstantExpression;
        }

        protected override Expression VisitSqlParameter(SqlParameterExpression sqlParameterExpression)
        {
            var parameterNameInCommand = _sqlGenerationHelper.GenerateParameterName(sqlParameterExpression.Name);

            if (_relationalCommandBuilder.ParameterBuilder.Parameters
                .All(p => p.InvariantName != sqlParameterExpression.Name))
            {
                _relationalCommandBuilder.AddParameter(
                    sqlParameterExpression.Name,
                    parameterNameInCommand,
                    sqlParameterExpression.TypeMapping,
                    sqlParameterExpression.Type.IsNullableType());
            }

            _relationalCommandBuilder
                .Append(_sqlGenerationHelper.GenerateParameterNamePlaceholder(sqlParameterExpression.Name));

            return sqlParameterExpression;
        }

        protected override Expression VisitOrdering(OrderingExpression orderingExpression)
        {
            Visit(orderingExpression.Expression);

            if (!orderingExpression.Ascending)
            {
                _relationalCommandBuilder.Append(" DESC");
            }

            return orderingExpression;
        }

        private void GenerateList<T>(
            IReadOnlyList<T> items,
            Action<T> generationAction,
            Action<IRelationalCommandBuilder> joinAction = null)
        {
            joinAction = joinAction ?? (isb => isb.Append(", "));

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    joinAction(_relationalCommandBuilder);
                }

                generationAction(items[i]);
            }
        }

        protected override Expression VisitLike(LikeExpression likeExpression)
        {
            Visit(likeExpression.Match);
            _relationalCommandBuilder.Append(" LIKE ");
            Visit(likeExpression.Pattern);

            if (likeExpression.EscapeChar != null)
            {
                _relationalCommandBuilder.Append(" ESCAPE ");
                Visit(likeExpression.EscapeChar);
            }

            return likeExpression;
        }

        protected override Expression VisitCase(CaseExpression caseExpression)
        {
            _relationalCommandBuilder.Append("CASE");

            //if (caseExpression.Operand != null)
            //{
            //    _relationalCommandBuilder.Append(" ");
            //    Visit(caseExpression.Operand);
            //}

            using (_relationalCommandBuilder.Indent())
            {
                foreach (var whenClause in caseExpression.WhenClauses)
                {
                    _relationalCommandBuilder
                        .AppendLine()
                        .Append("WHEN ");
                    Visit(whenClause.Test);
                    _relationalCommandBuilder.Append(" THEN ");
                    Visit(whenClause.Result);
                }

                if (caseExpression.ElseResult != null)
                {
                    _relationalCommandBuilder
                        .AppendLine()
                        .Append("ELSE ");
                    Visit(caseExpression.ElseResult);
                }
            }

            _relationalCommandBuilder
                .AppendLine()
                .Append("END");

            return caseExpression;
        }

        protected override Expression VisitSqlCast(SqlCastExpression sqlCastExpression)
        {
            _relationalCommandBuilder.Append("CAST(");
            Visit(sqlCastExpression.Operand);
            _relationalCommandBuilder.Append(" AS ");
            _relationalCommandBuilder.Append(sqlCastExpression.TypeMapping.StoreType);
            _relationalCommandBuilder.Append(")");

            return sqlCastExpression;
        }

        protected override Expression VisitSqlNot(SqlNotExpression sqlNotExpression)
        {
            _relationalCommandBuilder.Append("NOT (");
            Visit(sqlNotExpression.Operand);
            _relationalCommandBuilder.Append(")");

            return sqlNotExpression;
        }

        protected override Expression VisitSqlNull(SqlNullExpression sqlNullExpression)
        {
            Visit(sqlNullExpression.Operand);
            _relationalCommandBuilder.Append(sqlNullExpression.Negated ? " IS NOT NULL" : " IS NULL");

            return sqlNullExpression;
        }

        protected override Expression VisitExists(ExistsExpression existsExpression)
        {
            if (existsExpression.Negated)
            {
                _relationalCommandBuilder.Append("NOT ");
            }

            _relationalCommandBuilder.AppendLine("EXISTS (");

            using (_relationalCommandBuilder.Indent())
            {
                Visit(existsExpression.Subquery);
            }

            _relationalCommandBuilder.Append(")");

            return existsExpression;
        }
    }
}

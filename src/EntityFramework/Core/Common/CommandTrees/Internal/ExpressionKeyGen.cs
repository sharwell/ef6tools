﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace System.Data.Entity.Core.Common.CommandTrees.Internal
{
    using System.Collections.Generic;
    using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Data.Entity.Resources;
    using System.Data.Entity.Spatial;
    using System.Data.Entity.Utilities;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    // <summary>
    // Generates a key for a command tree.
    // </summary>
    [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    internal sealed class ExpressionKeyGen : DbExpressionVisitor
    {
        internal static bool TryGenerateKey(DbExpression tree, out string key)
        {
            var keyGen = new ExpressionKeyGen();
            try
            {
                tree.Accept(keyGen);
                key = keyGen._key.ToString();
                return true;
            }
            catch (NotSupportedException)
            {
                key = null;
                return false;
            }
        }

        internal ExpressionKeyGen()
        {
        }

        #region Fields

        private readonly StringBuilder _key = new StringBuilder();

        private static readonly string[] _exprKindNames = InitializeExprKindNames();

        private static string[] InitializeExprKindNames()
        {
#if DEBUG
            var values = Enum.GetValues(typeof(DbExpressionKind)).Cast<int>().ToArray();
            for (var i = 0; i < values.Length; ++i)
            {
                // If there are gaps, then we need to change the algorithm for building _exprKindNames.
                Debug.Assert(i == values[i], "Are there any gaps in DbExpressionKind members?");
            }
#endif
            var names = Enum.GetNames(typeof(DbExpressionKind));

            // Arithmetic
            names[(int)DbExpressionKind.Divide] = "/";
            names[(int)DbExpressionKind.Modulo] = "%";
            names[(int)DbExpressionKind.Multiply] = "*";
            names[(int)DbExpressionKind.Plus] = "+";
            names[(int)DbExpressionKind.Minus] = "-";
            names[(int)DbExpressionKind.UnaryMinus] = "-";

            // Comparison
            names[(int)DbExpressionKind.Equals] = "=";
            names[(int)DbExpressionKind.LessThan] = "<";
            names[(int)DbExpressionKind.LessThanOrEquals] = "<=";
            names[(int)DbExpressionKind.GreaterThan] = ">";
            names[(int)DbExpressionKind.GreaterThanOrEquals] = ">=";
            names[(int)DbExpressionKind.NotEquals] = "<>";

            names[(int)DbExpressionKind.Property] = ".";

            // Relops
            names[(int)DbExpressionKind.InnerJoin] = "IJ";
            names[(int)DbExpressionKind.FullOuterJoin] = "FOJ";
            names[(int)DbExpressionKind.LeftOuterJoin] = "LOJ";
            names[(int)DbExpressionKind.CrossApply] = "CA";
            names[(int)DbExpressionKind.OuterApply] = "OA";

            return names;
        }

        #endregion

        internal string Key
        {
            get { return _key.ToString(); }
        }

        private void VisitVariableName(string varName)
        {
#if DEBUG
    // There are generally four sources of var names:
    //      1. generated by default alias generator (DbExpressionBuilder.AliasGenerator): "Var_123"
    //      2. generated by ExpressionConverted.AliasGenerator (ELinq compiler): "LQ123"
    //      3. generated by SemanticResolver.GenerateInternalName (eSQL compiler): "_##hint123"
    //      4. inferred from user-defined artefacts, such as local names introduced inside a linq query
    // Out of these four sources, ##2, 3 and 4 provide stable names in the sense that the same conversion by ExpressionConverted
    // will produce the same variable names for the same linq query. It is assumed that unless there is a code defect, 
    // ELinq queries will contain variables from the stable sources only, so this check is debug only.
            var _notSupportedVarNames = new Regex("^" + DbExpressionBuilder.AliasGenerator.Prefix + "[0-9]+");
            Debug.Assert(
                _notSupportedVarNames.Match(varName).Success == false,
                "ExpressionKeyGen does not support variables generated using default expression builder alias generator.");
#endif
            _key.Append('\'');
            _key.Append(varName.Replace("'", "''"));
            _key.Append('\'');
        }

        private void VisitBinding(DbExpressionBinding binding)
        {
            _key.Append("BV");
            VisitVariableName(binding.VariableName);
            _key.Append("=(");
            binding.Expression.Accept(this);
            _key.Append(')');
        }

        private void VisitGroupBinding(DbGroupExpressionBinding groupBinding)
        {
            _key.Append("GBVV");
            VisitVariableName(groupBinding.VariableName);
            _key.Append(",");
            VisitVariableName(groupBinding.GroupVariableName);
            _key.Append("=(");
            groupBinding.Expression.Accept(this);
            _key.Append(')');
        }

        private void VisitFunction(EdmFunction func, IList<DbExpression> args)
        {
            _key.Append("FUNC<");
            _key.Append(func.Identity);
            _key.Append(">:ARGS(");
            foreach (var a in args)
            {
                _key.Append('(');
                a.Accept(this);
                _key.Append(')');
            }
            _key.Append(')');
        }

        private void VisitExprKind(DbExpressionKind kind)
        {
            _key.Append('[');
            _key.Append(_exprKindNames[(int)kind]);
            _key.Append(']');
        }

        private void VisitUnary(DbUnaryExpression expr)
        {
            VisitExprKind(expr.ExpressionKind);
            _key.Append('(');
            expr.Argument.Accept(this);
            _key.Append(')');
        }

        private void VisitBinary(DbBinaryExpression expr)
        {
            VisitExprKind(expr.ExpressionKind);
            _key.Append('(');
            expr.Left.Accept(this);
            _key.Append(',');
            expr.Right.Accept(this);
            _key.Append(')');
        }

        private void VisitCastOrTreat(DbUnaryExpression e)
        {
            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            e.Argument.Accept(this);
            _key.Append(":");
            _key.Append(e.ResultType.Identity);
            _key.Append(')');
        }

        #region DbExpressionVisitor Members

        public override void Visit(DbExpression e)
        {
            Check.NotNull(e, "e");

            throw new NotSupportedException(Strings.Cqt_General_UnsupportedExpression(e.GetType().FullName));
        }

        public override void Visit(DbConstantExpression e)
        {
            Check.NotNull(e, "e");

            Debug.Assert(TypeSemantics.IsScalarType(e.ResultType), "Non-scalar type constant expressions are not supported.");
            var primitive = TypeHelpers.GetPrimitiveTypeUsageForScalar(e.ResultType);

            switch (((PrimitiveType)primitive.EdmType).PrimitiveTypeKind)
            {
                case PrimitiveTypeKind.Binary:
                    var byteArray = e.Value as byte[];
                    if (byteArray != null)
                    {
                        _key.Append("'");
                        foreach (var b in byteArray)
                        {
                            _key.AppendFormat("{0:X2}", b);
                        }
                        _key.Append("'");
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                    break;
                case PrimitiveTypeKind.String:
                    var @string = e.Value as string;
                    if (@string != null)
                    {
                        _key.Append("'");
                        _key.Append(@string.Replace("'", "''"));
                        _key.Append("'");
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                    break;

                case PrimitiveTypeKind.Boolean:
                case PrimitiveTypeKind.Byte:
                case PrimitiveTypeKind.Decimal:
                case PrimitiveTypeKind.Double:
                case PrimitiveTypeKind.Guid:
                case PrimitiveTypeKind.Single:
                case PrimitiveTypeKind.SByte:
                case PrimitiveTypeKind.Int16:
                case PrimitiveTypeKind.Int32:
                case PrimitiveTypeKind.Int64:
                case PrimitiveTypeKind.Time:
                    _key.AppendFormat(CultureInfo.InvariantCulture, "{0}", e.Value);
                    break;

                case PrimitiveTypeKind.DateTime:
                    _key.Append(((DateTime)e.Value).ToString("o", CultureInfo.InvariantCulture));
                    break;

                case PrimitiveTypeKind.DateTimeOffset:
                    _key.Append(((DateTimeOffset)e.Value).ToString("o", CultureInfo.InvariantCulture));
                    break;

                case PrimitiveTypeKind.Geometry:
                case PrimitiveTypeKind.GeometryPoint:
                case PrimitiveTypeKind.GeometryLineString:
                case PrimitiveTypeKind.GeometryPolygon:
                case PrimitiveTypeKind.GeometryMultiPoint:
                case PrimitiveTypeKind.GeometryMultiLineString:
                case PrimitiveTypeKind.GeometryMultiPolygon:
                case PrimitiveTypeKind.GeometryCollection:
                    var geometry = e.Value as DbGeometry;
                    if (geometry != null)
                    {
                        _key.Append(geometry.AsText());
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                    break;
                case PrimitiveTypeKind.Geography:
                case PrimitiveTypeKind.GeographyPoint:
                case PrimitiveTypeKind.GeographyLineString:
                case PrimitiveTypeKind.GeographyPolygon:
                case PrimitiveTypeKind.GeographyMultiPoint:
                case PrimitiveTypeKind.GeographyMultiLineString:
                case PrimitiveTypeKind.GeographyMultiPolygon:
                case PrimitiveTypeKind.GeographyCollection:
                    var geography = e.Value as DbGeography;
                    if (geography != null)
                    {
                        _key.Append(geography.AsText());
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                    break;

                default:
                    throw new NotSupportedException();
            }

            _key.Append(":");
            _key.Append(e.ResultType.Identity);
        }

        public override void Visit(DbNullExpression e)
        {
            Check.NotNull(e, "e");

            _key.Append("NULL:");
            _key.Append(e.ResultType.Identity);
        }

        public override void Visit(DbVariableReferenceExpression e)
        {
            Check.NotNull(e, "e");

            _key.Append("Var(");
            VisitVariableName(e.VariableName);
            _key.Append(")");
        }

        public override void Visit(DbParameterReferenceExpression e)
        {
            Check.NotNull(e, "e");

            _key.Append("@");
            _key.Append(e.ParameterName);
            _key.Append(":");
            _key.Append(e.ResultType.Identity);
        }

        public override void Visit(DbFunctionExpression e)
        {
            Check.NotNull(e, "e");

            VisitFunction(e.Function, e.Arguments);
        }

        public override void Visit(DbLambdaExpression expression)
        {
            Check.NotNull(expression, "expression");

            _key.Append("Lambda(");
            foreach (var v in expression.Lambda.Variables)
            {
                _key.Append("(V");
                VisitVariableName(v.VariableName);
                _key.Append(":");
                _key.Append(v.ResultType.Identity);
                _key.Append(')');
            }
            _key.Append("=");
            foreach (var a in expression.Arguments)
            {
                _key.Append('(');
                a.Accept(this);
                _key.Append(')');
            }
            _key.Append(")Body(");
            expression.Lambda.Body.Accept(this);
            _key.Append(")");
        }

        public override void Visit(DbPropertyExpression e)
        {
            Check.NotNull(e, "e");

            e.Instance.Accept(this);
            VisitExprKind(e.ExpressionKind);
            _key.Append(e.Property.Name);
        }

        public override void Visit(DbComparisonExpression e)
        {
            Check.NotNull(e, "e");

            VisitBinary(e);
        }

        public override void Visit(DbLikeExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            e.Argument.Accept(this);
            _key.Append(")(");
            e.Pattern.Accept(this);
            _key.Append(")(");
            if (e.Escape != null)
            {
                e.Escape.Accept(this);
            }
            e.Argument.Accept(this);
            _key.Append(')');
        }

        public override void Visit(DbLimitExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            if (e.WithTies)
            {
                _key.Append("WithTies");
            }
            _key.Append('(');
            e.Argument.Accept(this);
            _key.Append(")(");
            e.Limit.Accept(this);
            _key.Append(')');
        }

        public override void Visit(DbIsNullExpression e)
        {
            Check.NotNull(e, "e");

            VisitUnary(e);
        }

        public override void Visit(DbArithmeticExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            foreach (var a in e.Arguments)
            {
                _key.Append('(');
                a.Accept(this);
                _key.Append(')');
            }
        }

        public override void Visit(DbAndExpression e)
        {
            Check.NotNull(e, "e");

            VisitBinary(e);
        }

        public override void Visit(DbOrExpression e)
        {
            Check.NotNull(e, "e");

            VisitBinary(e);
        }

        public override void Visit(DbInExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            e.Item.Accept(this);
            _key.Append(",(");

            var first = true;
            foreach (var item in e.List)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    _key.Append(',');
                }

                item.Accept(this);
            }

            _key.Append("))");
        }

        public override void Visit(DbNotExpression e)
        {
            Check.NotNull(e, "e");

            VisitUnary(e);
        }

        public override void Visit(DbDistinctExpression e)
        {
            Check.NotNull(e, "e");

            VisitUnary(e);
        }

        public override void Visit(DbElementExpression e)
        {
            Check.NotNull(e, "e");

            VisitUnary(e);
        }

        public override void Visit(DbIsEmptyExpression e)
        {
            Check.NotNull(e, "e");

            VisitUnary(e);
        }

        public override void Visit(DbUnionAllExpression e)
        {
            Check.NotNull(e, "e");

            VisitBinary(e);
        }

        public override void Visit(DbIntersectExpression e)
        {
            Check.NotNull(e, "e");

            VisitBinary(e);
        }

        public override void Visit(DbExceptExpression e)
        {
            Check.NotNull(e, "e");

            VisitBinary(e);
        }

        public override void Visit(DbTreatExpression e)
        {
            Check.NotNull(e, "e");

            VisitCastOrTreat(e);
        }

        public override void Visit(DbCastExpression e)
        {
            Check.NotNull(e, "e");

            VisitCastOrTreat(e);
        }

        public override void Visit(DbIsOfExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            e.Argument.Accept(this);
            _key.Append(":");
            _key.Append(e.OfType.EdmType.Identity);
            _key.Append(')');
        }

        public override void Visit(DbOfTypeExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            e.Argument.Accept(this);
            _key.Append(":");
            _key.Append(e.OfType.EdmType.Identity);
            _key.Append(')');
        }

        public override void Visit(DbCaseExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            for (var idx = 0; idx < e.When.Count; idx++)
            {
                _key.Append("WHEN:(");
                e.When[idx].Accept(this);
                _key.Append(")THEN:(");
                e.Then[idx].Accept(this);
            }
            _key.Append("ELSE:(");
            e.Else.Accept(this);
            _key.Append("))");
        }

        public override void Visit(DbNewInstanceExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append(':');
            _key.Append(e.ResultType.EdmType.Identity);
            _key.Append('(');
            foreach (var a in e.Arguments)
            {
                _key.Append('(');
                a.Accept(this);
                _key.Append(')');
            }
            if (e.HasRelatedEntityReferences)
            {
                foreach (var relatedRef in e.RelatedEntityReferences)
                {
                    _key.Append("RE(A(");
                    _key.Append(relatedRef.SourceEnd.DeclaringType.Identity);
                    _key.Append(")(");
                    _key.Append(relatedRef.SourceEnd.Name);
                    _key.Append("->");
                    _key.Append(relatedRef.TargetEnd.Name);
                    _key.Append(")(");
                    relatedRef.TargetEntityReference.Accept(this);
                    _key.Append("))");
                }
            }
            _key.Append(')');
        }

        public override void Visit(DbRefExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append("(ESET(");
            _key.Append(e.EntitySet.EntityContainer.Name);
            _key.Append('.');
            _key.Append(e.EntitySet.Name);
            _key.Append(")T(");
            _key.Append(TypeHelpers.GetEdmType<RefType>(e.ResultType).ElementType.FullName);
            _key.Append(")(");
            e.Argument.Accept(this);
            _key.Append(')');
        }

        public override void Visit(DbRelationshipNavigationExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            e.NavigationSource.Accept(this);
            _key.Append(")A(");
            _key.Append(e.NavigateFrom.DeclaringType.Identity);
            _key.Append(")(");
            _key.Append(e.NavigateFrom.Name);
            _key.Append("->");
            _key.Append(e.NavigateTo.Name);
            _key.Append("))");
        }

        public override void Visit(DbDerefExpression e)
        {
            Check.NotNull(e, "e");

            VisitUnary(e);
        }

        public override void Visit(DbRefKeyExpression e)
        {
            Check.NotNull(e, "e");

            VisitUnary(e);
        }

        public override void Visit(DbEntityRefExpression e)
        {
            Check.NotNull(e, "e");

            VisitUnary(e);
        }

        public override void Visit(DbScanExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            _key.Append(e.Target.EntityContainer.Name);
            _key.Append('.');
            _key.Append(e.Target.Name);
            _key.Append(':');
            _key.Append(e.ResultType.EdmType.Identity);
            _key.Append(')');
        }

        public override void Visit(DbFilterExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            VisitBinding(e.Input);
            _key.Append('(');
            e.Predicate.Accept(this);
            _key.Append("))");
        }

        public override void Visit(DbProjectExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            VisitBinding(e.Input);
            _key.Append('(');
            e.Projection.Accept(this);
            _key.Append("))");
        }

        public override void Visit(DbCrossJoinExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            foreach (var i in e.Inputs)
            {
                VisitBinding(i);
            }
            _key.Append(')');
        }

        public override void Visit(DbJoinExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            VisitBinding(e.Left);
            VisitBinding(e.Right);
            _key.Append('(');
            e.JoinCondition.Accept(this);
            _key.Append("))");
        }

        public override void Visit(DbApplyExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            VisitBinding(e.Input);
            VisitBinding(e.Apply);
            _key.Append(')');
        }

        public override void Visit(DbGroupByExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            VisitGroupBinding(e.Input);
            foreach (var k in e.Keys)
            {
                _key.Append("K(");
                k.Accept(this);
                _key.Append(')');
            }
            foreach (var a in e.Aggregates)
            {
                var ga = a as DbGroupAggregate;
                if (ga != null)
                {
                    _key.Append("GA(");
                    Debug.Assert(ga.Arguments.Count == 1, "Group aggregate must have one argument.");
                    ga.Arguments[0].Accept(this);
                    _key.Append(')');
                }
                else
                {
                    _key.Append("A:");
                    var fa = (DbFunctionAggregate)a;
                    if (fa.Distinct)
                    {
                        _key.Append("D:");
                    }
                    VisitFunction(fa.Function, fa.Arguments);
                }
            }
            _key.Append(')');
        }

        private void VisitSortOrder(IList<DbSortClause> sortOrder)
        {
            _key.Append("SO(");
            foreach (var clause in sortOrder)
            {
                _key.Append(clause.Ascending ? "ASC(" : "DESC(");
                clause.Expression.Accept(this);
                _key.Append(')');
                if (!String.IsNullOrEmpty(clause.Collation))
                {
                    _key.Append(":(");
                    _key.Append(clause.Collation);
                    _key.Append(')');
                }
            }
            _key.Append(')');
        }

        public override void Visit(DbSkipExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            VisitBinding(e.Input);
            VisitSortOrder(e.SortOrder);
            _key.Append('(');
            e.Count.Accept(this);
            _key.Append("))");
        }

        public override void Visit(DbSortExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            VisitBinding(e.Input);
            VisitSortOrder(e.SortOrder);
            _key.Append(')');
        }

        public override void Visit(DbQuantifierExpression e)
        {
            Check.NotNull(e, "e");

            VisitExprKind(e.ExpressionKind);
            _key.Append('(');
            VisitBinding(e.Input);
            _key.Append('(');
            e.Predicate.Accept(this);
            _key.Append("))");
        }

        #endregion
    }
}

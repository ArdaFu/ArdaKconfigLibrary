//------------------------------------------------------------------------------
//  Copyright(C) FU Pengfei, 2007-2018.
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License along
//  with this program; if not, write to the Free Software Foundation, Inc.,
//  51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
//------------------------------------------------------------------------------
//  Project    : Arda Kconfig Library
//  Description: Expression calculation
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: Expression.cs 1683 2018-01-26 06:55:11Z fupengfei                    $
//------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Arda.ArmDevTool.Kconfig
{
    /// <summary>
    /// Expression
    /// </summary>
    public class Expression
    {
        /// <summary>
        /// Type
        /// </summary>
        public ExpressionType Type;
        /// <summary>
        /// Left calculate data
        /// </summary>
        public ExpressionData Left;
        /// <summary>
        /// Right calculate data
        /// </summary>
        public ExpressionData Right;

        private bool IsCompareToString()
        {
            return (Left != null && Right != null)
                && (Left.Symbol != null && Right.Symbol != null)
                && (Left.Symbol.ValueType == MenuAttributeType.String)
                && (Right.Symbol.ValueType == MenuAttributeType.String);
        }

        /// <summary>
        /// Calculate result
        /// </summary>
        /// <returns></returns>
        public TristateValue Calculate()
        {
            // constant expression
            switch (Type)
            {
                case ExpressionType.N:
                    return TristateValue.N;
                case ExpressionType.M:
                    return TristateValue.M;
                case ExpressionType.Y:
                    return TristateValue.Y;
            }

            // "not" and "none" only use right expression data
            var resultR = Right?.Calculate() ?? TristateValue.N;
            switch (Type)
            {
                case ExpressionType.Not:
                    return 2 - resultR;

                case ExpressionType.None:
                    return resultR;
            }
            // "and" "or" "equal" and "no equal" using both sides of expression data
            var resultL = Left?.Calculate() ?? TristateValue.N;
            switch (Type)
            {

                case ExpressionType.And:
                    return (TristateValue) Math.Min((int) resultL, (int) resultR);

                case ExpressionType.Or:
                    return (TristateValue) Math.Max((int) resultL, (int) resultR);

                case ExpressionType.Equal:
                    if (IsCompareToString())
                        return (Left.Symbol.Value == Right.Symbol.Value)
                            ? TristateValue.Y
                            : TristateValue.N;
                    return (resultL == resultR)
                        ? TristateValue.Y
                        : TristateValue.N;

                case ExpressionType.NoEuqal:
                    if (IsCompareToString())
                        return (Left.Symbol.Value != Right.Symbol.Value)
                            ? TristateValue.Y
                            : TristateValue.N;

                    return (resultL != resultR)
                        ? TristateValue.Y
                        : TristateValue.N;
                default:
                    return TristateValue.N;
            }
        }

        public override string ToString()
        {
            switch (Type)
            {
                case ExpressionType.And:
                    return $"({Left} && {Right})";
                case ExpressionType.Or:
                    return $"({Left} || {Right})";
                case ExpressionType.Equal:
                    return $"({Left} = {Right})";
                case ExpressionType.NoEuqal:
                    return $"({Left} != {Right})";
                case ExpressionType.Not:
                    return $"(! {Right})";

                case ExpressionType.None:
                    return $"({Right})";

                case ExpressionType.N:
                    return "n";
                case ExpressionType.M:
                    return "m";
                case ExpressionType.Y:
                    return "y";
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        #region Converter
        #region regex

        /// <summary>
        /// Find "n" "m" "y"
        /// </summary>
        private static readonly Regex IsTristateRegex = new Regex(@"^[n|m|y]$",
            RegexOptions.Compiled);

        /// <summary>
        /// find a string with quotes mark, e.g. "hello" 'hello'
        /// </summary>
        public static readonly Regex FindStringRegex =
            new Regex(@"\s*(?<!\\)(?<markL>[""'])(?<string>.*?)(?<!\\)(?<markR>[""'])\s*",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// find symbol, which should not be wrapped in "[]" or "{}".
        /// </summary>
        public static readonly Regex FindSymbolRegex =
            new Regex(@"\s*(?<![\[|\{])\b(?<name>\w+)\b(?![\]|\}])\s*",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// find symbol or expression index which is wrapped in "[]" or "{}".
        /// </summary>
        public static readonly Regex FindIndexRegex =
            new Regex(@"\s*(\[(?<symbol>\w+?)\])|(\{(?<expr>\w+?)\})\s*",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// find nest expression which is wrapped in "()", the expression do not include any other "(" or ")"
        /// </summary>
        private static readonly Regex NestExpressionRegex =
            new Regex(@"\((?<expr>[^\(\)]+?)\)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// split atomic expression into left expr/symbol , op and right expr/symbol. 
        /// All expr and symbol should be an index which is wrapped in brackets. e.g. "{n}" or "[n]"
        /// </summary>
        private static readonly Regex RegexAtomicExpression =
            new Regex(
                @"\s*((((?<exprL>\{\d+\}|\[\d+\])\s*)(?<op>!=|=|&&|\|\|))|(?<op>!))\s*(?<exprR>\{\d+\}|\[\d+\])\s*",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        #endregion //regex

        #region constant expression "N", "M", "Y"

        /// <summary>
        /// constant expression "N" = false
        /// </summary>
        private static readonly Expression ExprN = new Expression() { Type = ExpressionType.N };

        /// <summary>
        /// constant expression "M" = module
        /// </summary>
        private static readonly Expression ExprM = new Expression() { Type = ExpressionType.M };

        /// <summary>
        /// constant expression "Y" = true
        /// </summary>
        private static readonly Expression ExprY = new Expression() { Type = ExpressionType.Y };

        #endregion //constant expression "N", "M", "Y"

        private static int GetIndexOfEntry(IReadOnlyList<MenuEntry> list,
            string name, bool isConst = false)
        {
            for (var id = 0; id < list.Count; id++)
            {
                if (list[id].Name == name && isConst == list[id].IsConst)
                    return id;
            }
            return -1;
        }

        private static ExpressionData CreateExpressionData(string src,
            IReadOnlyList<Expression> exprs, IReadOnlyList<MenuEntry> symbols)
        {
            var match = FindIndexRegex.Match(src);

            if (int.TryParse(match.Groups["symbol"].Value, out var symId))
                return new ExpressionData(symbols[symId]);

            if (int.TryParse(match.Groups["expr"].Value, out var exprId))
                return new ExpressionData(exprs[exprId]);

            throw new Exception($"Fail to get symbol or expression index. source = {src}");
        }

        private static string CreateFlatExpression(string src,
            List<Expression> exprs, IReadOnlyList<MenuEntry> symbols)
        {
            var exprStr = src;
            while (RegexAtomicExpression.IsMatch(exprStr))
            {
                var exprStrTemp = RegexAtomicExpression.Replace(exprStr, match =>
                {
                    var expr = new Expression();
                    var opStr = match.Groups["op"].Value;
                    var exprStrL = match.Groups["exprL"].Value;
                    var exprStrR = match.Groups["exprR"].Value;

                    expr.Right = CreateExpressionData(exprStrR, exprs, symbols);

                    switch (opStr)
                    {
                        case "=":
                            expr.Type = ExpressionType.Equal;
                            break;
                        case "!=":
                            expr.Type = ExpressionType.NoEuqal;
                            break;
                        case "!":
                            expr.Type = ExpressionType.Not;
                            break;
                        case "&&":
                            expr.Type = ExpressionType.And;
                            break;
                        case "||":
                            expr.Type = ExpressionType.Or;
                            break;
                        default:
                            throw new Exception(
                                $"Operation type is not supported. Operator = \"{opStr}\"");
                    }

                    if (expr.Type != ExpressionType.Not)
                        expr.Left = CreateExpressionData(exprStrL, exprs, symbols);
                    exprs.Add(expr);
                    return $"{{{exprs.Count - 1}}}";
                });
                exprStr = exprStrTemp;
            }
            return exprStr;
        }

        public static Expression ConvertToExpression(string src, HashSet<MenuEntry> entries,
            EntryLocation location, out List<MenuEntry> dependsOn)
        {
            // using "[number]" to replace all symbols, 
            // using "{number}" to replace nest atomic expression

            dependsOn = null;
            if (string.IsNullOrEmpty(src))
            {
                dependsOn = new List<MenuEntry>();
                return null;
            }
            var symbols = new List<MenuEntry>();
            // three constant expression
            var exprs = new List<Expression>
            {
                ExprN, //id = 0
                ExprM, //id = 1
                ExprY, //id = 2
            };

            try
            {
                // add all string's as constant symbol, and mark as "[id]" in source
                var srcTemp = FindStringRegex.Replace(src, match =>
                {
                    if (match.Groups["markL"].Value != match.Groups["markR"].Value)
                        throw new Exception($"Quotes mark are not in pairs. source = {src}.");

                    var str = match.Groups["string"].Value;
                    var id = GetIndexOfEntry(symbols, str, true);
                    if (id >= 0)
                        return $"[{id}]";

                    var entry = new MenuEntry()
                    {
                        EntryType = MenuEntryType.Config,
                        Value = str,
                        IsConst = true, // constant for strings
                    };
                    entry.Attributes.Add(new MenuAttribute()
                    {
                        AttributeType = MenuAttributeType.ValueType,
                        ExpressionType = MenuAttributeType.String
                    });
                    symbols.Add(entry);

                    return $"[{symbols.Count - 1}]";
                });
                // storage constant symbol count
                var constSymbolCopunt = symbols.Count;

                // add all no-constant symbols, and mark as "[id]" in source
                srcTemp = FindSymbolRegex.Replace(srcTemp, match =>
                {
                    var name = match.Groups["name"].Value;
                    // replace "n" "m" "y" with constant expression "{0}" "{1}" "{2}"
                    if (IsTristateRegex.IsMatch(name))
                    {
                        Enum.TryParse(name, true, out TristateValue type);
                        return $"{{{(int)type}}}";
                    }
                    // if we have add the same symbol, just insert the "[id]"
                    var id = GetIndexOfEntry(symbols, name);
                    if (id >= 0)
                        return $"[{id}]";

                    var entry = entries.FirstOrDefault(menuEntry => menuEntry.Name == name);
                    if (entry == null)
                        throw new Exception($"Entry do not exist, entry name = {name}");
                    symbols.Add(entry);
                    return $"[{symbols.Count - 1}]";
                });

                // string to expression
                // NestExpressionRegex will find atomic expression with "()" at the outside.
                while (NestExpressionRegex.IsMatch(srcTemp))
                {
                    // replace "([n]\{n} op [m]\{m})" to "{x}"
                    srcTemp = NestExpressionRegex.Replace(srcTemp, matchNest =>
                    {
                        var exprStr = matchNest.Groups["expr"].Value;
                        return CreateFlatExpression(exprStr, exprs, symbols);
                    });
                }
                // replace "[n]\{n} op [m]\{m}" to "{x}"
                srcTemp = CreateFlatExpression(srcTemp, exprs, symbols);

                // generate final expression, here the string should only have one "[number]" or one "{number}".
                Expression result = null;
                var matchIndex = FindIndexRegex.Match(srcTemp);

                if (!string.IsNullOrEmpty(matchIndex.Groups["expr"].Value))
                    result = exprs[int.Parse(matchIndex.Groups["expr"].Value)];
                else if (!string.IsNullOrEmpty(matchIndex.Groups["symbol"].Value))
                    // create expression here when string only has one symbol
                    result = new Expression()
                    {
                        Type = ExpressionType.None,
                        Right = new ExpressionData(
                            symbols[int.Parse(matchIndex.Groups["symbol"].Value)])
                    };

                // depends on are the no constant symbols
                dependsOn = symbols.GetRange(constSymbolCopunt,
                    symbols.Count - constSymbolCopunt);
                symbols.Clear();
                exprs.Clear();

                return result;
            }
            catch (Exception ex)
            {
                // if depends on symbol do not exist, set expression as null, and depends on list as empty.
                Console.WriteLine($"{ex.Message}. {location}", Brushes.Red);
                dependsOn = new List<MenuEntry>();
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Expression data
    /// </summary>
    public class ExpressionData
    {
        /// <summary>
        /// data is expression
        /// </summary>
        public Expression Expr;
        /// <summary>
        /// data is menu entry
        /// </summary>
        public MenuEntry Symbol;

        /// <summary>
        /// Calculate expression result
        /// </summary>
        /// <returns></returns>
        public TristateValue Calculate()
        {
            if (Expr != null)
                return Expr.Calculate();
            if ((Symbol == null) || (Symbol.IsConst))
                return TristateValue.N;
            if (Symbol.ValueType != MenuAttributeType.Bool 
                && Symbol.ValueType != MenuAttributeType.Tristate)
                return TristateValue.N;

            return Enum.TryParse<TristateValue>(
                Symbol.Value, true, out var result) 
                ? result 
                : TristateValue.N;
        }

        public override string ToString()
        {
            return Expr?.ToString() ??
                   (Symbol.IsConst ? $"\"{Symbol.Name}\"" : Symbol.Name);
        }

        public ExpressionData()
        {
        }

        public ExpressionData(Expression expr)
        {
            Expr = expr;
        }

        public ExpressionData(MenuEntry symbol)
        {
            Symbol = symbol;
        }
    }
}
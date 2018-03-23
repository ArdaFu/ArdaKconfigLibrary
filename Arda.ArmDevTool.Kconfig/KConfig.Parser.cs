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
//  Description: Parser of Kconfig elements
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: KConfig.Parser.cs 1772 2018-03-23 02:11:49Z arda                     $
//------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Arda.ArmDevTool.Kconfig
{
    /// <summary>
    /// Parser to convert kconfig file into menu entry
    /// </summary>
    internal static class KConfigParser
    {
        #region regex

        /// <summary>
        /// find entry type and value
        /// </summary>
        internal static readonly Regex FindEntryTypeValueRegex =
            new Regex(
                @"^\s*\b((?<key>mainmenu|menu|comment)(\s+(?<mark>""|')(?<val>.*))?\k<mark>)|((?<key>menuconfig|config|choice|if|source)(\s+(?<val>\S.*\S))?)|((?<key>endmenu|endchoice|endif))\s*$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// find entry attribute type and value
        /// </summary>
        internal static readonly Regex FindAttributeTypeValueRegex =
            new Regex(
                @"^\s*(?<key>bool|tristate|string|hex|int|prompt|default|def_bool|def_tristate|depends on|select|imply|visible if|range|help|---help---|option|optional)(\s+(?<val>.*?))?\s*$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// Clear attribute key, remove blank, '-', '_'
        /// </summary>
        internal static readonly Regex ClearAttributeKeyRegex =
            new Regex(@"(?<element>\W)",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// &lt;prompt/symbol/expr&gt; ["if" &lt;expr&gt;]
        /// </summary>
        internal static readonly Regex FindAttributeConditionRegex =
            new Regex(@"^\s*(?<symbol>.*?)(\s+if\s*(?<expr>.*?)\s*)?$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// misc options: "option" &lt;symbol&gt;[=&lt;value&gt;]
        /// </summary>
        internal static readonly Regex FindOptionSymbolValueRegex =
            new Regex(
                @"^\s*(?<key>defconfig_list|modules|env|allnoconfig_y)(\s*=\s*(?<mark>""|')(?<val>\w*?)\k<mark>\s*)?$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// numerical ranges: "range" &lt;symbol&gt; &lt;symbol&gt; ["if" &lt;expr&gt;]
        /// </summary>
        internal static readonly Regex FindNumberRangeRegex =
            new Regex(@"^\s*(?<min>.*?)\s+(?<max>.*?)(\s+if\s+(?<expr>.*?)\s*)?$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// Get the text, where source string is start with "&lt;text&gt;" 
        /// </summary>
        internal static readonly Regex RemoveQuotationMarkRegex =
            new Regex(@"^\s*(?<mark>""|')(?<val>.*?)\k<mark>\s*$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// Get start blank
        /// </summary>
        internal static readonly Regex FindStartBlankRegex =
            new Regex(@"^(?<blank>\s*)",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        internal static readonly Regex RemoveEndBlankRegex =
            new Regex(@"^(?<string>(.|\n)*?)\s*$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        #endregion // regex

        /// <summary>
        /// static direction for storage menu entry type and its parser. 
        /// this do not include parser of source and help.
        /// </summary>
        private static readonly Dictionary<MenuEntryType,
            Func<FileReader, MenuEntry, Task<int>>> ParseFuncDict
            = new Dictionary<MenuEntryType,
                Func<FileReader, MenuEntry, Task<int>>>()
            {
                {MenuEntryType.Config, ParseConfig},
                {MenuEntryType.MenuConfig, ParseConfig},
                {MenuEntryType.Menu, ParseMenu},
                {MenuEntryType.Choice, ParseChoice},
                {MenuEntryType.If, ParseEntryBlock},
                {MenuEntryType.Comment, ParseComment},
                {MenuEntryType.Source, ParseSource},
            };

        /// <summary>
        /// Split entry type and expr/prompt
        /// </summary>
        /// <param name="src">source line</param>
        /// <param name="entryType">entry type</param>
        /// <param name="value">expr or prompt</param>
        /// <returns>isSuccess</returns>
        private static bool GetEntryTypeValue(string src,
            out MenuEntryType entryType, out string value)
        {
            var match = FindEntryTypeValueRegex.Match(src);
            var type = match.Groups["key"].Value;
            value = match.Groups["val"].Value;
            return Enum.TryParse(type, true, out entryType);
        }

        #region Parse attribute method

        /// <summary>
        /// Split attribute type and expr/prompt
        /// </summary>
        /// <param name="src">source line</param>
        /// <param name="entryType">attribute type</param>
        /// <param name="value">expr or prompt</param>
        /// <returns>isSuccess</returns>
        private static bool GetAttributeTypeValue(string src,
            out MenuAttributeType entryType, out string value)
        {
            var match = FindAttributeTypeValueRegex.Match(src);
            // remove blank, '-', '_'
            var type = ClearAttributeKeyRegex.Replace(match.Groups["key"].Value, "");
            value = match.Groups["val"].Value;
            return Enum.TryParse(type, true, out entryType);
        }

        /// <summary>
        /// Split range expr with min, max and condition expr
        /// </summary>
        /// <param name="src">source line</param>
        /// <param name="min">min value string</param>
        /// <param name="max">max value string</param>
        /// <param name="expr">condition expr</param>
        /// <returns>isSuccess</returns>
        private static bool GetRangeValueCodition(string src,
            out string min, out string max, out string expr)
        {
            var match = FindNumberRangeRegex.Match(src);
            min = match.Groups["min"].Value;
            max = match.Groups["max"].Value;
            expr = match.Groups["expr"].Value;
            return match.Success;
        }

        /// <summary>
        /// Split symbol/expr with condition expr.
        /// <para>format: &lt;expression&gt; ["if" &lt;condition&gt;]</para>
        /// </summary>
        /// <param name="src">source line</param>
        /// <param name="symbol">symbol or expr</param>
        /// <param name="expr">condition expr</param>
        /// <returns>isSuccess</returns>
        private static bool GetAttributeSymbolValueCodition(string src,
            out string symbol, out string expr)
        {
            var match = FindAttributeConditionRegex.Match(src);
            symbol = match.Groups["symbol"].Value;
            expr = match.Groups["expr"].Value;
            return match.Success;
        }

        /// <summary>
        /// Split option type and value
        /// <para>format: &lt;type&gt;[=&lt;value&gt;]</para>
        /// </summary>
        /// <param name="src">source line</param>
        /// <param name="optionType">option type</param>
        /// <param name="value">option value, for env option only.</param>
        /// <returns>isSuccess</returns>
        private static bool GetOptionTypeValue(string src,
            out MenuAttributeType optionType, out string value)
        {
            var match = FindOptionSymbolValueRegex.Match(src);
            // remove blank, '-', '_'
            var type = ClearAttributeKeyRegex.Replace(match.Groups["key"].Value, "");
            value = match.Groups["val"].Value;
            return Enum.TryParse(type, true, out optionType);
        }

        /// <summary>
        /// Calculate start blank length
        /// </summary>
        /// <param name="src">source line</param>
        /// <returns>blank length</returns>
        private static int GetStartBlankLength(string src)
        {
            try
            {
                var match = FindStartBlankRegex.Match(src);
                return match.Groups["blank"].Length;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return -1;
            }

        }

        /// <summary>
        /// parse and add attribute to entry. 
        /// <para>prototype is &lt;type&gt; &lt; expr &gt; ["if" &lt; expr &gt;]</para>
        /// </summary>
        /// <param name="inStr">source line</param>
        /// <param name="entry">attribute will add to this entry</param>
        /// <param name="type">attribute type</param>
        /// <param name="sr">for get location</param>
        private static void ParseAttrWithValAndCond(FileReader sr, MenuEntry entry, string inStr,
            MenuAttributeType type)
        {
            // <type> < expression > ["if" < condition >]
            if (!GetAttributeSymbolValueCodition(inStr, out var expression, out var condition))
                throw sr.GenExpWithLocation($"Could not parse \"{type}\" attribute.");
            entry.Attributes.Add(new MenuAttribute
            {
                AttributeType = type,
                SymbolValue = expression,
                Condition = condition,
            });
        }

        /// <summary>
        /// parse and add depends on attribute to entry. link all depends on attribute with "and" mark.
        /// <para>prototype is &lt;type&gt; &lt;expr&gt;</para>
        /// </summary>
        /// <param name="inStr">source line</param>
        /// <param name="entry">attribute will add to this entry</param>
        private static void ParseDependsOn(MenuEntry entry, string inStr)
        {
            try
            {
                if (string.IsNullOrEmpty(inStr))
                    return;
                var attrDependsOn =
                    entry.Attributes.FirstOrDefault(attribute =>
                        attribute.AttributeType == MenuAttributeType.DependsOn);

                if (attrDependsOn != null)
                {
                    attrDependsOn.Condition =
                        $"({attrDependsOn.Condition})&&({inStr})";
                    return;
                }

                entry.Attributes.Add(new MenuAttribute
                {
                    AttributeType = MenuAttributeType.DependsOn,
                    Condition = inStr,
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }

        /// <summary>
        /// parse and add range attribute to entry. 
        /// <para>"range" &lt;symbol&gt; &lt;symbol&gt; ["if" &lt;expr&gt;]</para>
        /// </summary>
        /// <param name="sr">for get location</param>
        /// <param name="entry">attribute will add to this entry</param>
        /// <param name="inStr">source line</param>
        private static void ParseAttrRange(FileReader sr, MenuEntry entry, string inStr)
        {
            // numerical ranges: "range" <symbol> <symbol> ["if" <expr>]
            if (!GetRangeValueCodition(inStr, out var min, out var max, out var rangeCond))
                throw sr.GenExpWithLocation("Could not parse \"Range\" attribute.");
            entry.Attributes.Add(new MenuAttribute
            {
                AttributeType = MenuAttributeType.Range,
                SymbolValue = $"{min},{max}",
                Condition = rangeCond,
            });
        }

        /// <summary>
        /// parse and add help attribute to entry
        /// </summary>
        /// <param name="sr">for source line and location</param>
        /// <param name="entry">attribute will add to this entry</param>
        /// <returns>0: OK, others: error</returns>
        private static async Task<int> ParseAttrHelp(FileReader sr, MenuEntry entry)
        {
            // help text: "help" or "---help---"
            var sb = new StringBuilder();

            var firstIndentationLevel = 0;
            while (true)
            {
                var helpLine = await sr.ReadLineAsync(false, false);
                if (helpLine == null)
                    return 0;
                if (string.IsNullOrEmpty(helpLine))
                {
                    sb.AppendLine(helpLine);
                    continue;
                }

                var indentationLevel = GetStartBlankLength(helpLine);
                if (firstIndentationLevel == 0)
                    firstIndentationLevel = indentationLevel;

                if (indentationLevel < firstIndentationLevel)
                {
                    sr.PushBackLastLine();
                    break;
                }

                sb.AppendLine(helpLine.Substring(firstIndentationLevel));
            }

            entry.Attributes.Add(new MenuAttribute
            {
                AttributeType = MenuAttributeType.Help,
                SymbolValue = RemoveEndBlankRegex.Match(sb.ToString()).Groups["string"].Value
            });
            return 0;
        }

        /// <summary>
        /// parse and add option attribute to entry
        /// </summary>
        /// <param name="sr">for get location</param>
        /// <param name="entry">attribute will add to this entry</param>
        /// <param name="inStr">source line</param>
        /// <returns>true: option is env, false: others</returns>
        private static bool ParseAttrOption(FileReader sr, MenuEntry entry, string inStr)
        {
            if (!GetOptionTypeValue(inStr, out var optType, out var optVal))
                throw sr.GenExpWithLocation("Could not parse \"option\" attribute.");

            // add option
            entry.Attributes.Add(new MenuAttribute
            {
                AttributeType = MenuAttributeType.Option,
                SymbolValue = optVal, // use for "env"=<value> only.
                ExpressionType = optType,
            });
            return optType == MenuAttributeType.Env;
        }

        private static string RemoveQuotationMark(string str)
        {
            var match = RemoveQuotationMarkRegex.Match(str);
            return match.Success ? match.Groups["val"].Value : str;
        }

        /// <summary>
        /// Add value type and prompt to entry
        /// <para>"bool"/"tristate"/"string"/"hex"/"int" [&lt;prompt&gt;]</para>
        /// </summary>
        /// <param name="sr">for get location</param>
        /// <param name="entry">attribute will add to this entry</param>
        /// <param name="inStr">prompt</param>
        /// <param name="type">"bool"/"tristate"/"string"/"hex"/"int"</param>
        private static void ParseAttrValueType(FileReader sr, MenuEntry entry, string inStr,
            MenuAttributeType type)
        {
            // type definition: "bool"/"tristate"/"string"/"hex"/"int" [<prompt>]
            if (entry.Attributes.Any(attribute =>
                attribute.AttributeType == MenuAttributeType.ValueType))
                throw sr.GenExpWithLocation(
                    "Could not add multi \"ValueType\" attribute.");
            // add value type
            entry.Attributes.Add(new MenuAttribute
            {
                AttributeType = MenuAttributeType.ValueType,
                ExpressionType = type
            });
            // set entry value type
            entry.ValueType = type;

            if (string.IsNullOrEmpty(inStr))
                return;

            // add prompt
            entry.Attributes.Add(new MenuAttribute()
            {
                AttributeType = MenuAttributeType.Prompt,
                SymbolValue = RemoveQuotationMark(inStr)

            });
        }

        /// <summary>
        /// parse and add default attribute to entry
        /// <para>"def_bool" / "def_tristate" &lt; expr &gt; ["if" &lt; expr &gt;]</para>
        /// </summary>
        /// <param name="sr">for get location</param>
        /// <param name="entry">attribute will add to this entry</param>
        /// <param name="inStr">source line</param>
        /// <param name="type">"def_bool"/"def_tristate"</param>
        private static void ParseAttrDefaultType(FileReader sr, MenuEntry entry, string inStr,
            MenuAttributeType type)
        {
            // remove "def_"
            if (!Enum.TryParse(type.ToString().Substring(4),
                true, out MenuAttributeType defValType))
                throw sr.GenExpWithLocation($"Fail to parse \"{type}\" attribute.");

            if (entry.Attributes.Any(attribute =>
                attribute.AttributeType == MenuAttributeType.ValueType))
                throw sr.GenExpWithLocation(
                    $"Could not add {type} attribute when \"ValueType\" attribute exist.");

            entry.Attributes.Add(new MenuAttribute
            {
                AttributeType = MenuAttributeType.ValueType,
                ExpressionType = defValType
            });

            //set entry value type
            entry.ValueType = defValType;

            if (!string.IsNullOrEmpty(inStr))
                ParseAttrWithValAndCond(sr, entry, inStr, MenuAttributeType.Default);
        }

        /// <summary>
        /// Set environment as Process level when entry has option env and not condition default 
        /// </summary>
        /// <param name="entry">search option env in this entry</param>
        private static void SetEnvironmentVariable(MenuEntry entry)
        {
            var attrName = entry.Attributes.FirstOrDefault(attr =>
                attr.AttributeType == MenuAttributeType.Option &&
                attr.ExpressionType == MenuAttributeType.Env);
            if (attrName == null)
                return;

            var envName = attrName.SymbolValue;

            var envVal = Environment.GetEnvironmentVariable(envName);

            if (envVal == null)
            {
                var attrDefault = entry.Attributes.FirstOrDefault(attr =>
                    (attr.AttributeType == MenuAttributeType.Default &&
                     attr.Condition == ""));
                if (attrDefault == null)
                    return;
                envVal = RemoveQuotationMark(attrDefault.SymbolValue);
            }

            Environment.SetEnvironmentVariable(entry.Name.Substring(1),
                envVal, EnvironmentVariableTarget.Process);
        }

        #endregion // Parse attribute method

        /// <summary>
        /// Default kconfig file name. Append this when parse input path is a directory.
        /// </summary>
        public static string DefaultKconfigFileName = "Kconfig";

        /// <summary>
        /// Parse kconfig file to menu entry
        /// </summary>
        /// <param name="path">top Kconfig file name, or root folder which contains the top Kconfig file</param>
        /// <param name="tabWidth">tab width, using for replace tab with whitespace</param>
        /// <returns>menu entry</returns>
        public static async Task<MenuEntry> Parse(string path, int tabWidth = 4)
        {
            var attr = File.GetAttributes(path);
            var name = (attr.HasFlag(FileAttributes.Directory))
                ? $"{path}\\{DefaultKconfigFileName}"
                : path;

            if (!File.Exists(name))
            {
                Console.WriteLine($"File do not exist. File name = {name}", Brushes.Red);
                return null;
            }

            // change working directory.
            var filePath = Path.GetDirectoryName(name);
            if (!string.IsNullOrEmpty(filePath))
                Directory.SetCurrentDirectory(filePath);

            // split kconfig file name from path
            var fileName = Path.GetFileName(name);

            // create root menu entry (main menu)
            var mainMenuEntry = new MenuEntry {EntryType = MenuEntryType.MainMenu};
            int st;
            using (var sr = new FileReader(fileName, tabWidth))
                st = await ParseEntryBlock(sr, mainMenuEntry);
            if (st == 0)
                return mainMenuEntry;

            Console.WriteLine($"Fail to parse file. File name = {name}. error = {st}", Brushes.Red);
            return null;
        }

        #region Parse entry method

        /// <summary>
        /// "AND" IF block condition with parent entry's nest depends on expression
        /// </summary>
        /// <param name="parentEntry"></param>
        /// <returns></returns>
        private static string CalcualteNestDependsOnExpr(MenuEntry parentEntry)
        {
            // only if block need to AND new expression
            if (parentEntry.EntryType != MenuEntryType.If)
                return parentEntry.NestDependsOnExpression;

            // if block's nest depends on is null, the expression should be if block's name.
            if (string.IsNullOrEmpty(parentEntry.NestDependsOnExpression))
                return parentEntry.Name;

            // nest expr and if block name(expr)
            return string.IsNullOrEmpty(parentEntry.Name)
                ? parentEntry.NestDependsOnExpression
                : $"({parentEntry.NestDependsOnExpression})&&({parentEntry.Name})";
        }

        /// <summary>
        /// for menuconfig, insert the following entries to it when they depends on this menuconfig.
        /// for others, insert to parent entry directly.
        /// </summary>
        /// <param name="parentEntry"></param>
        /// <param name="childEntry"></param>
        private static List<MenuEntry> AddEntryToParentEntry(MenuEntry parentEntry, 
            MenuEntry childEntry)
        {
            // skip when child entry count is zero
            if (parentEntry.ChildEntries.Count == 0)
                return parentEntry.ChildEntries;

            var lastChildEntry = parentEntry.ChildEntries[parentEntry.ChildEntries.Count - 1];

            // skip when child entry is not menu config
            if (lastChildEntry.EntryType != MenuEntryType.MenuConfig)
                return parentEntry.ChildEntries;

            // process if block
            if ((childEntry.EntryType == MenuEntryType.If) &&
                (childEntry.Name == lastChildEntry.Name))
                return lastChildEntry.ChildEntries;

            // process depends on
            if (childEntry.Attributes.Any(attribute =>
                attribute.AttributeType == MenuAttributeType.DependsOn
                && attribute.Condition == lastChildEntry.Name))
                return lastChildEntry.ChildEntries;

            return parentEntry.ChildEntries;
        }

        /// <summary>
        /// Parse and add entry block into parent entry
        /// </summary>
        /// <param name="sr">file reader</param>
        /// <param name="parentEntry">parent entry</param>
        /// <returns>0: OK, others: Fail</returns>
        private static async Task<int> ParseEntryBlock(FileReader sr, MenuEntry parentEntry)
        {
            while (true)
            {
                var str = await sr.ReadLineAsync();

                if (str == null)
                    return 0;

                // read MenuEntryType/Prompt pair
                if (!GetEntryTypeValue(str, out var entryType, out var val))
                {
                    sr.PushBackLastLine();
                    return 0;
                }

                int st;
                MenuEntry childEntry;
                switch (entryType)
                {
                    case MenuEntryType.EndMenu:
                        if (parentEntry.EntryType == MenuEntryType.Menu)
                            return 0;
                        throw sr.GenExpWithLocation($"Find unpaired {entryType}.");
                    case MenuEntryType.EndChoice:
                        if (parentEntry.EntryType == MenuEntryType.Choice)
                            return 0;
                        throw sr.GenExpWithLocation($"Find unpaired {entryType}.");
                    case MenuEntryType.EndIf:
                        if (parentEntry.EntryType == MenuEntryType.If)
                            return 0;
                        throw sr.GenExpWithLocation($"Find unpaired {entryType}.");
                    case MenuEntryType.MainMenu:
                        st = ParseMainMenu(sr, parentEntry, val);
                        if (st != 0)
                            return st;
                        break;
                    case MenuEntryType.Source:
                    case MenuEntryType.If:
                        childEntry = new MenuEntry
                        {
                            EntryType = entryType,
                            Location = sr.GetLocation(),
                            Name = val,
                            ParentEntry = parentEntry,
                            NestDependsOnExpression = CalcualteNestDependsOnExpr(parentEntry)
                        };
                        st = await ParseFuncDict[entryType](sr, childEntry);
                        if (st != 0)
                            return st;
                        // remove if and source entry form nest tree
                        foreach (var entry in childEntry.ChildEntries)
                            entry.ParentEntry = parentEntry;
                        AddEntryToParentEntry(parentEntry, childEntry).AddRange(childEntry.ChildEntries);
                        break;

                    default:
                        childEntry = new MenuEntry
                        {
                            EntryType = entryType,
                            Location = sr.GetLocation(),
                            Name = val,
                            ParentEntry = parentEntry,
                            NestDependsOnExpression = CalcualteNestDependsOnExpr(parentEntry)
                        };
                        st = await ParseFuncDict[entryType](sr, childEntry);
                        if (st != 0)
                            return st;
                        AddEntryToParentEntry(parentEntry, childEntry).Add(childEntry);
                        break;
                }

            }
        }

        /// <summary>
        /// Parse and add "config" entry into parent entry
        /// </summary>
        /// <param name="sr">file reader</param>
        /// <param name="parentEntry">parent entry</param>
        /// <returns>0: OK, others: Fail</returns>
        private static async Task<int> ParseConfig(FileReader sr, MenuEntry parentEntry)
        {
            var isOptionEnv = false;
            while (true)
            {
                var str = await sr.ReadLineAsync();

                if (str == null)
                    break;

                if (!GetAttributeTypeValue(str, out var attributeType, out var val))
                {
                    sr.PushBackLastLine();
                    break;
                }

                switch (attributeType)
                {
                    case MenuAttributeType.Bool:
                    case MenuAttributeType.Tristate:
                    case MenuAttributeType.String:
                    case MenuAttributeType.Int:
                    case MenuAttributeType.Hex:
                        // type definition: "bool"/"tristate"/"string"/"hex"/"int" [<prompt>]
                        ParseAttrValueType(sr, parentEntry, val, attributeType);
                        break;
                    case MenuAttributeType.DefBool:
                    case MenuAttributeType.DefTristate:
                        // type definition + default value: "def_bool"/"def_tristate" < expr > ["if" < expr >]
                        ParseAttrDefaultType(sr, parentEntry, val, attributeType);
                        break;
                    case MenuAttributeType.Prompt:
                    // input prompt: "prompt" <prompt> ["if" <expr>]
                    case MenuAttributeType.Default:
                        // default value: "default" <expr> ["if" <expr>]
                        ParseAttrWithValAndCond(sr, parentEntry, RemoveQuotationMark(val), attributeType);
                        break;
                    case MenuAttributeType.Select:
                    // reverse dependencies: "select" <symbol> ["if" <expr>]
                    case MenuAttributeType.Imply:
                        // weak reverse dependencies: "imply" <symbol> ["if" <expr>]
                        ParseAttrWithValAndCond(sr, parentEntry, val, attributeType);
                        break;
                    case MenuAttributeType.DependsOn:
                        // dependencies: "depends on" <expr>
                        ParseDependsOn(parentEntry, val);
                        break;
                    case MenuAttributeType.Range:
                        // numerical ranges: "range" <symbol> <symbol> ["if" <expr>]
                        ParseAttrRange(sr, parentEntry, val);
                        break;
                    case MenuAttributeType.Help:
                        // help text: "help" or "---help---"
                        await ParseAttrHelp(sr, parentEntry);
                        break;
                    case MenuAttributeType.Option:
                        // misc options: "option" <symbol>[=<value>]
                        isOptionEnv = ParseAttrOption(sr, parentEntry, val);
                        break;
                    default:
                        throw sr.GenExpWithLocation(
                            $"Found invalid \"{attributeType}\" attribute for config.");
                }
            }

            if (isOptionEnv)
                SetEnvironmentVariable(parentEntry);
            return 0;
        }

        /// <summary>
        /// Parse and add "menu" entry into the parent entry,
        /// </summary>
        /// <param name="sr">file reader</param>
        /// <param name="parentEntry">parent entry</param>
        /// <returns>0: OK, others: Fail</returns>
        private static async Task<int> ParseMenu(FileReader sr, MenuEntry parentEntry)
        {
            // parse menu attributes
            while (true)
            {
                var str = await sr.ReadLineAsync();

                if (str == null)
                    return 0;

                if (!GetAttributeTypeValue(str, out var attributeType, out var val))
                {
                    sr.PushBackLastLine();
                    break;
                }

                switch (attributeType)
                {
                    case MenuAttributeType.VisibleIf:
                        // limiting menu display: "visible if" <expr> (menu block only!)
                        if (parentEntry.EntryType != MenuEntryType.Menu)
                            throw sr.GenExpWithLocation(
                                "Attribute VisibleIf is only applicable to menu blocks");
                        parentEntry.Attributes.Add(new MenuAttribute
                        {
                            AttributeType = attributeType,
                            Condition = val,
                        });

                        break;
                    case MenuAttributeType.DependsOn:
                        // dependencies: "depends on" <expr>
                        ParseDependsOn(parentEntry, val);
                        break;
                    default:
                        throw sr.GenExpWithLocation(
                            $"Found invalid \"{attributeType}\" attribute for menu.");
                }
            }

            return await ParseEntryBlock(sr, parentEntry);
        }

        /// <summary>
        /// choice's child entry can only be config or if block with configures. 
        /// Value type of all config should be bool or tristate.
        /// Assign choice value type from first config if choice value has not assigned.
        /// </summary>
        /// <param name="entry">choice entry</param>
        /// <param name="choiceType">choice value type</param>
        private static void CheckConfigValueTypeInChoice(MenuEntry entry,
            ref MenuAttributeType choiceType)
        {
            foreach (var childEntry in entry.ChildEntries)
            {
                switch (childEntry.EntryType)
                {
                    case MenuEntryType.Config:
                        var attr = childEntry.Attributes
                            .FirstOrDefault(attribute =>
                                attribute.AttributeType == MenuAttributeType.ValueType);
                        if (attr == null)
                            break;

                        var valueType = attr.ExpressionType;

                        if (valueType != MenuAttributeType.Bool && valueType != MenuAttributeType.Tristate)
                            throw new ParseException(
                                $"Choice contains config which type is \"{valueType}\".",
                                childEntry.Location);

                        if (choiceType == MenuAttributeType.Invalid)
                        {
                            choiceType = valueType;
                            break;
                        }

                        if (valueType != choiceType)
                            throw new ParseException(
                                $"Choice type is \"{choiceType}\", but it contains config which type is \"{valueType}\".",
                                childEntry.Location);
                        break;

                    case MenuEntryType.If:
                        CheckConfigValueTypeInChoice(childEntry, ref choiceType);
                        break;
                    default:
                        throw new ParseException(
                            $"Choice contains invalid entry which type is \"{childEntry.EntryType}\".",
                            childEntry.Location);
                }
            }
        }

        /// <summary>
        /// Parse and add "choice" entry into the parent entry,
        /// </summary>
        /// <param name="sr">file reader</param>
        /// <param name="parentEntry">parent entry</param>
        /// <returns>0: OK, others: Fail</returns>
        private static async Task<int> ParseChoice(FileReader sr, MenuEntry parentEntry)
        {
            var choiceValueType = MenuAttributeType.Invalid;
            // parse attribute
            while (true)
            {
                var str = await sr.ReadLineAsync();

                if (str == null)
                    return 0;

                if (!GetAttributeTypeValue(str, out var attributeType, out var val))
                {
                    sr.PushBackLastLine();
                    break;
                }

                switch (attributeType)
                {
                    case MenuAttributeType.Bool:
                    case MenuAttributeType.Tristate:
                        ParseAttrValueType(sr, parentEntry, val, attributeType);
                        choiceValueType = attributeType;
                        break;

                    case MenuAttributeType.DefBool:
                    case MenuAttributeType.DefTristate:
                        // "def_bool" / "def_tristate" < expr > ["if" < expr >]
                        ParseAttrDefaultType(sr, parentEntry, val, attributeType);
                        choiceValueType = attributeType;
                        break;
                    case MenuAttributeType.Prompt:
                        ParseAttrWithValAndCond(sr, parentEntry, RemoveQuotationMark(val), 
                            attributeType);
                        break;
                    case MenuAttributeType.Default:
                    case MenuAttributeType.Select:
                    case MenuAttributeType.Imply:
                        ParseAttrWithValAndCond(sr, parentEntry, val, attributeType);
                        break;
                    case MenuAttributeType.DependsOn:
                        // dependencies: "depends on" <expr>
                        ParseDependsOn(parentEntry, val);
                        break;
                    case MenuAttributeType.Help:
                        // help text: "help" or "---help---"
                        await ParseAttrHelp(sr, parentEntry);
                        break;
                    case MenuAttributeType.Option:
                        ParseAttrOption(sr, parentEntry, val);
                        break;
                    case MenuAttributeType.Optional:
                        // A choice accepts another option "optional", which allows to set the
                        // choice to 'n' and no entry needs to be selected.
                        parentEntry.Attributes.Add(new MenuAttribute
                        {
                            AttributeType = MenuAttributeType.Optional,
                        });
                        break;
                    default:
                        throw sr.GenExpWithLocation(
                            $"Found invalid \"{attributeType}\" attribute for choice.");
                }
            }

            // parse config entry and nest if blocks
            var st = await ParseEntryBlock(sr, parentEntry);
            if (st != 0)
                return st;
            // check all configures' value type. 
            CheckConfigValueTypeInChoice(parentEntry, ref choiceValueType);

            //set entry value type
            parentEntry.ValueType = choiceValueType;
            return 0;
        }

        /// <summary>
        /// Parse and add "comment" entry into the parent entry,
        /// </summary>
        /// <param name="sr">file reader</param>
        /// <param name="parentEntry">parent entry</param>
        /// <returns>0: OK, others: Fail</returns>
        private static async Task<int> ParseComment(FileReader sr, MenuEntry parentEntry)
        {
            // parse menu attributes
            while (true)
            {
                var str = await sr.ReadLineAsync();

                if (str == null)
                    return 0;

                if (!GetAttributeTypeValue(str, out var attributeType, out var val))
                {
                    sr.PushBackLastLine();
                    return 0;
                }

                switch (attributeType)
                {
                    case MenuAttributeType.DependsOn:
                        // dependencies: "depends on" <expr>
                        ParseDependsOn(parentEntry, val);
                        break;
                    default:
                        throw sr.GenExpWithLocation(
                            $"Found invalid \"{attributeType}\" attribute for comment.");
                }
            }
        }

        /// <summary>
        /// Parse and add "main menu" entry into the parent entry,
        /// </summary>
        /// <param name="sr">file reader</param>
        /// <param name="parentEntry">parent entry</param>
        /// <param name="name">main menu prompt</param>
        /// <returns>0: OK, others: Fail</returns>
        private static int ParseMainMenu(FileReader sr, MenuEntry parentEntry, string name)
        {
            if ((parentEntry.EntryType != MenuEntryType.MainMenu) || (parentEntry.Name != null))
                throw sr.GenExpWithLocation("Find multi main menu define.");
            parentEntry.Name = name;
            parentEntry.EntryType = MenuEntryType.MainMenu;
            parentEntry.Location = sr.GetLocation();
            return 0;
        }

        /// <summary>
        /// Parse and add "source" entry into the parent entry,
        /// </summary>
        /// <param name="sr">file reader</param>
        /// <param name="parentEntry">parent entry</param>
        /// <returns>0: OK, others: Fail</returns>
        private static async Task<int> ParseSource(FileReader sr, MenuEntry parentEntry)
        {
            // remove quotation marks if path has.
            var nameTemp = RemoveQuotationMark(parentEntry.Name);

            if (!File.Exists(nameTemp))
            {
                Console.WriteLine($"Source file do not exist. name = {nameTemp}. {sr.GetLocation()}",
                    Brushes.Red);
                return 0;
            }

            using (var fr = new FileReader(nameTemp, sr.TabWidth))
                return await ParseEntryBlock(fr, parentEntry);
        }

        #endregion // Parse entry method
    }
}

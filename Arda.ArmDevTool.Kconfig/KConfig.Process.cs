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
//  File       : DotConfigIo.cs
//  Description: Process the structure, from hierarchical to flat
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: KConfig.Process.cs 1679 2018-01-25 04:00:30Z fupengfei               $
//------------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Arda.ArmDevTool.Kconfig
{
    internal static class KConfigProcess
    {

        #region Parse Hierarchical

        /// <summary>
        /// Form nest menu entry to flat menu entry list
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static async Task<HashSet<MenuEntry>> ToFlat(MenuEntry entry)
        {
            var list = new HashSet<MenuEntry>();
            switch (entry.EntryType)
            {
                case MenuEntryType.Menu:
                case MenuEntryType.If:
                case MenuEntryType.MainMenu:
                case MenuEntryType.Choice:
                case MenuEntryType.Source:
                case MenuEntryType.MenuConfig:
                    list.Add(entry);
                    foreach (var childEntry in entry.ChildEntries)
                        list.UnionWith(await ToFlat(childEntry));
                    break;
                case MenuEntryType.Config:
                case MenuEntryType.Comment:
                    list.Add(entry);
                    break;
            }
            return list;
        }

        /// <summary>
        /// Generate expression for all entry in entry list.
        /// </summary>
        /// <param name="entryList"></param>
        public static void GenExpr(HashSet<MenuEntry> entryList)
        {
            Parallel.ForEach(entryList, entry =>
            {
                try
                {
                    entry.DependsOnExpr = Expression.ConvertToExpression(
                        entry.NestDependsOnExpression, entryList, entry.Location, out var exprDependsOn);
                    entry.DependsOnList.UnionWith(exprDependsOn);

                    // choice control its children configures.
                    if ((entry.ParentEntry != null) && (entry.ParentEntry.EntryType == MenuEntryType.Choice))
                        entry.DependsOnList.Add(entry.ParentEntry);

                    foreach (var attr in entry.Attributes)
                    {
                        attr.ConditionExpr = Expression.ConvertToExpression(
                            attr.Condition, entryList, entry.Location, out exprDependsOn);
                        entry.DependsOnList.UnionWith(exprDependsOn);

                        // process depends on, select and imply
                        if (attr.AttributeType == MenuAttributeType.DependsOn)
                        {
                            CombineDependsOnExpr(entry, attr.ConditionExpr);
                            continue;
                        }

                        if (attr.AttributeType != MenuAttributeType.Select
                            && attr.AttributeType != MenuAttributeType.Imply)
                            continue;
                        attr.ReverseDependency =
                            entryList.FirstOrDefault(revEntry =>
                                revEntry.Name == attr.SymbolValue);

                        if (attr.ReverseDependency == null)
                        {
                            Console.WriteLine(
                                $"Could not find reverse dependency symbol. Name = {attr.SymbolValue}. {entry.Location}",
                                Brushes.Red);
                            continue;
                        }

                        var list = attr.AttributeType == MenuAttributeType.Select
                            ? attr.ReverseDependency.BeSelectedList
                            : attr.ReverseDependency.BeImpliedList;
                        list.Add(entry);
                    }
                }
                catch (Exception e)
                {
                    throw new ParseException(e.Message, e, entry.Location);
                }

            });
        }

        /// <summary>
        /// combine depends on from depends on attribute and parent entry's nest depends on
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="dependsOnExpr"></param>
        private static void CombineDependsOnExpr(MenuEntry entry, Expression dependsOnExpr)
        {
            if (dependsOnExpr == null)
                return;
            if (entry.DependsOnExpr == null)
            {
                entry.DependsOnExpr = dependsOnExpr;
                return;
            }
            // "AND" two expressions
            var expr = new Expression()
            {
                Right = new ExpressionData(entry.DependsOnExpr),
                Left = new ExpressionData(dependsOnExpr),
                Type = ExpressionType.And
            };
            entry.DependsOnExpr = expr;
        }

        /// <summary>
        /// Generate multi level control lists. 
        /// When entry value changed, all entries in lists should be calculated level by level.
        /// </summary>
        /// <param name="entryList"></param>
        public static void GenControls(List<HashSet<MenuEntry>> entryList)
        {
            var levelCount = entryList.Count;
            if (levelCount <= 1)
                return;
            // Use ConcurrentQueue to enable safe enqueue from multiple threads.
            var exceptions = new ConcurrentQueue<Exception>();

            Parallel.For(0, levelCount - 1, i =>
            {
                // Execute the complete loop and capture all exceptions.
                Parallel.ForEach(entryList[i], entry =>
                {
                    try
                    {
                        var controlsList = new List<HashSet<MenuEntry>>();

                        for (var j = i + 1; j < entryList.Count; j++)
                        {
                            var temp = entryList[j].Where(menuEntry =>
                                menuEntry.DependsOnList.Contains(entry)).ToList();

                            if (temp.Count == 0)
                                continue;
                            var currentLevelControlList = new HashSet<MenuEntry>(temp);
                            controlsList.Add(currentLevelControlList);
                        }

                        if (controlsList.Count > 0)
                            entry.ControlsList = controlsList;
                    }
                    catch (Exception e)
                    {
                        exceptions.Enqueue(e);
                    }
                });
            });
            // Throw the exceptions here after the loop completes.
            if (exceptions.Count > 0)
                throw new AggregateException(exceptions);
        }

        /// <summary>
        /// flat entry list to multi level depends on entry list
        /// </summary>
        /// <param name="input"></param>
        /// <param name="total">total entry count in result</param>
        /// <param name="circulationDependsOnItems">circulation depends on entry list</param>
        /// <returns>multi level depends on entry list</returns>
        public static List<HashSet<MenuEntry>> HierarchicalSort(HashSet<MenuEntry> input,
            out int total, out HashSet<MenuEntry> circulationDependsOnItems)
        {
            total = 0;
            circulationDependsOnItems = null;
            if (input == null || input.Count == 0)
                return null;

            var dest = new List<HashSet<MenuEntry>>();
            var names = new HashSet<MenuEntry>();

            var src = input;
            var count = 0;

            do
            {
                var currentLevel = (count == 0)
                    ? src.AsParallel().Where(cfg =>
                    cfg.DependsOnList.Count == 0
                    //&& cfg.BeSelectedList.Count == 0
                    //&& cfg.BeImpliedList.Count == 0
                    ).ToList()

                    : src.AsParallel().Where(cfg =>
                    cfg.DependsOnList.IsSubsetOf(names)
                    //&& cfg.BeSelectedList.IsSubsetOf(names)
                    //&& cfg.BeImpliedList.IsSubsetOf(names)
                    ).ToList();

                if (currentLevel.Count == 0)
                    break;

                foreach (var entry in currentLevel)
                    entry.DependsOnLevel = dest.Count;

                dest.Add(new HashSet<MenuEntry>(currentLevel));
                names.UnionWith(currentLevel);

                count = src.Count;
                src.ExceptWith(currentLevel);

            } while (count != src.Count);
            total = names.Count;
            circulationDependsOnItems = src;
            return dest;
        }

        #endregion //Parse Hierarchical

    }
}
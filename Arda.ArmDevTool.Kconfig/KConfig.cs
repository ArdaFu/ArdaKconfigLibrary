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
//  Description: Kconfig file operation
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: KConfig.cs 1679 2018-01-25 04:00:30Z fupengfei                       $
//------------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Arda.ArmDevTool.Kconfig
{
    public class KConfig
    {
        /// <summary>
        /// Nest menu entry
        /// </summary>
        public MenuEntry Entries { get; private set; }

        /// <summary>
        /// Hierarchical List
        /// </summary>
        public List<HashSet<MenuEntry>> HierarchicalList { get; private set; }

        /// <summary>
        /// Normally Circulation Depends On Items count should be zero.
        /// </summary>
        public HashSet<MenuEntry> CirculationDependsOnItems { get; private set; }

        /// <summary>
        /// Parse kconfig file to menu entry
        /// </summary>
        /// <param name="fileName">kconfig file name</param>
        /// <param name="tabWidth">tab width, using for replace tab with whitespace</param>
        /// <returns>menu entry</returns>
        public async Task<int> Parse(string fileName, int tabWidth = 4)
        {
            // parse files to menu entry
            Entries = await KConfigParser.Parse(fileName, tabWidth);
            if (Entries == null)
                return -1;

            // nest structure to flat structure
            var flatList = await KConfigProcess.ToFlat(Entries);

            //Generate expression for all entry in entry list.
            KConfigProcess.GenExpr(flatList);

            // Generate hierarchical list
            HierarchicalList = KConfigProcess.HierarchicalSort(flatList, out _, 
                out var circulationDependsOnItems);
            CirculationDependsOnItems = circulationDependsOnItems;
            if (circulationDependsOnItems.Count != 0)
            {
                Console.WriteLine($"Found circulation depends on items, count = {circulationDependsOnItems.Count}",
                    Brushes.Red);
                foreach (var entry in circulationDependsOnItems)
                {
                    Console.WriteLine($"[{entry.EntryType}]{entry.Name} {entry.Location}",
                        Brushes.Black);
                }
                return -2;
            }

            // Generate Control list
            KConfigProcess.GenControls(HierarchicalList);

            // load default values
            LoadDefault();

            // read .config file
            return await ReadDotConfig();
        }

        private void LoadDefault()
        {
            var exceptions = new ConcurrentQueue<Exception>();
            foreach (var entries in HierarchicalList)
                Parallel.ForEach(entries, entry =>
                {
                    try
                    {
                        entry.Calculate(Entries, false, true);
                    }
                    catch (Exception e)
                    {
                        exceptions.Enqueue(e);
                    }

                });
            if (exceptions.Count > 0)
                throw new AggregateException(exceptions);
        }

        /// <summary>
        /// read from .config file to get previous config values
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private async Task<int> ReadDotConfig(string fileName = ".config")
        {
            var list = await DotConfigIo.ReadFile(fileName);
            if (list == null)
                return 0;

            foreach (var entries in HierarchicalList)
            {
                var removeSet = new HashSet<DotConfigItem>();
                foreach (var item in list)
                {
                    var entry = entries.FirstOrDefault(menuEntry => menuEntry.Name == item.Name);
                    if (entry == null)
                        continue;
                    removeSet.Add(item);

                    if ((entry.ValueType == MenuAttributeType.Bool)
                        && (item.Type == MenuAttributeType.Tristate))
                    {
                        entry.Value = item.Value;
                        continue;
                    }
                    if (entry.ValueType == item.Type)
                        entry.Value = item.Value;
                }
                foreach (var item in removeSet)
                {
                    list.Remove(item);
                }
                if (list.Count == 0)
                    return 0;
            }
            return 0;
        }

        /// <summary>
        /// write to .config file to save config values
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task<int> WriteDotConfig(string fileName = ".config")
            => await DotConfigIo.WriteFile(Entries, fileName);

    }
}

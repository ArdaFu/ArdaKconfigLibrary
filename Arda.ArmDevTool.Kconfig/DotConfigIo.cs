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
//  Description: .config file reader and writer.
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: DotConfigIo.cs 1679 2018-01-25 04:00:30Z fupengfei                   $
//------------------------------------------------------------------------------
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Arda.ArmDevTool.Kconfig
{
    /// <summary>
    /// .config file reader and writer
    /// </summary>
    public class DotConfigIo
    {
        protected static readonly Regex FindConfigRegex = new Regex(
            @"^CONFIG_(?<config>\w+)=((?<triVal>y|m)|(?<intVal>\d+)|(?<hexVal>0x([0-9a-fA-F])+)|(?<mark>[""'])(?<strVal>.*?)(?<!\\)(\k<mark>))$",
            RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        protected static readonly Regex FindConfigNRegex = new Regex(
            @"^# CONFIG_(?<config>\w+) is not set$",
            RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// read .config file
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static async Task<HashSet<DotConfigItem>> ReadFile(string fileName = ".config")
        {
            if (!File.Exists(fileName))
                return null;
            var list = new HashSet<DotConfigItem>();
            using (var sr = new FileReader(fileName))
            {
                while (true)
                {
                    var line = await sr.ReadLineWithoutProcessAsync();
                    if (line == null)
                        break;

                    var match1 = FindConfigRegex.Match(line);
                    if (match1.Success)
                    {
                        if (match1.Groups["triVal"].Success)
                        {
                            list.Add(new DotConfigItem()
                            {
                                Name = match1.Groups["config"].Value,
                                Type = MenuAttributeType.Tristate,
                                Value = match1.Groups["triVal"].Value
                            });
                            continue;
                        }
                        if (match1.Groups["intVal"].Success)
                        {
                            list.Add(new DotConfigItem()
                            {
                                Name = match1.Groups["config"].Value,
                                Type = MenuAttributeType.Int,
                                Value = match1.Groups["intVal"].Value
                            });
                            continue;
                        }
                        if (match1.Groups["hexVal"].Success)
                        {
                            list.Add(new DotConfigItem()
                            {
                                Name = match1.Groups["config"].Value,
                                Type = MenuAttributeType.Hex,
                                Value = match1.Groups["hexVal"].Value
                            });
                            continue;
                        }
                        if (match1.Groups["strVal"].Success)
                        {
                            list.Add(new DotConfigItem()
                            {
                                Name = match1.Groups["config"].Value,
                                Type = MenuAttributeType.String,
                                Value = match1.Groups["strVal"].Value
                            });
                            continue;
                        }
                    }
                    var match2 = FindConfigNRegex.Match(line);
                    if (!match2.Success)
                        continue;
                    list.Add(new DotConfigItem()
                    {
                        Name = match2.Groups["config"].Value,
                        Type = MenuAttributeType.Tristate,
                        Value = "n",
                    });
                }
            }
            return list;
        }
        /// <summary>
        /// Write a nest entry to file writer
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="sw"></param>
        /// <returns></returns>
        private static async Task WriteEntry(MenuEntry entry, FileWriter sw)
        {
            if (!entry.IsEnable)
                return;
            switch (entry.EntryType)
            {
                case MenuEntryType.MainMenu:
                    await sw.WriteLineAsync(
                        $"#\n# Automatically generated file; DO NOT EDIT.\n# {entry.Prompt}\n#");
                    foreach (var childEntry in entry.ChildEntries)
                        await WriteEntry(childEntry, sw);
                    break;
                case MenuEntryType.Menu:
                    await sw.WriteLineAsync($"\n#\n# {entry.Prompt}\n#");
                    foreach (var childEntry in entry.ChildEntries)
                        await WriteEntry(childEntry, sw);
                    break;
                case MenuEntryType.Choice:
                    foreach (var childEntry in entry.ChildEntries)
                        await WriteEntry(childEntry, sw);
                    break;
                case MenuEntryType.Config:
                case MenuEntryType.MenuConfig:
                    //skip global variable
                    if (entry.Name.StartsWith("$"))
                        break;
                    if ((entry.ValueType == MenuAttributeType.Bool)
                        || (entry.ValueType == MenuAttributeType.Tristate))
                    {
                        if (entry.Value == "n")
                        {
                            await sw.WriteLineAsync($"# CONFIG_{entry.Name} is not set");
                            break;
                        }
                    }
                    if (entry.ValueType == MenuAttributeType.String)
                        await sw.WriteLineAsync($"CONFIG_{entry.Name}=\"{entry.Value}\"");
                    else
                        await sw.WriteLineAsync($"CONFIG_{entry.Name}={entry.Value}");
                    break;
                case MenuEntryType.Comment:
                    await sw.WriteLineAsync($"# {entry.Prompt}");
                    break;
            }
        }

        /// <summary>
        /// Write a nest entry to a file.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static async Task<int> WriteFile(MenuEntry entry, string fileName = ".config")
        {
            if (entry == null)
                return -1;
            using (var sw = new FileWriter(fileName))
            {
                await WriteEntry(entry, sw);
                await sw.WriteLineAsync("");
                await sw.FlashAsync();
            }
            return 0;
        }
    }

    /// <summary>
    /// .config file item
    /// </summary>
    public class DotConfigItem
    {
        /// <summary>
        /// configure name
        /// </summary>
        public string Name;
        /// <summary>
        /// configure value
        /// </summary>
        public string Value;
        /// <summary>
        ///  configure type
        /// </summary>
        public MenuAttributeType Type;

        public override string ToString()
        {
            return $"[{Type}] {Name} = {Value}";
        }
    }
}

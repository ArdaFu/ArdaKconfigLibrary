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
//  Description: Convert config.src to config.in. 
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: ConfigSrcToIn.cs 1683 2018-01-26 06:55:11Z fupengfei                 $
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Arda.ArmDevTool.Kconfig
{

    /// <summary>
    /// convert config.src to config.in. 
    /// Inset all config item from *.c files into "INSERT" point.
    /// for "busy-box"
    /// </summary>
    public static class ConfigSrcToIn
    {

        public static async Task<int> GenerateConfigIn(string fileName = "Config.in")
        {
            var attr = File.GetAttributes(fileName);
            var name = (attr.HasFlag(FileAttributes.Directory)) ? $"{fileName}\\Config.in" : fileName;

            if (!File.Exists(name))
            {
                Console.WriteLine($"File do not exist. File name = {name}", Brushes.Red);
                return -1;
            }

            // change working directory.
            var filePath = Path.GetDirectoryName(name);
            if (!string.IsNullOrEmpty(filePath))
                Directory.SetCurrentDirectory(filePath);
            await ProcessConfigIn(name);
            return 0;
        }

        private static readonly Regex GetConfigSourceRegex = new Regex(
            @"^source (?<path>.*)\/(?<name>Config).in$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static async Task ProcessConfigIn(string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Open))
            using (var sr = new StreamReader(fs))
            {
                var lines = (await sr.ReadToEndAsync()).Split('\n');
               ProcessConfigIn(lines);
            }
        }

        private static void ProcessConfigIn(IEnumerable<string> lines)
        {

            var gps = lines.Where(line => GetConfigSourceRegex.IsMatch(line))
                .Select(line => GetConfigSourceRegex.Match(line).Groups).ToList();

            Parallel.ForEach(gps, async gp =>
            {
                await SearchConfigInCppfiles(gp["path"].Value, gp["name"].Value);
            });
        }

        private static readonly Regex GetConfigLineRegex = new Regex(
            @"^\/\/config:(?<config>.*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static async Task<int> SearchConfigInCppfiles(string path, string name)
        {
            var cfgSrcName = $"{path}/{name}.src";
            var cfgInName = $"{path}/{name}.in";

            if (!File.Exists(cfgSrcName))
                return -1;
            if (File.Exists(cfgInName))
                File.Delete(cfgInName);

            var cfgLines = new List<string>();

            var dirInfos = new DirectoryInfo(path);
            var files = dirInfos.GetFiles("*.c");

            foreach (var file in files)
            {
                using (var fs = new FileStream(file.FullName, FileMode.Open))
                using (var sr = new StreamReader(fs))
                {
                    var lines = (await sr.ReadToEndAsync()).Split('\n');
                    var cfgs = lines.Where(line => GetConfigLineRegex.IsMatch(line)).
                        Select(line => GetConfigLineRegex.Match(line).Groups["config"].Value);
                    cfgLines.AddRange(cfgs);
                }
            }
            using (var fsr = new FileStream(cfgSrcName, FileMode.Open))
            using (var sr = new StreamReader(fsr))
            using (var fsw = new FileStream(cfgInName, FileMode.Create))
            using (var sw = new StreamWriter(fsw))
            {
                var lines = (await sr.ReadToEndAsync()).Split('\n').ToList();
                var insertPoint = -1;
                for (var i = 0; i < lines.Count; i++)
                {
                    if (lines[i] != "INSERT")
                        continue;
                    insertPoint = i;
                    break;
                }
                if (insertPoint >= 0)
                {
                    lines.RemoveAt(insertPoint);
                    lines.InsertRange(insertPoint, cfgLines);
                }
                foreach (var line in lines)
                {
                    await sw.WriteLineAsync(line);
                }
                await sw.FlushAsync();
                await fsw.FlushAsync();
                ProcessConfigIn(lines);
            }
            return 0;
        }
    }
}
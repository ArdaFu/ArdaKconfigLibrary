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
//  Description: File reader and writer with location information.
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: KConfigIo.cs 1679 2018-01-25 04:00:30Z fupengfei                     $
//------------------------------------------------------------------------------
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Arda.ArmDevTool.Kconfig
{
    /// <summary>
    /// Entry location information, file name and line number
    /// </summary>
    public class EntryLocation
    {
        /// <summary>
        /// File full name path
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Line number, start form 1
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Format is "In file "&lt;FileName&gt;", at line &lt;LineNumber&gt;.";
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"In file \"{FileName}\", at line {LineNumber}.";
        }
    }

    /// <summary>
    /// File reader with location information.
    /// </summary>
    public class FileReader : IDisposable
    {
        private static readonly Regex EnvVarRegex =
            new Regex(@"(?<mark>""|')(\\\k<mark>|^\k<mark>)*?\$(?<env>\w+)",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        private static readonly Regex CommentRegex =
            new Regex(@"\s*#.*",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);


        private readonly FileStream _stream;
        private readonly StreamReader _reader;


        private EntryLocation _location;
        public string LastLine { get; private set; }
        private readonly string _tabHolder;
        public readonly int TabWidth;

        /// <summary>
        /// Indicate if last line has been bushed back.
        /// </summary>
        private bool _isPushBackLastLine;

        /// <summary>
        /// Extend string which include env defines.
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        private string ExtWithEnvVar(string src)
        {
            var temp = src;
            var matches = EnvVarRegex.Matches(src);
            if (matches.Count == 0)
                return src;
            foreach (Match match in matches)
            {
                var envName = match.Groups["env"].Value;
                var envVal = Environment.GetEnvironmentVariable(envName);
                if (envVal == null)
                    throw GenExpWithLocation($"Could not get environment variable value. Env name = {envName}.");
                temp = temp.Replace($"${envName}", envVal);
            }
            return temp;
        }

        /// <summary>
        /// Read a line form kconfig file.
        /// </summary>
        /// <returns></returns>
        public async Task<string> ReadLineAsync(bool isSkipEmpty = true,
            bool isExtWithEnvVar = true, bool isRrmoveComment = true)
        {
            while (true)
            {

                if (!_isPushBackLastLine)
                {
                    LastLine = await _reader.ReadLineAsync();
                    if (LastLine == null)
                        return null;
                }
                _isPushBackLastLine = false;
                _location.LineNumber++;

                // remove comment
                var temp = LastLine;

                if (isRrmoveComment)
                {
                    temp = CommentRegex.Replace(LastLine, "");
                    if (string.IsNullOrWhiteSpace(temp))
                        continue;
                }

                if (isSkipEmpty && string.IsNullOrWhiteSpace(temp))
                    continue;
                if (_tabHolder != null)
                    temp = temp.Replace("\t", _tabHolder);

                return isExtWithEnvVar ? ExtWithEnvVar(temp) : temp;
            }
        }

        /// <summary>
        /// Read a line form file without process
        /// </summary>
        /// <returns></returns>
        public async Task<string> ReadLineWithoutProcessAsync(bool isSkipEmpty = true)
        {
            while (true)
            {
                if (!_isPushBackLastLine)
                {
                    LastLine = await _reader.ReadLineAsync();
                    if (LastLine == null)
                        return null;
                }
                _isPushBackLastLine = false;
                _location.LineNumber++;

                if (isSkipEmpty && string.IsNullOrEmpty(LastLine))
                    continue;

                return _tabHolder == null ? LastLine : LastLine.Replace("\t", _tabHolder);
            }
        }

        /// <summary>
        /// Push last read line back to reader. This method could not nest.
        /// </summary>
        public void PushBackLastLine()
        {
            if (_isPushBackLastLine)
                throw GenExpWithLocation("Could not push back last line again");
            _isPushBackLastLine = true;
            _location.LineNumber--;
        }

        /// <summary>
        /// Get location information
        /// </summary>
        /// <returns></returns>
        public EntryLocation GetLocation()
            => new EntryLocation
            {
                FileName = _location.FileName,
                LineNumber = _location.LineNumber
            };

        /// <summary>
        /// Generate exception with location information
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public ParseException GenExpWithLocation(string message)
            => new ParseException(message, _location);

        /// <summary>
        /// Create a file reader
        /// </summary>
        /// <param name="fileName">file name</param>
        /// <param name="tabWidth">tab width, using for replace tab with whitespace</param>
        public FileReader(string fileName, int tabWidth = 4)
        {

            if (tabWidth > 0)
            {
                var sb = new StringBuilder();
                for (var i = 0; i < tabWidth; i++)
                    sb.Append(" ");
                _tabHolder = sb.ToString();
                TabWidth = tabWidth;
            }

            _location = new EntryLocation()
            {
                FileName = Path.GetFullPath(fileName)
            };
            LastLine = null;

            _stream = new FileStream(fileName, FileMode.Open);
            _reader = new StreamReader(_stream);
        }

        #region IDisposable Support

        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _reader.Dispose();
                    _stream.Dispose();
                    _location = null;
                    LastLine = null;
                }
                _disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion

        /// <summary>
        /// Format: "&lt;LastLine&gt;" In file "&lt;FileName&gt;", at line &lt;LineNumber&gt;.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"\"{LastLine}\" {_location}.";
        }
    }

    /// <summary>
    /// File Writer with location information.
    /// </summary>
    public class FileWriter : IDisposable
    {
        private readonly FileStream _stream;
        private readonly StreamWriter _writer;
        private EntryLocation _location;

        public FileWriter(string fileName)
        {
            _location = new EntryLocation()
            {
                FileName = Path.GetFullPath(fileName)
            };
            if (File.Exists(fileName))
            {
                var oldFileName = $"{fileName}.old";
                if (File.Exists(oldFileName))
                    File.Delete(oldFileName);
                File.Move(fileName, oldFileName);
            }
            _stream = new FileStream(fileName, FileMode.CreateNew);
            _writer = new StreamWriter(_stream);
        }

        public async Task WriteLineAsync(string str)
        {
            await _writer.WriteLineAsync(str);
            _location.LineNumber++;
        }

        public async Task FlashAsync()
        {
            await _writer.FlushAsync();
        }

        #region IDisposable Support

        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _writer.Dispose();
                    _stream.Dispose();
                    _location = null;
                }
                _disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion

    }


    /// <inheritdoc />
    /// <summary>
    /// Kconfig parse exception with location information
    /// </summary>
    public class ParseException : ApplicationException
    {
        public readonly EntryLocation Location;

        public ParseException(EntryLocation location)
        {
            Location = location;
        }

        public ParseException(string message,
            EntryLocation location) : base(message)
        {
            Location = location;
        }

        public ParseException(string message,
            Exception innerException, EntryLocation location)
            : base(message, innerException)
        {
            Location = location;
        }

        public override string ToString()
        {
            return $"{base.ToString()} {Location}";
        }
    }
}

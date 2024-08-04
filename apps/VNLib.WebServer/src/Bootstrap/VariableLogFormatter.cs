/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: VariableLogFormatter.cs 
*
* VariableLogFormatter.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System.Text;
using System.Collections.Generic;

using VNLib.Utils.Logging;

namespace VNLib.WebServer.Bootstrap
{
    internal sealed class VariableLogFormatter(ILogProvider logger, LogLevel level)
    {
        private readonly StringBuilder _logFormatSb = new();
        private readonly List<object?> _formatArgs = [];

        public void AppendLine(string line) => _logFormatSb.AppendLine(line);

        public void Append(string value) => _logFormatSb.Append(value);

        public void AppendFormat(string format, params object?[] formatargs)
        {
            _logFormatSb.Append(format);
            _formatArgs.AddRange(formatargs);
        }

        public void AppendLine() => _logFormatSb.AppendLine();

        public void Flush()
        {
            logger.Write(level, _logFormatSb.ToString(), [.._formatArgs]);

            _logFormatSb.Clear();
            _formatArgs.Clear();
        }
    }
}

/*
 * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.
 * Copyright (C) 2018 Mathias Thierbach
 *
 * pbi-tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * pbi-tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * A copy of the GNU Affero General Public License is available in the LICENSE file,
 * and at <https://goto.pbi.tools/license>.
 */

using System;
using System.Diagnostics;
using Serilog;
using Serilog.Events;

namespace PbiTools.Utils
{
    public class LogOperationScope : IDisposable
    {
        private readonly Stopwatch _stopWatch = Stopwatch.StartNew();
        private readonly string _label;
        private readonly LogEventLevel _logLevel;
        private readonly ILogger _log;

        /// <summary>
        /// Creates a new <see cref="LogOperationScope"/>.
        /// </summary>
        /// <param name="label">A custom label to include in log messages.</param>
        /// <param name="logInfo"><c>True</c> to log at Information level, otherwise at Debug.</param>
        /// <param name="log">A specific <see cref="ILogger"/> to use. If not specified, uses the globally-shared instance.</param>
        public LogOperationScope(string label, bool logInfo = true, ILogger log = null)
        {
            this._label = label;
            this._logLevel = logInfo ? LogEventLevel.Information : LogEventLevel.Debug;
            this._log = log ?? Log.Logger;

            if (_log.IsEnabled(_logLevel)) _log.Write(_logLevel, "Starting operation: {Operation}", _label);
        }

        void IDisposable.Dispose()
        {
            if (_log.IsEnabled(_logLevel)) _log.Write(_logLevel, "Operation completed: {Operation}. Took {Elapsed}.", _label, _stopWatch.Elapsed);
        }
    }
}

// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Serilog.Core;
using Serilog.Events;

namespace PbiTools
{
    public class AppSettings
    {
        public static string EnvPrefix => "PBITOOLS_";

        public static class Environment
        {
            public static string LogLevel => $"{EnvPrefix}{nameof(LogLevel)}";
            public static string PbiInstallDir => $"{EnvPrefix}{nameof(PbiInstallDir)}";
        }

        public static string GetEnvironmentSetting(string name) => System.Environment.GetEnvironmentVariable(name);

        public AppSettings()
        {
            // The Console log level can optionally be configured vai an environment variable:
            var envLogLevel = GetEnvironmentSetting(Environment.LogLevel);
            var initialLogLevel = envLogLevel != null && Enum.TryParse<LogEventLevel>(envLogLevel, out var logLevel)
                ? logLevel
                : LogEventLevel.Information; // Default log level
            this.LevelSwitch = new LoggingLevelSwitch(
#if DEBUG
                // A DEBUG compilation will always log at the Verbose level
                LogEventLevel.Verbose
#else
                initialLogLevel
#endif
            );
        }

        public LoggingLevelSwitch LevelSwitch { get; }

        internal bool ShouldSuppressConsoleLogs { get; set; } = false;

        /// <summary>
        /// Completely disables the console logger inside an <c>IDisposable</c> scope.
        /// </summary>
        public IDisposable SuppressConsoleLogs()
        {
            var prevValue = this.ShouldSuppressConsoleLogs;
            this.ShouldSuppressConsoleLogs = true;
            return new Disposable(()=>
            {
                this.ShouldSuppressConsoleLogs = prevValue;
            });
        }

        /// <summary>
        /// Resets the console log level to the specified level inside an <c>IDisposable</c> scope.
        /// </summary>
        public IDisposable SetScopedLogLevel(LogEventLevel logLevel, bool overwriteVerbose = false)
        {
            if (this.LevelSwitch.MinimumLevel == LogEventLevel.Verbose && !overwriteVerbose)
                return new Disposable(() => {});

            var prevLogLevel = this.LevelSwitch.MinimumLevel;
            this.LevelSwitch.MinimumLevel = logLevel;
            return new Disposable(() => 
            {
                this.LevelSwitch.MinimumLevel = prevLogLevel;
            });
        }

        private class Disposable : IDisposable
        {
            private readonly Action _disposeAction;

            public Disposable(Action disposeAction)
            {
                this._disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
            }
            void IDisposable.Dispose()
            {
                _disposeAction();
            }
        }
    }

}

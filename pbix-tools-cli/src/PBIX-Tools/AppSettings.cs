using System;
using System.Runtime.CompilerServices;
using Serilog.Core;
using Serilog.Events;

namespace PbixTools
{
    public class AppSettings
    {
        public LoggingLevelSwitch LevelSwitch { get; } = new LoggingLevelSwitch( // default is Information
#if DEBUG
            LogEventLevel.Verbose
#endif
        );

        internal bool ShouldSuppressConsoleLogs { get; set; } = false;

        public IDisposable SuppressConsoleLogs()
        {
            this.ShouldSuppressConsoleLogs = true;
            return new Disposable(()=>
            {
                this.ShouldSuppressConsoleLogs = false;
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

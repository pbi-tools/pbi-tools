// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Serilog;

namespace PbiTools.Utils
{
    public class CurrentDirectoryScope : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<CurrentDirectoryScope>();

        public string OriginalDirectory { get; }

        public CurrentDirectoryScope(string newCurrentDirectory)
        {
            this.OriginalDirectory = Environment.CurrentDirectory;
            Environment.CurrentDirectory = newCurrentDirectory;
            Log.Debug("Changed CWD from {OldCWD} to {NewCWD}", OriginalDirectory, newCurrentDirectory);
        }


        void IDisposable.Dispose()
        {
            Environment.CurrentDirectory = OriginalDirectory;
            Log.Debug("Reset CWD to {NewCWD}", OriginalDirectory);
        }
    }
}

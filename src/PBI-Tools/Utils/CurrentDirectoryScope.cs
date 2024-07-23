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

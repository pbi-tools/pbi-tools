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
using System.IO;
using PbiTools.FileSystem;
using Serilog;

namespace PbiTools.Serialization
{
    public class StringPartSerializer : IPowerBIPartSerializer<string>
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<StringPartSerializer>();
        private readonly IProjectFile _file;

        public StringPartSerializer(IProjectRootFolder folder, string label)
        {
            if (folder == null) throw new ArgumentNullException(nameof(folder));
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Value cannot be null or empty.", nameof(label));

            _file = folder.GetFile($"{label}.txt");
        }

        public string BasePath => _file.Path;
        
        public bool Serialize(string content)
        {
            if (content == null) return false;
            _file.Write(content);
            return true;
        }

        public bool TryDeserialize(out string part)
        {
            if (_file.TryReadFile(out var stream))
            {
                using (var reader = new StreamReader(stream))
                {
                    part = reader.ReadToEnd();
                    return true;
                }
            }

            part = null;
            return false;
        }
    }
}

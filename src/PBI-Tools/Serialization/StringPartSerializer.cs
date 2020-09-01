// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
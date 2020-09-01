// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PbiTools.FileSystem;
using Serilog;

namespace PbiTools.Serialization
{
    public class JsonPartSerializer : IPowerBIPartSerializer<JObject>
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<JsonPartSerializer>();
        private readonly IProjectFile _file;

        public JsonPartSerializer(IProjectRootFolder folder, string label)
        {
            if (folder == null) throw new ArgumentNullException(nameof(folder));
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Value cannot be null or empty.", nameof(label));

            _file = folder.GetFile($"{label}.json");
        }

        public string BasePath => _file.Path;

        public bool Serialize(JObject content)
        {
            if (content == null) return false;
            _file.Write(content);
            return true;
        }

        public bool TryDeserialize(out JObject part)
        {
            if (_file.TryReadFile(out var stream))
            {
                using (var reader = new JsonTextReader(new StreamReader(stream)))
                {
                    try
                    {
                        part = JToken.ReadFrom(reader) as JObject;
                        return (part != null);
                    }
                    catch (JsonException e)
                    {
                        Log.Error(e, "Json file is invalid: {Path}", _file.Path);
                    }
                }
            }

            part = null;
            return false;
        }
    }
}
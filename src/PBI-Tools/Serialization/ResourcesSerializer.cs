// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using PbiTools.FileSystem;
using Serilog;

namespace PbiTools.Serialization
{
    public class ResourcesSerializer : IPowerBIPartSerializer<IDictionary<string, byte[]>>
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ResourcesSerializer>();
        private readonly IProjectFolder _folder;

        public ResourcesSerializer(IProjectRootFolder folder, string label)
        {
            if (folder == null) throw new ArgumentNullException(nameof(folder));
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Value cannot be null or empty.", nameof(label));

            _folder = folder.GetFolder(label);
        }

        public string BasePath => _folder.BasePath;

        public bool Serialize(IDictionary<string, byte[]> content)
        {
            if (content == null || content.Count == 0) return false;
            foreach (var entry in content)
            {
                // Special handling of 'package.json' to make it readable
                if (Path.GetFileName(entry.Key) == "package.json")
                {
                    try
                    {
                        var json = JObject.Parse(Encoding.UTF8.GetString(entry.Value));
                        _folder.Write(json, entry.Key);
                        continue;
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Failed to parse resource at {Path} as Json object.", entry.Key);
                    }
                }

                _folder.WriteFile(entry.Key, stream =>
                {
                    stream.Write(entry.Value, 0, entry.Value.Length);
                });
            }
        
            return true;
        }

        public bool TryDeserialize(out IDictionary<string, byte[]> part)
        {
            var result = new Dictionary<string, byte[]>();
            var baseUri = new Uri(BasePath + "/" /* Need trailing slash for MakeRelativeUri() below */);

            foreach (var file in _folder.GetFiles("*", SearchOption.AllDirectories))
            {
                if (file.TryReadFile(out var stream))
                using (stream)
                using (var buffer = new MemoryStream())
                {
                    var relResourcePath = baseUri.MakeRelativeUri(new Uri(file.Path)).ToString();
                    stream.CopyTo(buffer);
                    Log.Debug("Deserializing resource at {BasePath} - {Uri}", this.BasePath, relResourcePath);
                    result.Add(relResourcePath, buffer.ToArray());
                }
            }

            if (result.Count > 0)
            {
                part = result;
                return true;
            }
            else
            {
                part = null;
                return false;
            }
        }
    }
}
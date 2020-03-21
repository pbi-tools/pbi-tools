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
        
        public void Serialize(string content)
        {
            if (content == null) return;
            _file.Write(content);
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
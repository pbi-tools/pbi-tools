using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace PbixTools.Tests
{
    public class MockProjectFolder : IProjectFolder
    {
        private readonly Dictionary<string, string> _filesWritten 
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public int NumberOfFilesWritten => _filesWritten.Count;

        public void Dispose()
        {
        }

        public string BasePath { get; }
        public bool CommitDelete { get; set; }

        public bool TryGetFile(string path, out Stream stream)
        {
            stream = null;
            return false;
        }

        public void WriteFile(string path, Stream stream)
        {
            _filesWritten[path] = "**STREAM**";  // TODO Implement once needed by tests
        }

        public void WriteText(string path, Action<TextWriter> writerCallback)
        {
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                writerCallback(writer);
            }

            _filesWritten[path] = sb.ToString();
        }

        public bool ContainsPath(string path)
        {
            return _filesWritten.ContainsKey(path);
        }

        public JObject GetAsJson(string path)
        {
            return JObject.Parse(_filesWritten[path]);
        }
        public XDocument GetAsXml(string path)
        {
            return XDocument.Parse(_filesWritten[path]);
        }

    }
}

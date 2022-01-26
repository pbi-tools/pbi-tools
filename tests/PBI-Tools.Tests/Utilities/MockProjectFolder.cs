// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using PbiTools.FileSystem;

namespace PbiTools.Tests
{
    public class MockRootFolder : IProjectRootFolder
    {
        private readonly Func<IProjectFolder> _createMockFolder;

        public MockRootFolder() : this(() => new MockProjectFolder())
        {
        }

        public MockRootFolder(Func<IProjectFolder> createMockFolder)
        {
            _createMockFolder = createMockFolder;
        }

        public void Dispose()
        {
        }

        public IProjectFolder GetFolder(string name)
        {
            return _createMockFolder();
        }

        public IProjectFile GetFile(string relativePath)
        {
            throw new NotImplementedException();
        }

        public void Commit()
        {
        }

        public bool Exists()
        {
            throw new NotImplementedException();
        }
    }



    /// <summary>
    /// A mock implementation of <see cref="IProjectFolder"/> that stores all file contents in memory, and provides additional methods to retrieve the contents later.
    /// For testing purposes only.
    /// </summary>
    public class MockProjectFolder : IProjectFolder
    {
        private readonly MockProjectFolder _root;

        // ReSharper disable once InconsistentNaming
        private readonly Dictionary<string, string> FilesStore 
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public int NumberOfFilesWritten => _root.FilesStore.Count;

        public MockProjectFolder()
        {
            _root = this;
            BasePath = "";
        }

        private MockProjectFolder(MockProjectFolder parent, params string[] segments)
        {
            _root = parent._root;
            BasePath = Path.Combine(parent.BasePath, Path.Combine(segments ?? new string[0]));
        }


        private string NormalizePath(string path) => new FileInfo(Path.Combine(BasePath, path)).FullName;


        public string BasePath { get; }
        public string Name { get; }

        public IProjectFolder GetSubfolder(params string[] segments)
        {
            return new MockProjectFolder(this, segments);
        }

        public bool TryGetFile(string path, out Stream stream)
        {
            if (ContainsPath(path))
            {
                stream = new MemoryStream(Encoding.UTF8.GetBytes(_root.FilesStore[NormalizePath(path)]));
                return true;
            }

            stream = null;
            return false;
        }

        public bool ContainsFile(string path)
        {
            return ContainsPath(path);
        }

        public void DeleteFile(string path)
        {
        }

        public void WriteFile(string path, Action<Stream> onStreamAvailable)
        {
            using (var stream = new MemoryStream())
            {
                onStreamAvailable(stream);

                stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(stream))
                {
                    _root.FilesStore[NormalizePath(path)] = reader.ReadToEnd();
                }
            }
        }

        public void WriteText(string path, Action<TextWriter> writerCallback)
        {
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                writerCallback(writer);
            }

            _root.FilesStore[NormalizePath(path)] = sb.ToString();
        }

        public bool ContainsPath(string path)
        {
            return _root.FilesStore.ContainsKey(NormalizePath(path));
        }

        public JObject GetAsJson(string path)
        {
            return JObject.Parse(_root.FilesStore[NormalizePath(path)]);
        }

        public XDocument GetAsXml(string path)
        {
            return XDocument.Parse(_root.FilesStore[NormalizePath(path)]);
        }

        public string GetAsString(string path)
        {
            return _root.FilesStore[NormalizePath(path)];
        }

        public bool Exists()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IProjectFolder> GetSubfolders(string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IProjectFile> GetFiles(string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            throw new NotImplementedException();
        }

        public IProjectFile GetFile(string relativePath)
        {
            throw new NotImplementedException();
        }

        public bool TryReadFile(string path, Action<Stream> streamHandler)
        {
            throw new NotImplementedException();
        }
    }
}

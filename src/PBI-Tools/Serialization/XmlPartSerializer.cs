// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using PbiTools.FileSystem;
using Serilog;

namespace PbiTools.Serialization
{
    public class XmlPartSerializer : IPowerBIPartSerializer<XDocument>
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<XmlPartSerializer>();
        private readonly IProjectFile _file;

        public XmlPartSerializer(IProjectRootFolder folder, string label)
        {
            if (folder == null) throw new ArgumentNullException(nameof(folder));
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Value cannot be null or empty.", nameof(label));

            _file = folder.GetFile($"{label}.xml");
        }

        public string BasePath => _file.Path;
        
        public bool Serialize(XDocument content)
        {
            if (content == null) return false;
            _file.Write(content);
            return true;
        }

        public bool TryDeserialize(out XDocument part)
        {
            if (_file.TryReadFile(out var stream))
            {
                using (var reader = XmlReader.Create(new StreamReader(stream), new XmlReaderSettings { CloseInput = true }))
                {
                    try
                    {
                        part = XDocument.Load(reader);
                        return true;
                    }
                    catch (XmlException e)
                    {
                        Log.Error(e, "Xml file is invalid: {Path}", _file.Path);
                    }
                }
            }

            part = null;
            return false;
        }
    }
}
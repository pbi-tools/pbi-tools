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

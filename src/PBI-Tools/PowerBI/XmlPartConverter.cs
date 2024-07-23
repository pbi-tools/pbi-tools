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
using System.Text;
using System.Xml.Linq;

namespace PbiTools.PowerBI
{
    public class XmlPartConverter : IPowerBIPartConverter<XDocument>
    {
        private readonly Encoding _encoding;

        public XmlPartConverter(string relativePartUri) : this(new Uri(relativePartUri, UriKind.Relative), Encoding.Unicode)
        { }

        public XmlPartConverter(Uri partUri, Encoding encoding)
        {
            this.PartUri = partUri;
            _encoding = encoding;
        }

        public Uri PartUri { get; }

        public bool IsOptional { get; set; } = true;
        public string ContentType { get; set; } = PowerBIPartConverters.ContentTypes.Xml;

        public XDocument FromPackagePart(Func<Stream> part, string contentType)
        {
            if (!part.TryGetStream(out var stream) || (contentType?.Contains("json") ?? false)) return default(XDocument);
            using (var reader = new StreamReader(stream, _encoding))
            {
                return XDocument.Load(reader);  // TODO Error Handling
            }
        }

        public Func<Stream> ToPackagePart(XDocument content)
        {
            if (content == null) return null;
            return () => new MemoryStream(_encoding.GetBytes(content.ToString(SaveOptions.DisableFormatting)));
        }
    }
}

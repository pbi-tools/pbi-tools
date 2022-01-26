// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;

namespace PbiTools.PowerBI
{
    public class BytesPartConverter : IPowerBIPartConverter<byte[]>
    {
        public Uri PartUri { get; }

        public bool IsOptional { get; set; } = true;
        public string ContentType { get; set; } = PowerBIPartConverters.ContentTypes.DEFAULT;

        public BytesPartConverter(string relativePartUri) : this(new Uri(relativePartUri, UriKind.Relative))
        { }

        public BytesPartConverter(Uri partUri)
        {
            this.PartUri = partUri;
        }

        public byte[] FromPackagePart(Func<Stream> part, string contentType)
        {
            if (!part.TryGetStream(out var stream)) return default(byte[]);
            using (var memStream = new MemoryStream()) {
                stream.CopyTo(memStream);
                return memStream.ToArray();
            }
        }

        public Func<Stream> ToPackagePart(byte[] content)
        {
            return () => new MemoryStream(content);
        }
    }
}
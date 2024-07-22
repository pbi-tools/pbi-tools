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

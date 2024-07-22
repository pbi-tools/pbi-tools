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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PbiTools.PowerBI
{
    public class JsonPartConverter : IPowerBIPartConverter<JObject>
    {
        private static readonly JsonSerializer DefaultSerializer = new JsonSerializer { };
        private readonly Encoding _encoding;

        /// <summary>
        /// Creates a new <see cref="JsonPartConverter"/> with the specified Uri and UTF-16 encoding.
        /// </summary>
        public JsonPartConverter(string relativePartUri) : this(new Uri(relativePartUri, UriKind.Relative), Encoding.Unicode)
        { }

        public JsonPartConverter(string relativePartUri, Encoding encoding) : this(new Uri(relativePartUri, UriKind.Relative), encoding)
        { }

        public JsonPartConverter(Uri partUri, Encoding encoding)
        {
            this.PartUri = partUri;
            _encoding = encoding;
        }

        public Uri PartUri { get; }

        public bool IsOptional { get; set; } = true;
        public string ContentType { get; set; } = PowerBIPartConverters.ContentTypes.Json;

        public JObject FromPackagePart(Func<Stream> part, string contentType)
        {
            if (!part.TryGetStream(out var stream)) return default(JObject);
            using (var reader = new JsonTextReader(new StreamReader(stream, _encoding)))
            {
                return DefaultSerializer.Deserialize<JObject>(reader); // TODO Error handling
            }
        }

        public Func<Stream> ToPackagePart(JObject content)
        {
            if (content == null) return null;
            return () => new MemoryStream(_encoding.GetBytes(content.ToString(Formatting.None)));
        }
    }
}

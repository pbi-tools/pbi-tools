// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
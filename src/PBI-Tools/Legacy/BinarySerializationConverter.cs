// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.IO;
using Microsoft.PowerBI.Packaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace PbiTools.PowerBI
{
    public class BinarySerializationConverter<T> : IPowerBIPartConverter<JObject>
        where T : IBinarySerializable, new()
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly JsonSerializer CamelCaseSerializer = new JsonSerializer
            { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        public Uri PartUri { get; }

        public bool IsOptional { get; set; } = true;
        public string ContentType { get; set; } = PowerBIPartConverters.ContentTypes.DEFAULT;

        public BinarySerializationConverter(Uri partUri)
        {
            this.PartUri = partUri;
        }

        public JObject FromPackagePart(Func<Stream> part, string contentType)
        {
            if (!part.TryGetStream(out var stream)) return default(JObject);
            using (var reader = new BinarySerializationReader(stream))
            {
                var obj = new T();
                obj.Deserialize(reader);

                return JObject.FromObject(obj, CamelCaseSerializer);
            }
        }

        public Func<Stream> ToPackagePart(JObject content)
        {
            if (content == null) return null;

            return () =>
            {
                var stream = new MemoryStream();
                var writer = new BinarySerializationWriter(stream);

                var obj = content.ToObject<T>(CamelCaseSerializer);
                obj.Serialize(writer);

                return stream;
            };
        }
    }
}
#endif
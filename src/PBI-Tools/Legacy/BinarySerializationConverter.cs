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

            throw new NotSupportedException("Legacy serialization formats are no longer supported.");
        }
    }
}
#endif

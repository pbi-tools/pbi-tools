// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System.IO;
using System.Text;
using Microsoft.PowerBI.Packaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PbiTools.PowerBI
{
    public class JsonPartConverter : IPowerBIPartConverter<JObject>
    {
        private static readonly JsonSerializer DefaultSerializer = new JsonSerializer { };
        private readonly Encoding _encoding;

        public JsonPartConverter() : this(Encoding.Unicode)
        {
        }

        public JsonPartConverter(Encoding encoding)
        {
            _encoding = encoding;
        }


        public JObject FromPackagePart(IStreamablePowerBIPackagePartContent part)
        {
            if (part == null) return default(JObject);
            using (var reader = new JsonTextReader(new StreamReader(part.GetStream(), _encoding)))
            {
                return DefaultSerializer.Deserialize<JObject>(reader); // TODO Error handling
            }
        }

        public IStreamablePowerBIPackagePartContent ToPackagePart(JObject content)
        {
            if (content == null) return new StreamablePowerBIPackagePartContent(default(string));
            return new StreamablePowerBIPackagePartContent(_encoding.GetBytes(content.ToString(Formatting.None)));
        }
    }
}
#endif
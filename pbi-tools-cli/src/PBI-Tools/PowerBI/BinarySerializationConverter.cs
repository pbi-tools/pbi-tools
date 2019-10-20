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

        public JObject FromPackagePart(IStreamablePowerBIPackagePartContent part)
        {
            if (part == null) return default(JObject);
            using (var reader = new BinarySerializationReader(part.GetStream()))
            {
                var metadata = new T();
                metadata.Deserialize(reader);

                return JObject.FromObject(metadata, CamelCaseSerializer);
            }
        }

        public IStreamablePowerBIPackagePartContent ToPackagePart(JObject content)
        {
            if (content == null) return new StreamablePowerBIPackagePartContent(default(string));
            using (var stream = new MemoryStream())
            {
                var writer = new BinarySerializationWriter(stream);

                var obj = content.ToObject<T>(CamelCaseSerializer);
                obj.Serialize(writer);

                return new StreamablePowerBIPackagePartContent(stream.ToArray());
            }
        }
    }
}
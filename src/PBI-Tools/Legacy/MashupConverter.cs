// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Mashup.Client.Packaging;
using Microsoft.Mashup.Client.Packaging.SerializationObjectModel;
using Microsoft.Mashup.Client.Packaging.Serializers;
using Microsoft.Mashup.Host.Document.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace PbiTools.PowerBI
{
    using Model;

    public class MashupConverter : IPowerBIPartConverter<MashupParts>
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<MashupConverter>();
        private static readonly JsonSerializer CamelCaseSerializer = new JsonSerializer { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        private readonly XmlSerializerNamespaces _xmlNamespaces = new XmlSerializerNamespaces();

        private static readonly XmlSerializer PackageMetadataXmlSerializer = new XmlSerializer(typeof(SerializedPackageMetadata));

        public Uri PartUri => PowerBILegacyPartConverters.MashupPartUri;

        public bool IsOptional { get; set; } = true;
        public string ContentType { get; set; } = PowerBIPartConverters.ContentTypes.DEFAULT;

        public MashupConverter()
        {
            _xmlNamespaces.Add("", ""); // omits 'xsd' and 'xsi' namespaces in serialized xml
        }


        public MashupParts FromPackagePart(Func<Stream> part, string contentType)
        {
            if (!part.TryGetStream(out var stream)) return default(MashupParts);

            PackageComponents packageComponents;
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                if (!PackageComponents.TryDeserialize(memoryStream.ToArray(), out packageComponents))
                {
                    throw new Exception("Could not read MashupPackage"); // TODO Better error handling
                }
            }

            var mashupParts = new MashupParts
            {
                Package = new MemoryStream(packageComponents.PartsBytes),
            };

            var permissions = PermissionsSerializer.Deserialize(packageComponents.PermissionBytes, out var _);
            mashupParts.Permissions = JObject.FromObject(permissions, CamelCaseSerializer);

            if (PackageMetadataSerializer.TryDeserialize(packageComponents.MetadataBytes, out SerializedPackageMetadata packageMetadata, out var contentStorageBytes))
            {
                using (var stringWriter = new StringWriter(CultureInfo.InvariantCulture))
                {
                    using (var writer = XmlWriter.Create(stringWriter, new XmlWriterSettings()))
                    {
                        PackageMetadataXmlSerializer.Serialize(writer, packageMetadata, _xmlNamespaces);
                    }

                    mashupParts.Metadata = XDocument.Parse(stringWriter.ToString());
                }

                mashupParts.QueryGroups = ParseQueryGroups(packageMetadata);

                mashupParts.Content = new MemoryStream(contentStorageBytes);
            }

            return mashupParts;
        }

        public Func<Stream> ToPackagePart(MashupParts content)
        {
            if (content == null) return null;

            return () =>
            {
                // PartsBytes: Package Stream as byte[]
                var partsBytes = content.Package?.ToArray() ?? new byte[0];

                // Convert json to PackagePermissions, then use PermissionsSerializer to convert to byte[]
                var permissionBytes = PermissionsSerializer.Serialize(content?.Permissions.ToObject<PackagePermissions>() ?? new PackagePermissions());

                // Convert xml to SerializedPackageMetadata, then use PackageMetadataSerializer to convert to byte[]
                var serializedPackageMetadata = (SerializedPackageMetadata)PackageMetadataXmlSerializer.Deserialize(content.Metadata.CreateReader());
                var metadataBytes = PackageMetadataSerializer.Serialize(
                    serializedPackageMetadata,
                    content.Content?.ToArray() ?? new byte[0]
                );

                Log.Verbose("Generating PackageComponents: {PartsBytes} PartsBytes, {PermissionBytes} PermissionBytes, {MetadataBytes} MetadataBytes"
                    , partsBytes.Length
                    , permissionBytes.Length
                    , metadataBytes.Length);

                var pc = new PackageComponents(partsBytes, permissionBytes, metadataBytes);

                return new MemoryStream(pc.Serialize());
            };
        }


        /* Mashup structure
           - PowerBIPackage.DataMashup (Microsoft.PowerBI.Packaging)
             converts to: PackageComponents (Microsoft.Mashup.Client.Packaging)
             - PartsBytes
               -> ZipArchive
             - PermissionBytes
               -> JObject    (PermissionsSerializer)
             - MetadataBytes
               -> XDocument  (PackageMetadataSerializer)
               -> JArray     (QueriesMetadataSerializer)
               -> ZipArchive (PackageMetadataSerializer/contentStorageBytes)
         */



        internal static JArray ParseQueryGroups(SerializedPackageMetadata packageMetadata)
        {
            foreach (var item in packageMetadata.Items)
            {
                if (QueriesMetadataSerializer.TryGetQueryGroups(item, out QueryGroupMetadataSet queryGroups))
                {
                    return JArray.FromObject(queryGroups.ToArray(), CamelCaseSerializer);
                }
            }

            return null;
        }
    }
}
#endif
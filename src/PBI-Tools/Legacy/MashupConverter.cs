// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
using Microsoft.PowerBI.Packaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PbiTools.Model;

namespace PbiTools.PowerBI
{
    public class MashupConverter : IPowerBIPartConverter<MashupParts>
    {
        private static readonly JsonSerializer CamelCaseSerializer = new JsonSerializer { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        private readonly XmlSerializerNamespaces _xmlNamespaces = new XmlSerializerNamespaces();

        private static readonly XmlSerializer PackageMetadataXmlSerializer = new XmlSerializer(typeof(SerializedPackageMetadata));

        public MashupConverter()
        {
            _xmlNamespaces.Add("", ""); // omits 'xsd' and 'xsi' namespaces in serialized xml
        }


        public MashupParts FromPackagePart(IStreamablePowerBIPackagePartContent part)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (!PackageComponents.TryDeserialize(PowerBIPackagingUtils.GetContentAsBytes(part, isOptional: false), out PackageComponents packageComponents))
            {
                throw new Exception("Could not read MashupPackage"); // TODO Better error handling
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

        public IStreamablePowerBIPackagePartContent ToPackagePart(MashupParts content)
        {
            // TODO Needs Testing
            var partsBytes = content.Package.ToArray();
            var permissionBytes = PermissionsSerializer.Serialize(content?.Permissions.ToObject<PackagePermissions>() ?? new PackagePermissions());
            var serializedPackageMetadata = (SerializedPackageMetadata)PackageMetadataXmlSerializer.Deserialize(content.Metadata.CreateReader());
            var metadataBytes = PackageMetadataSerializer.Serialize(serializedPackageMetadata,
                content.Content.ToArray());

            var pc = new PackageComponents(partsBytes, permissionBytes, metadataBytes);
            return new StreamablePowerBIPackagePartContent(pc.Serialize());
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
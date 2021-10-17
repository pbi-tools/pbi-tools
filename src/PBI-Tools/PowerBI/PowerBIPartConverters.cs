// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using PbiTools.Model;
using PbiTools.Utils;

namespace PbiTools.PowerBI
{
#if NETFRAMEWORK
    public class PowerBILegacyPartConverters
    {

        public IPowerBIPartConverter<JObject> ReportSettings { get; } = new BinarySerializationConverter<Microsoft.PowerBI.Packaging.Storage.ReportSettings>(new Uri("/Settings", UriKind.Relative));
        
        public IPowerBIPartConverter<JObject> ReportMetadata { get; } = new BinarySerializationConverter<Microsoft.PowerBI.Packaging.Storage.ReportMetadataContainer>(new Uri("/Metadata", UriKind.Relative));
        
        public IPowerBIPartConverter<MashupParts> DataMashup { get; } = new MashupConverter();

    }
#endif

    // ReSharper disable once InconsistentNaming
    public class PowerBIPartConverters
    {
        public static class ContentTypes
        {
            public const string Json = "application/json";
            public const string Xml = "application/xml";
            public const string DEFAULT = "";
        }

        public PowerBIPartConverters(
#if NETFRAMEWORK
            string modelName, 
            IDependenciesResolver resolver
#endif
        )
        {
#if NETFRAMEWORK
            if (modelName == null) throw new ArgumentNullException(nameof(modelName));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            this.DataModel = new DataModelConverter(new Uri("/DataModel", UriKind.Relative), modelName, resolver);
#endif
        }

        public IPowerBIPartConverter<string> Version { get; } = new StringPartConverter(new Uri("/Version", UriKind.Relative)) { IsOptional = false };
        public IPowerBIPartConverter<JObject> ReportSettingsV3 { get; } = new JsonPartConverter(new Uri("/Settings", UriKind.Relative));
        public IPowerBIPartConverter<JObject> ReportMetadataV3 { get; } = new JsonPartConverter(new Uri("/Metadata", UriKind.Relative));
        public IPowerBIPartConverter<XDocument> LinguisticSchema { get; } = new XmlPartConverter(new Uri("/Report/LinguisticSchema", UriKind.Relative));
        public IPowerBIPartConverter<JObject> LinguisticSchemaV3 { get; } = new JsonPartConverter(new Uri("/Report/LinguisticSchema", UriKind.Relative));
        public IPowerBIPartConverter<JObject> DiagramViewState { get; } = new JsonPartConverter(new Uri("/DiagramState", UriKind.Relative));
        public IPowerBIPartConverter<JObject> DiagramLayout { get; } = new JsonPartConverter(new Uri("/DiagramLayout", UriKind.Relative));
        public IPowerBIPartConverter<JObject> ReportDocument { get; } = new JsonPartConverter(new Uri("/Report/Layout", UriKind.Relative));

        public IPowerBIPartConverter<JObject> DataModelSchema { get; } = new JsonPartConverter(new Uri("/DataModelSchema", UriKind.Relative));
#if NETFRAMEWORK
        public IPowerBIPartConverter<JObject> DataModel { get; }
#endif
        public IPowerBIPartConverter<JObject> Connections { get; } = new JsonPartConverter(new Uri("/Connections", UriKind.Relative), Encoding.UTF8); // TODO Verify encoding
        public IPowerBIPartConverter<byte[]> CustomVisuals { get; } = new BytesPartConverter(new Uri("/Report/CustomVisuals", UriKind.Relative));
        public IPowerBIPartConverter<byte[]> StaticResources { get; } = new BytesPartConverter(new Uri("/Report/StaticResources", UriKind.Relative));


        // TODO CustomProperties new Uri("/docProps/custom.xml", UriKind.Relative)
        // TODO ReportMobileState new Uri("/Report/MobileState", UriKind.Relative)

        public static string ConvertToValidModelName(string name)
        { 
            const string
                forbiddenCharacters =
                    @". , ; ' ` : / \ * | ? "" & % $ ! + = ( ) [ ] { } < >"; // grabbed these from an AMO exception message
            var modelName = forbiddenCharacters // TODO Could also use TOM.Server.Databases.CreateNewName()
                .Replace(" ", "")
                .ToCharArray()
                .Aggregate(
                    name,
                    (n, c) => n.Replace(c, '_')
                );
            return modelName;
        }
    }
}
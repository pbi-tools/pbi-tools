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

        public static readonly Uri MashupPartUri = new Uri("/DataMashup", UriKind.Relative);
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

            this.DataModel = new DataModelConverter("/DataModel", modelName, resolver);
#endif
        }

        public IPowerBIPartConverter<string> Version { get; } = new StringPartConverter("/Version") { IsOptional = false };
        public IPowerBIPartConverter<JObject> ReportSettingsV3 { get; } = new JsonPartConverter("/Settings");
        public IPowerBIPartConverter<JObject> ReportMetadataV3 { get; } = new JsonPartConverter("/Metadata");
        public IPowerBIPartConverter<XDocument> LinguisticSchema { get; } = new XmlPartConverter("/Report/LinguisticSchema");
        public IPowerBIPartConverter<JObject> LinguisticSchemaV3 { get; } = new JsonPartConverter("/Report/LinguisticSchema");
        public IPowerBIPartConverter<JObject> DiagramViewState { get; } = new JsonPartConverter("/DiagramState");
        public IPowerBIPartConverter<JObject> DiagramLayout { get; } = new JsonPartConverter("/DiagramLayout");
        public IPowerBIPartConverter<JObject> ReportDocument { get; } = new JsonPartConverter("/Report/Layout");

        public IPowerBIPartConverter<JObject> DataModelSchema { get; } = new JsonPartConverter("/DataModelSchema");
#if NETFRAMEWORK
        public IPowerBIPartConverter<JObject> DataModel { get; }
#endif
        public IPowerBIPartConverter<JObject> Connections { get; } = new JsonPartConverter("/Connections", Encoding.UTF8);
        public IPowerBIPartConverter<byte[]> CustomVisuals { get; } = new BytesPartConverter("/Report/CustomVisuals");
        public IPowerBIPartConverter<byte[]> StaticResources { get; } = new BytesPartConverter("/Report/StaticResources");


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
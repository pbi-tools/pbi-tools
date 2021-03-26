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
    public class PowerBILegacyPartConverters
    {

        public IPowerBIPartConverter<JObject> ReportSettings { get; } = new BinarySerializationConverter<Microsoft.PowerBI.Packaging.Storage.ReportSettings>();
        
        public IPowerBIPartConverter<JObject> ReportMetadata { get; } = new BinarySerializationConverter<Microsoft.PowerBI.Packaging.Storage.ReportMetadataContainer>();
        
        public IPowerBIPartConverter<MashupParts> DataMashup { get; } = new MashupConverter();

    }

    // ReSharper disable once InconsistentNaming
    public class PowerBIPartConverters
    {
        public PowerBIPartConverters(string modelName, IDependenciesResolver resolver)
        {
            if (modelName == null) throw new ArgumentNullException(nameof(modelName));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            this.DataModel = new DataModelConverter(modelName, resolver);
        }

        public IPowerBIPartConverter<string> Version { get; } = new StringPartConverter();
        public IPowerBIPartConverter<JObject> ReportSettingsV3 { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> ReportMetadataV3 { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<XDocument> LinguisticSchema { get; } = new XmlPartConverter();
        public IPowerBIPartConverter<JObject> LinguisticSchemaV3 { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> DiagramViewState { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> DiagramLayout { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> ReportDocument { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> DataModelSchema { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> DataModel { get; }
        public IPowerBIPartConverter<JObject> Connections { get; } = new JsonPartConverter(Encoding.UTF8); // TODO Verify encoding
        public IPowerBIPartConverter<byte[]> Resources { get; } = new BytesPartConverter(); // TODO Different API required here?

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
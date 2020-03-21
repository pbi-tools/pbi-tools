using System;
using System.Text;
using System.Xml.Linq;
using Microsoft.PowerBI.Client.Windows;
using Newtonsoft.Json.Linq;
using PbiTools.Model;
using PbiTools.Utils;

namespace PbiTools.PowerBI
{
    // ReSharper disable once InconsistentNaming
    public class PowerBIPartConverters
    {
        public PowerBIPartConverters(string modelName, IDependenciesResolver resolver)
        {
            if (modelName == null) throw new ArgumentNullException(nameof(modelName));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            this.DataModel = new DataModelConverter(modelName, resolver);
        }

        #region Legacy

        public IPowerBIPartConverter<JObject> ReportSettings { get; } = new BinarySerializationConverter<ReportSettings>();
        public IPowerBIPartConverter<JObject> ReportMetadata { get; } = new BinarySerializationConverter<ReportMetadata>();
        public IPowerBIPartConverter<MashupParts> DataMashup { get; } = new MashupConverter();
        public IPowerBIPartConverter<XDocument> LinguisticSchema { get; } = new XmlPartConverter();
        
        #endregion

        public IPowerBIPartConverter<string> Version { get; } = new StringPartConverter();
        public IPowerBIPartConverter<JObject> ReportSettingsV3 { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> ReportMetadataV3 { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> LinguisticSchemaV3 { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> DiagramViewState { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> DiagramLayout { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> ReportDocument { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> DataModelSchema { get; } = new JsonPartConverter();
        public IPowerBIPartConverter<JObject> DataModel { get; }
        public IPowerBIPartConverter<JObject> Connections { get; } = new JsonPartConverter(Encoding.UTF8); // TODO Verify encoding
        public IPowerBIPartConverter<byte[]> Resources { get; } = new BytesPartConverter(); // TODO Different API required here?

    }
}
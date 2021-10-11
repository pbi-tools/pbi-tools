// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace PbiTools.Serialization
{
    using FileSystem;
    using Model;
    using ProjectSystem;

    public class PowerBIPartSerializers
    {
        public PowerBIPartSerializers(IProjectRootFolder rootFolder, PbixProjectSettings settings)
        {
            if (rootFolder is null) throw new ArgumentNullException(nameof(rootFolder));

            this.Version = new StringPartSerializer(rootFolder, nameof(Version));
            this.CustomVisuals = new ResourcesSerializer(rootFolder, nameof(CustomVisuals));
            this.StaticResources = new ResourcesSerializer(rootFolder, nameof(StaticResources));
            this.Connections = new JsonPartSerializer(rootFolder, nameof(Connections));
            this.ReportMetadata = new JsonPartSerializer(rootFolder, nameof(ReportMetadata));
            this.ReportSettings = new JsonPartSerializer(rootFolder, nameof(ReportSettings));
            this.DiagramViewState = new JsonPartSerializer(rootFolder, nameof(DiagramViewState));
            this.DiagramLayout = new JsonPartSerializer(rootFolder, nameof(DiagramLayout));
            this.LinguisticSchema = new JsonPartSerializer(rootFolder, nameof(LinguisticSchema));
            this.LinguisticSchemaXml = new XmlPartSerializer(rootFolder, nameof(LinguisticSchema));

            this.DataModel = new TabularModelSerializer(rootFolder, settings.Model);
            this.ReportDocument = new ReportSerializer(rootFolder);
#if NETFRAMEWORK
            this.DataMashup = new MashupSerializer(rootFolder, settings.Mashup);
#endif
        }



        public IPowerBIPartSerializer<string> Version { get; }
        public IPowerBIPartSerializer<JObject> ReportSettings { get; }
        public IPowerBIPartSerializer<JObject> ReportMetadata { get; }
        public IPowerBIPartSerializer<JObject> LinguisticSchema { get; }
        public IPowerBIPartSerializer<XDocument> LinguisticSchemaXml { get; }
        public IPowerBIPartSerializer<JObject> DiagramViewState { get; }
        public IPowerBIPartSerializer<JObject> DiagramLayout { get; }
        public IPowerBIPartSerializer<JObject> ReportDocument { get; }
        public IPowerBIPartSerializer<JObject> DataModel { get; }
        public IPowerBIPartSerializer<JObject> Connections { get; }
        public IPowerBIPartSerializer<IDictionary<string, byte[]>> CustomVisuals { get; }
        public IPowerBIPartSerializer<IDictionary<string, byte[]>> StaticResources { get; }
#if NETFRAMEWORK
        public IPowerBIPartSerializer<MashupParts> DataMashup { get; }
#endif

    }
}
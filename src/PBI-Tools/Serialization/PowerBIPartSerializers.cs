// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.PowerBI.Packaging;
using Newtonsoft.Json.Linq;
using PbiTools.FileSystem;

namespace PbiTools.Serialization
{
    public class PowerBIPartSerializers
    {
        public PowerBIPartSerializers(IProjectRootFolder rootFolder)
        {
            if (rootFolder is null) throw new ArgumentNullException(nameof(rootFolder));

            this.Version = new StringPartSerializer(rootFolder, nameof(IPowerBIPackage.Version));
            this.CustomVisuals = new ResourcesSerializer(rootFolder, nameof(IPowerBIPackage.CustomVisuals));
            this.StaticResources = new ResourcesSerializer(rootFolder, nameof(IPowerBIPackage.StaticResources));
            this.Connections = new JsonPartSerializer(rootFolder, nameof(IPowerBIPackage.Connections));
            this.ReportMetadata = new JsonPartSerializer(rootFolder, nameof(IPowerBIPackage.ReportMetadata));
            this.ReportSettings = new JsonPartSerializer(rootFolder, nameof(IPowerBIPackage.ReportSettings));
            this.DiagramViewState = new JsonPartSerializer(rootFolder, nameof(IPowerBIPackage.DiagramViewState));
            this.DiagramLayout = new JsonPartSerializer(rootFolder, nameof(IPowerBIPackage.DiagramLayout));
            this.LinguisticSchema = new JsonPartSerializer(rootFolder, nameof(IPowerBIPackage.LinguisticSchema));
            this.LinguisticSchemaXml = new XmlPartSerializer(rootFolder, nameof(IPowerBIPackage.LinguisticSchema));

            this.DataModel = new TabularModelSerializer(rootFolder);
            this.ReportDocument = new ReportSerializer(rootFolder);
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

    }
}
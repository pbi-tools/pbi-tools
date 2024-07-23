/*
 * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.
 * Copyright (C) 2018 Mathias Thierbach
 *
 * pbi-tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * pbi-tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * A copy of the GNU Affero General Public License is available in the LICENSE file,
 * and at <https://goto.pbi.tools/license>.
 */

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
            this.ReportMobileState = new MobileStateSerializer(rootFolder);
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
        public IPowerBIPartSerializer<JObject> ReportMobileState { get; }
        public IPowerBIPartSerializer<JObject> DataModel { get; }
        public IPowerBIPartSerializer<JObject> Connections { get; }
        public IPowerBIPartSerializer<IDictionary<string, byte[]>> CustomVisuals { get; }
        public IPowerBIPartSerializer<IDictionary<string, byte[]>> StaticResources { get; }
#if NETFRAMEWORK
        public IPowerBIPartSerializer<MashupParts> DataMashup { get; }
#endif

    }
}

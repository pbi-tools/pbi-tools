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
        public IPowerBIPartConverter<JObject> ReportMobileState { get; } = new JsonPartConverter("/Report/MobileState");

        public IPowerBIPartConverter<JObject> DataModelSchema { get; } = new JsonPartConverter("/DataModelSchema");
#if NETFRAMEWORK
        public IPowerBIPartConverter<JObject> DataModel { get; }
#endif
        public IPowerBIPartConverter<JObject> Connections { get; } = new JsonPartConverter("/Connections", Encoding.UTF8);
        public IPowerBIPartConverter<byte[]> CustomVisuals { get; } = new BytesPartConverter("/Report/CustomVisuals");
        public IPowerBIPartConverter<byte[]> StaticResources { get; } = new BytesPartConverter("/Report/StaticResources");


        // TODO CustomProperties new Uri("/docProps/custom.xml", UriKind.Relative)

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

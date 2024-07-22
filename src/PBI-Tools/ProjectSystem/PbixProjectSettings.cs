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

using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PbiTools.ProjectSystem
{

    /* Config sources: (priority)
    1 cmd-line
    2 .pbixproj.json (cwd, parent, root, UserProfile)
    3 ENV ("PBITOOLS_")   
    */

    public interface IHasDefaultValue
    { 
        bool IsDefault { get; }
    }


    public class PbixProjectSettings : IHasDefaultValue
    {
        [JsonProperty("model")]
        public ModelSettings Model { get; set; } = new ModelSettings();

        [JsonProperty("mashup")]
        public MashupSettings Mashup { get; set; } = new MashupSettings();

        [JsonProperty("report")]
        public ReportSettings Report { get; set; } = new ReportSettings();

        public bool IsDefault => 
            Model.IsDefault() 
            && Mashup.IsDefault()
            && Report.IsDefault()
            ;

#region Json Serialization Support
        [OnSerializing]
        private void HideDefaultValuesOnSerializing(StreamingContext context)
        {
            if (Model.IsDefault()) Model = null;
            if (Mashup.IsDefault()) Mashup = null;
            if (Report.IsDefault()) Report = null;
        }

        [OnSerialized]
        private void ResetDefaultValuesAfterSerializing(StreamingContext context)
        {
            if (Model == null) Model = new();
            if (Mashup == null) Mashup = new();
            if (Report == null) Report = new();
        }
#endregion

        public void Save(string path)
        {
            var json = JObject.FromObject(this, JsonSerializer.Create(PbixProject.DefaultJsonSerializerSettings));

            using (var writer = new JsonTextWriter(File.CreateText(path)))
            {
                writer.Formatting = Formatting.Indented;
                json.WriteTo(writer);
            }
        }
    }

}

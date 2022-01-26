// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PowerArgs;

namespace PbiTools.ProjectSystem
{
    public enum ModelSerializationMode
    {
        [ArgDescription("Serializes the tabular model into a standard folder structure and performs various transformations to optimize file contents for source control.")]
        Default = 1,
        [ArgDescription("Serializes the tabular model into a single JSON file containing the full TMSL payload from the PBIX model.")]
        Raw = 2
    }

    public class ModelSettings
    {
        [JsonIgnore]
        public bool IsDefault => _ignoreProperties == null
            && SerializationMode == ModelSerializationMode.Default;

        private static readonly string[] DefaultIgnoreProperties = new [] {
            "modifiedTime", "refreshedTime", "lastProcessed", "structureModifiedTime", 
            "lastUpdate", "lastSchemaUpdate", "createdTimestamp", "lineageTag"
        };

        [JsonProperty("ignoreProperties", NullValueHandling = NullValueHandling.Ignore)]
        private string[] _ignoreProperties;
        
        [JsonIgnore]
        public string[] IgnoreProperties 
        {
            get => _ignoreProperties ?? DefaultIgnoreProperties; 
            set => _ignoreProperties = value;
        }

        [JsonProperty("serializationMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ModelSerializationMode SerializationMode { get; set; } = ModelSerializationMode.Default;
    }


}

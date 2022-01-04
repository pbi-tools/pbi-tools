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

    public class ModelSettings : IHasDefaultValue
    {
        [JsonProperty("serializationMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ModelSerializationMode SerializationMode { get; set; } = ModelSerializationMode.Default;

        [JsonIgnore]
        public bool IsDefault => 
            _ignoreProperties == null
            && SerializationMode == ModelSerializationMode.Default
            && Annotations.IsDefault();

        #region IgnoreProperties

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

        #endregion

        [JsonProperty("annotations", NullValueHandling = NullValueHandling.Ignore)]
        public ModelAnnotationSettings Annotations { get; set; } = new ModelAnnotationSettings();
    }

    public class ModelAnnotationSettings : IHasDefaultValue
    {
        /// <summary>
        /// All TOM object annotations to be ignored when serializing.
        /// Wildcards ("*", "?") are supported.
        /// </summary>
        [JsonProperty("exclude", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Exclude { get; set; }

        /// <summary>
        /// Exeptions to the 'exclude' rule. Annotations listed here are not excluded.
        /// Wildcards ("*", "?") are supported.
        /// </summary>
        [JsonProperty("include", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Include { get; set; }

        [JsonIgnore]
        public bool IsDefault => Exclude.IsDefault() && Include.IsDefault();
    }

}

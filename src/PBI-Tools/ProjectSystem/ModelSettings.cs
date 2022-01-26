// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using PowerArgs;

namespace PbiTools.ProjectSystem
{
    public enum ModelSerializationMode
    {
        [ArgDescription("Serializes the tabular model into the default PbixProj folder structure and performs various transformations to optimize file contents for source control.")]
        Default = 1,
        [ArgDescription("Serializes the tabular model into a single JSON file containing the full TMSL payload from the PBIX model. No transformation are applied.")]
        Raw = 2,
        // [ArgDescription("Serializes the tabular model into a single JSON file containing the full TMSL payload from the PBIX model. All transformations are applied.")]
        // SingleFile = 3,
        // [ArgDescription("Serializes the tabular model into Tabular Editor's Save-to-folder format.")]
        // TabularEditor = 4,
    }

    public class ModelSettings : IHasDefaultValue
    {
        /* TODO Support all TOM SerializeOptions:
            public bool IgnoreInferredProperties { get; set; }
            public bool IgnoreInferredObjects { get; set; }
            public bool IgnoreChildren { get; set; }
            public bool IgnoreChildrenExceptAnnotations { get; set; }
            public bool IgnoreTimestamps { get; set; }
            public bool PartitionsMergedWithTable { get; set; }
            public bool SplitMultilineStrings { get; set; }
            public bool IncludeTranslatablePropertiesOnly { get; set; }
            public bool IncludeRestrictedInformation { get; set; }        
        */

        [JsonProperty("serializationMode")]
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

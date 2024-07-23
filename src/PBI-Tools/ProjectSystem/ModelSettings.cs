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

using System.Runtime.Serialization;
using Microsoft.AnalysisServices.Tabular.Serialization;
using Newtonsoft.Json;
using PowerArgs;

namespace PbiTools.ProjectSystem
{
    using Configuration;

    public enum ModelSerializationMode
    {
        [ArgDescription("The default serialization format, effective if no option is specified. The default is TMDL.")]
        Default = 1,
        [ArgDescription("Serializes the tabular model into a single JSON file containing the full TMSL payload from the PBIX model. No transformations are applied.")]
        Raw = 2,
        [ArgDescription("Serializes the tabular model into the default PbixProj folder structure and performs various transformations to optimize file contents for source control.")]
        Legacy = 3,
        [ArgDescription("Serializes the tabular model into TMDL format. Annotation settings are applied.")]
        Tmdl = 4,

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
        public ModelSerializationMode SerializationMode { get; set; } = AppSettings.DefaultModelSerializationMode ?? ModelSerializationMode.Default;

        [JsonIgnore]
        public bool IsDefault => 
            _ignoreProperties == null
            && SerializationMode == ModelSerializationMode.Default
            && Annotations.IsDefault()
            && Formatting.IsDefault()
            && Measures.IsDefault()
            && ExcludeChildrenMetadata == null
            && IncludeRestrictedInformation == null
            && MetadataOrderHints == null
            && ExpressionTrimStyle == null;

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

        /// <summary>
        /// Serialization settings for TOM annotations.
        /// </summary>
        [JsonProperty("annotations", NullValueHandling = NullValueHandling.Ignore)]
        public ModelAnnotationSettings Annotations { get; set; } = new();

        /// <summary>
        /// Serialization settings for measures. Applies to PbixProj format only.
        /// </summary>
        [JsonProperty("measures", NullValueHandling = NullValueHandling.Ignore)]
        public ModelMeasureSettings Measures { get; set; } = new();

        /// <summary>
        /// Defines formatting options for the TMDL serializer.
        /// </summary>
        [JsonProperty("formatting", NullValueHandling = NullValueHandling.Ignore)]
        public ModelFormattingSettings Formatting { get; set; } = new();

        /// <summary>
        /// If set, only the root object will be serialized.
        /// </summary>
        [JsonProperty("excludeChildrenMetadata", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ExcludeChildrenMetadata { get; set; }

        /// <summary>
        /// If set, restricted information is included when serializing.
        /// </summary>
        [JsonProperty("includeRestrictedInformation", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IncludeRestrictedInformation { get; set; }

        /// <summary>
        /// Sets an indication that metadata-order hints should be included in the generated TMDL content.
        /// </summary>
        [JsonProperty("metadataOrderHints", NullValueHandling = NullValueHandling.Ignore)]
        public bool? MetadataOrderHints { get; set; }

        /// <summary>
        /// The trimming style for TMDL expression whitespace.
        /// See <a href="https://docs.microsoft.com/dotnet/api/microsoft.analysisservices.tabular.tmdl.tmdlexpressiontrimstyle">here</a> for further details.
        /// </summary>
        [JsonProperty("expressionTrimStyle", NullValueHandling = NullValueHandling.Ignore)]
        public Microsoft.AnalysisServices.Tabular.Tmdl.TmdlExpressionTrimStyle? ExpressionTrimStyle { get; set; }


#region Json Serialization Support
        [OnSerializing]
        private void HideDefaultValuesOnSerializing(StreamingContext context)
        {
            if (Annotations.IsDefault()) Annotations = null;
            if (Formatting.IsDefault()) Formatting = null;
            if (Measures.IsDefault()) Measures = null;
        }

        [OnSerialized]
        private void ResetDefaultValuesAfterSerializing(StreamingContext context)
        {
            if (Annotations == null) Annotations = new();
            if (Formatting == null) Formatting = new();
            if (Measures == null) Measures = new();
        }
#endregion
    }

    public class ModelFormattingSettings : IHasDefaultValue
    {
        /// <summary>
        /// Sets the encoding to use when serializing to TMDL. Default: 'utf-8'.
        /// </summary>
        [JsonProperty("encoding", NullValueHandling = NullValueHandling.Ignore)]
        public string Encoding { get; set; } = System.Text.Encoding.UTF8.BodyName;

        internal System.Text.Encoding GetEncoding() => System.Text.Encoding.GetEncoding(Encoding);

        /// <summary>
        /// Controls how line breaks are emitted.
        /// Supported values are <c>SystemDefault</c>, <c>WindowsStyle</c>, <c>UnixStyle</c>.
        /// </summary>
        [JsonProperty("newLineStyle", NullValueHandling = NullValueHandling.Ignore)]
        public NewLineStyle NewLineStyle { get; set; } = default;

        /// <summary>
        /// Controls the indentation mode.
        /// Supported values are <c>Default</c>, <c>Tabs</c>, <c>Spaces</c>.
        /// </summary>
        [JsonProperty("indentationMode", NullValueHandling = NullValueHandling.Ignore)]
        public IndentationMode IndentationMode { get; set; } = default;

        /// <summary>
        /// A whole number defining the number of columns used for each indentation level.
        /// </summary>
        [JsonProperty("indentationSize", NullValueHandling = NullValueHandling.Ignore)]
        public int IndentationSize { get; set; } = 2;

        [JsonIgnore]
        public bool IsDefault =>
            Encoding == System.Text.Encoding.UTF8.BodyName
            && NewLineStyle == default
            && IndentationMode == default
            && IndentationSize == 2;

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

    public class ModelMeasureSettings : IHasDefaultValue
    {
        [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
        public ModelMeasureSerializationFormat Format { get; set; } = ModelMeasureSerializationFormat.Json;

        /// <summary>
        /// Determines whether to extract measure expressions into separate *.dax files when using the <c>Json</c>
        /// serialization format. The default is <c>true</c>.
        /// </summary>
        [JsonProperty("extractExpression", NullValueHandling = NullValueHandling.Ignore)]
        public bool ExtractExpression { get; set; } = true;

        [JsonIgnore]
        public bool IsDefault => Format == ModelMeasureSerializationFormat.Json && ExtractExpression;
    }

    public enum ModelMeasureSerializationFormat
    { 
        Json = 1,
        Xml = 2
    }

}

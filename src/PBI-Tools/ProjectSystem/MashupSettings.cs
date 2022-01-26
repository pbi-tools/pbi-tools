// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PowerArgs;

namespace PbiTools.ProjectSystem
{
    public enum MashupSerializationMode
    {
        [ArgDescription("Similar to 'Raw' mode, with the exception that QueryGroups are extracted into a separate file for readability.")]
        Default = 1,
        [ArgDescription("Serializes all Mashup parts with no transformations applied.")]
        Raw = 2,
        [ArgDescription("Serializes the Mashup metadata part into a Json document, and embedded M queries into separate files. This mode is not supported for compilation.")]
        Expanded = 3
    }

    public class MashupSettings : IHasDefaultValue
    {
        [JsonIgnore]
        public bool IsDefault => SerializationMode == MashupSerializationMode.Default;

        [JsonProperty("serializationMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public MashupSerializationMode SerializationMode { get; set; } = MashupSerializationMode.Default;
    }


}

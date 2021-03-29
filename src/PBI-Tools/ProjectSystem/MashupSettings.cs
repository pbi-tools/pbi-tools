// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PowerArgs;

namespace PbiTools.ProjectSystem
{
    public enum MashupSerializationMode
    {
        [ArgDescription("TODO")]
        Default = 1,
        [ArgDescription("TODO")]
        Raw = 2
    }

    public class MashupSettings
    {
        [JsonIgnore]
        public bool IsDefault => SerializationMode == MashupSerializationMode.Default;

        [JsonProperty("serializationMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public MashupSerializationMode SerializationMode { get; set; } = MashupSerializationMode.Default;
    }


}

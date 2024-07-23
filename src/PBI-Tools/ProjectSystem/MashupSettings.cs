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

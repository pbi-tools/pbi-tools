// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace PbiTools.ProjectSystem
{

    /* Config sources: (priority)
    1 cmd-line
    2 .pbixproj.json (cwd, parent, root, UserProfile)
    3 ENV ("PBITOOLS_")   
    */

    public class PbixProjectSettings
    {
        [JsonProperty("model")]
        public ModelSettings Model { get; set; } = new ModelSettings();

        [JsonProperty("mashup")]
        public MashupSettings Mashup { get; set; } = new MashupSettings();

        public bool IsDefault() => 
            (Model == null || Model.IsDefault) 
            && (Mashup == null || Mashup.IsDefault);

    }

}

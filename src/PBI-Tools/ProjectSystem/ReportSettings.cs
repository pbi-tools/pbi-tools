// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace PbiTools.ProjectSystem
{
    public class ReportSettings : IHasDefaultValue
    {
        [JsonIgnore]
        public bool IsDefault => true;

    }


}

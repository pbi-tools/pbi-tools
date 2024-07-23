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

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PbiTools.Utils
{

    public class PowerBIDesktopInstallation
    {
        public string ProductVersion { get; set; }
        [JsonIgnore]
        public Version Version { get; set; }
        public bool Is64Bit { get; set; }
        public string InstallDir { get; set; }
        public string SettingsDir { get; set; }
        public PowerBIDesktopInstallationLocation Location { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PowerBIDesktopInstallationLocation
    {
        WindowsStore,
        Installer,
        Custom
    }
}

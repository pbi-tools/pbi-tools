// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
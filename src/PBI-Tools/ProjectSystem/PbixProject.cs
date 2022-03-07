// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace PbiTools.ProjectSystem
{
    using FileSystem;
    using Utils;

    public class PbixProject
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixProject>();

        public const string Filename = ".pbixproj.json";
        public static readonly Version CurrentVersion = Version.Parse("0.11");

        /*
         * PBIXPROJ Change Log
         * ===================
         * 0.0   - Initial version (Mashup, Model, Report, CustomVisuals, StaticResources)
         * 0.1   - Model/dataSources: use location (query name) as folder name (rather than datasource guid); always write 'dataSource.json'
         *       - FIX: use static name inside dataSource.json
         * 0.2   - "dataSources" renamed to "queries"
         *       - all PBIX parts extracted
         *       - '/Mashup/Package/Formulas/Section1.m' rather than '/Mashup/Section1.m' (package fully extracted)
         * 0.3   - '/Mashup/Metadata/**' (instead of '/Mashup/metadata.xml')
         *       - extract exports (queries) from mashup package into individual .m files
         *         - '/Mashup/Package/Formulas/Section1.m/*.m' (instead of '/Mashup/Package/Formulas/Section1.m')
         *       - excluding Report/visualContainers/queryHash, Report/section/objectId, Report/report/objectId to eliminate insignificant noise in source controlled files
         * 0.3.1 - Supports /Model/tables[]/measures: { "extendedProperties": [] }
         * 0.4   - excluding Report/section/id (field is volatile and 'name' is already a unique identifier for sections)
         * 0.4.1 - Measure names are url-encoded to account for characters not allowed in paths
         * 0.5   - Support for V3 Model (Mar-2020 Release)
         *       - /Model/tables/{name}/{name}.json is now /Model/tables/{name}/table.json
         *       - V3 models: /Mashup no longer serialized
         *       - V3 models: /Model/queries folder added
         *       - Added 'created' and 'lastModified' properties
         * 0.6   - Extract model columns into /Model/tables/{name}/columns/{name}.json|dax
         *       - Extract calculated table expressions into /Model/tables/{name}/table.dax
         *       - Extract measure expression into /Model/tables/{table}/measures/{name}.dax
         *       - Extract model cultures in /Model/cultures/{name}.json
         *       - Control Model serialization settings via settings.model in pbixproj file (Serialization Mode, Ignore Properties)
         * 0.7   - /Report: section and visualContainer folder names
         * 0.8   - /Mashup extracted from V3 models (when present in PBIX/PBIT)
         * 0.9   - (Released in 1.0.0-beta.5)
         *       - Mashup serialization modes: Default, Raw, Expanded.
         *       - BREAKING CHANGE: 'Expanded' is now considered legacy and no longer the default serialization mode. (The `compile-pbix` action only supports projects extracted using the _Default_ or _Raw_ Mashup serialization mode.)
         * 0.10  - (Released wit 1.0.0-beta.8)
         *       - Supports 'custom' token (ignored by pbi-tools, but available to external integrations)
         *       - #48 Breaking: 'nameConflict' moved into deployments/options/import
         *       - #48 Breaking: 'workspaceId' is now 'workspace' in deployments/environment
         *       - #48 New: Optional 'description' in deployment profile
         *       - #19 New Model settings: settings/model/annotations (exclude, include)
         * 0.11  - (Released with 1.0.0-rc.1)
         *       - #96 New Model settings: settings/model/measures (format, extractExpression)
         *       - #96 BREAKING CHANGE: Measures json format now default
         *       - #90 Always serialize (partial) partitions payload, ensuring 'queryGroup' property is retained
         *       - #19 Do not serialize empty model/annotations[]
         *       - #85 Visuals with titles only differing in casing are now extracted into unique folders
         *       - #91 Support for /Report/mobileState (mobileState.json, explorationState.json)
         */


        internal static readonly JsonSerializerSettings DefaultJsonSerializerSettings = new JsonSerializerSettings
        {
            DateFormatString = "yyyy-MM-ddTHH:mm:ssK",
            Formatting = Formatting.Indented,
            //ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            // don't use CamelCaseContractResolver as we need to maintain casing in query names, custom settings, and deployment manifest labels
            DefaultValueHandling = DefaultValueHandling.Ignore
        }.WithConverters(new StringEnumConverter());

        #region Version

        [JsonProperty("version")]
        private string versionString;

        [JsonIgnore]
        public Version Version
        {
            get => Version.TryParse(versionString, out var version) ? version : CurrentVersion;
            set => this.versionString = value.ToString();
        }

        #endregion

        #region Timestamps

        [JsonProperty("created")]
        public DateTimeOffset Created { get; set; }

        [JsonProperty("lastModified")]
        public DateTimeOffset LastModified { get; set; }

        #endregion

        #region Legacy

        [JsonProperty("queries", NullValueHandling = NullValueHandling.Ignore)] // Only needed for legacy models
        public IDictionary<string, string> Queries { get; set; }

        #endregion

        #region Settings

        [JsonProperty("settings", NullValueHandling = NullValueHandling.Ignore)]
        public PbixProjectSettings Settings { get; set; } = new PbixProjectSettings();

        #endregion

        #region Custom

        [JsonProperty("custom", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Custom { get; set; }

        #endregion

        #region Deployments

        [JsonProperty("deployments", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, JToken> Deployments { get; set; }

        #endregion

        public PbixProject()
        {
            this.Version = CurrentVersion;
            this.Created = DateTimeOffset.Now;
        }


        [JsonIgnore]
        internal string OriginalPath { get; private set; }

        public static PbixProject FromFolder(IProjectRootFolder folder)
        {
            var file = folder.GetFile(Filename);
            if (file.TryReadFile(out Stream stream))
            {
                Log.Information("Reading PBIXPROJ settings from: {Path}", file.Path);

                try
                {
                    var proj = FromStream(stream);
                    proj.OriginalPath = file.Path;
                    return proj;
                    // at this stage we could perform version compatibility checks
                }
                catch (JsonReaderException e)
                {
                    Log.Warning(e, "Failed to read PBIXPROJ file from {Path}. Using default settings.", file.Path);
                }
            }

            Log.Debug("No existing or invalid PBIXPROJ file found at {Path}. Generating new project file.", file.Path);

            return new() { OriginalPath = file.Path };
        }


        /// <summary>
        /// Loads the <see cref="PbixProject"/> metadata file from the specified folder, using the default filename ".pbixproj.json".
        /// Creates a new, blank, instance if the file doesn't exist or is invalid.
        /// </summary>
        public static PbixProject FromFolder(string path)
            => FromFile(Path.Combine(path, Filename));


        /// <summary>
        /// Loads the <see cref="PbixProject"/> metadata file from the specified file.
        /// Creates a new, blank, instance if the file doesn't exist or is invalid.
        /// </summary>
        public static PbixProject FromFile(string path)
        { 
            if (File.Exists(path))
            {
                Log.Information("Reading PBIXPROJ settings from: {Path}", path);
                
                using var stream = File.OpenRead(path);
                try
                {
                    var proj = FromStream(stream);
                    proj.OriginalPath = new FileInfo(path).FullName;
                    return proj;
                    // at this stage we could perform version compatibility checks
                }
                catch (JsonReaderException e)
                {
                    Log.Warning(e, "Failed to read PBIXPROJ file from {Path}. Using default settings.", path);
                }
            }

            Log.Debug("No existing or invalid PBIXPROJ file found at {Path}. Generating new project file.", path);

            return new() { OriginalPath = new FileInfo(path).FullName };
        }

        public static PbixProject FromStream(Stream fileStream)
        { 
            using (var reader = new StreamReader(fileStream))
            {
                return JsonConvert.DeserializeObject<PbixProject>(reader.ReadToEnd(), DefaultJsonSerializerSettings) ?? new();
            }
        }


        public void Save(IProjectRootFolder folder, bool setModified = false)
        {
            var json = JObject.FromObject(this, JsonSerializer.Create(DefaultJsonSerializerSettings));
            if (this.Settings.IsDefault()) json.Remove("settings");

            if (setModified) this.LastModified = DateTimeOffset.UtcNow;

            folder.GetFile(Filename).Write(json);
        }

        /// <summary>
        /// Saves the project file to the given path, or overwrites the original path if <c>null</c> is specified.
        /// </summary>
        public void Save(string path = null, bool setModified = false)
        {
            var json = JObject.FromObject(this, JsonSerializer.Create(DefaultJsonSerializerSettings));
            if (this.Settings.IsDefault()) json.Remove("settings");

            if (setModified) this.LastModified = DateTimeOffset.UtcNow;

            using var writer = new JsonTextWriter(File.CreateText(path ?? this.OriginalPath));
            writer.Formatting = Formatting.Indented;
            json.WriteTo(writer);
        }


        /// <summary>
        /// Determines the default project folder location for the given PBIX file path.
        /// </summary>
        public static string GetDefaultProjectFolderForFile(string pbixPath) =>
            Path.Combine(
                Path.GetDirectoryName(pbixPath),
                Path.GetFileNameWithoutExtension(pbixPath)
            ); // TODO make this configurable

        public static bool IsPbixProjFolder(string path) =>
            File.Exists(Path.Combine(path, Filename));

        public static string GetDefaultPath(string folder) =>
            new FileInfo(Path.Combine(folder, Filename)).FullName;
    }
}
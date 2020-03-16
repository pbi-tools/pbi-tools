using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PbiTools.FileSystem;
using Serilog;

namespace PbiTools.ProjectSystem
{
    public class PbixProject
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixProject>();

        public static readonly string Filename = ".pbixproj.json";
        public static readonly Version CurrentVersion = Version.Parse("0.5");

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
         */

        /* Entries to add later: */
        // Settings (Serialization)
        // Deployments
        // CustomProperties

        #region Version

        [JsonProperty("version")]
        public string VersionString { get; set; }

        [JsonIgnore]
        public Version Version
        {
            get => Version.TryParse(VersionString, out var version) ? version : CurrentVersion;
            set => this.VersionString = value.ToString();
        }

        #endregion

        [JsonProperty("queries")]
        public IDictionary<string, string> Queries { get; set; }


        public PbixProject()
        {
            this.Version = CurrentVersion;
            this.Queries = new Dictionary<string, string>();
        }


        public static PbixProject FromFolder(IProjectRootFolder folder)
        {
            var file = folder.GetFile(Filename);
            if (file.TryGetFile(out Stream stream))
            {
                using (var reader = new StreamReader(stream))
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<PbixProject>(reader.ReadToEnd());
                        // at this stage we could perform version compatibility checks
                    }
                    catch (JsonReaderException e)
                    {
                        Log.Error(e, "Failed to read PBIXPROJ file from {Path}", file.Path);
                    }
                }
            }

            return new PbixProject();
        }

        public void Save(IProjectRootFolder folder)
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented); // don't use CamelCaseContractResolver as it will modify query names

            folder.GetFile(Filename).Write(json);
        }
    }
}
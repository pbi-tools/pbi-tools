using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace PbixTools
{
    public class PbixProject
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixProject>();

        public static readonly string Filename = ".pbixproj.json";
        public static readonly Version CurrentVersion = new Version(0, 2);

        /*
         * PBIXPROJ Change Log
         * ===================
         * 0.0 - Initial version (Mashup, Model, Report, CustomVisuals, StaticResources)
         * 0.1 - Model/dataSources: use location (query name) as folder name (rather than datasource guid); always write 'dataSource.json'
         *     - FIX: use static name inside dataSource.json
         * 0.2 - "dataSources" renamed to "queries"
         *     - '/Mashup/Package/Formulas/Section1.m' rather than '/Mashup/Section1.m' (package fully extracted)
         */

        /* Entries to add later: */
        // Settings
        // Deployments

        [JsonProperty("version")]
        public string Version { get; set; }
        [JsonProperty("queries")]
        public IDictionary<string, string> Queries { get; set; }


        public PbixProject()
        {
            this.Version = CurrentVersion.ToString();
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
            this.Version = CurrentVersion.ToString(); // making sure we're always upgrading to the latest version number

            var json = JsonConvert.SerializeObject(this, Formatting.Indented); // don't use CamelCaseContractResolver as it will modify query names

            folder.GetFile(Filename).Write(json);
        }
    }
}
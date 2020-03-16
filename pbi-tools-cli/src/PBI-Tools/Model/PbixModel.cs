using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using PbiTools.PowerBI;
using PbiTools.ProjectSystem;
using PbiTools.Utils;
using Serilog;

namespace PbiTools.Model
{
    /// <summary>
    /// An in-memory representation of the contents of a PBIX file.
    /// </summary>
    public interface IPbixModel
    {
        JObject Connections { get; }
        JObject DataModel { get; }
        // MashupParts Mashup { get; }          // TODO Drop in V3?
        JObject Report { get; }
        JObject DiagramViewState { get; }
        JObject DiagramLayout { get; }
        JObject LinguisticSchema { get; }  // TODO Change to Json in V3?
        JObject ReportMetadata { get; }
        JObject ReportSettings { get; }
        string Version { get; }

        IDictionary<string, byte[]> CustomVisuals { get; }
        IDictionary<string, byte[]> StaticResources { get; }

        PbixProject PbixProj { get; }
        PbixModelSource Type { get; }
    }

    /* Workflow: Extract PBIX
     * 1 Create PbixModel from .pbix
     * 2 Serialize PbixModel to folder
     * 2.1 Read existing PbixProject from folder
     * 2.2 Replace query ids accordingly
     */

    public enum PbixModelSource
    {
        PowerBIPackage,
        PbixProjFolder,
        LiveSession
    }

    public class PbixModel : IPbixModel, IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixModel>();

        public PbixModelSource Type { get; private set; }
        public string SourcePath { get; private set; }

        private PbixModel()
        {
        }

        public static PbixModel FromFile(string path, IDependenciesResolver dependenciesResolver)
        {
            var pbixModel = new PbixModel { Type = PbixModelSource.PowerBIPackage, SourcePath = path };

            using (var reader = new PbixReader(path, dependenciesResolver))
            {
                pbixModel.Connections = reader.ReadConnections();
                // pbixModel.Mashup = reader.ReadMashup();
                pbixModel.Report = reader.ReadReport();
                pbixModel.DiagramViewState = reader.ReadDiagramViewState();
                pbixModel.LinguisticSchema = reader.ReadLinguisticSchemaV3();
                pbixModel.ReportMetadata = reader.ReadReportMetadataV3();
                pbixModel.ReportSettings = reader.ReadReportSettingsV3();
                pbixModel.Version = reader.ReadVersion();
                pbixModel.CustomVisuals = reader.ReadCustomVisuals();
                pbixModel.StaticResources = reader.ReadStaticResources();

                pbixModel.DataModel = reader.ReadDataModel();  // will fire up SSAS instance if PBIX has embedded model
            }

            pbixModel.PbixProj = new PbixProject();  // TODO Init 'Queries'?

            return pbixModel;
        }

        public static PbixModel FromFolder(string path)
        {
            // PBIXPROJ(folder) <==> Serializer <=|PbixModel|=> PbixReader|Writer[Converter] <==> PBIX(file)
            //                       ##########                 ############################

            throw new NotImplementedException();
        }

        public void ToFolder(string path)
        {
            // get or create directory (ProjectRootFolder)
            // read PbixProj (for format version and query ids)

            /*
               ./Mashup/
                      ./Section1.m
                      ./Package/
                              ./Formulas
                                       ./Section1.m
                      ./Permissions.json
                      ./Metadata.xml
                      ./Content/
               ./Report/
               ./Model/
                   ./queries/
                   ./tables/
                   ./database.json
               ./connections.json
               ./version.txt
               ./.pbixproj.json
            */

            throw new NotImplementedException();
        }

        public static void ToFile(string path)
        {
            throw new NotImplementedException();
        }

        #region IPbixModel

        public JObject Connections { get; private set; }
        public JObject DataModel { get; private set; }
        // public MashupParts Mashup { get; private set; }
        public JObject Report { get; private set; }
        public JObject DiagramViewState { get; private set; }
        public JObject DiagramLayout { get; private set; }
        public JObject LinguisticSchema { get; private set; }
        public JObject ReportMetadata { get; private set; }
        public JObject ReportSettings { get; private set; }
        public string Version { get; private set; }
        public IDictionary<string, byte[]> CustomVisuals { get; private set; }
        public IDictionary<string, byte[]> StaticResources { get; private set; }

        public PbixProject PbixProj { get; private set; }

        #endregion

        public void Dispose()
        {
            // TODO is that needed?
        }
    }
}

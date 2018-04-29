using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO.Packaging;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Serilog;

namespace PbixTools
{
    public interface IPbixModel
    {
        JObject Connections { get; }
        JObject DataModel { get; }
        Package MashupPackage { get; }
        JObject MashupPermissions { get; }
        XDocument MashupMetadata { get; }
        ZipArchive MashupContent { get; }
        JObject Report { get; }
        JObject DiagramViewState { get; }
        XDocument LinguisticSchema { get; }
        JObject ReportMetadata { get; }
        JObject ReportSettings { get; }
        string Version { get; }

        IDictionary<string, byte[]> CustomVisuals { get; }
        IDictionary<string, byte[]> StaticResources { get; }

        JObject PbixProj { get; }
    }

    /* Workflow: Extract PBIX
     * 1 Create PbixModel from .pbix
     * 2 Serialize PbixModel to folder
     * 2.1 Read existing PbixProject from folder
     * 2.2 Replace query ids accordingly
     */

    public enum PbixModelType
    {
        File,
        Folder,
        LiveSession
    }

    public class PbixModel : IPbixModel, IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixModel>();

        public PbixModelType Type { get; private set; }
        public string SourcePath { get; private set; }

        public static PbixModel FromPbix(string path, IDependenciesResolver dependenciesResolver)
        {
            var pbixModel = new PbixModel { Type = PbixModelType.File, SourcePath = path };

            using (var reader = new PbixReader(path, dependenciesResolver))
            {
                pbixModel.Connections = reader.ReadConnections();
                pbixModel.MashupPackage = reader.ReadMashupPackage();
                pbixModel.MashupPermissions = reader.ReadMashupPermissions();
                pbixModel.MashupMetadata = reader.ReadMashupMetadata();
                pbixModel.MashupContent = reader.ReadMashupContent();
                pbixModel.Report = reader.ReadReport();
                pbixModel.DiagramViewState = reader.ReadDiagramViewState();
                pbixModel.LinguisticSchema = reader.ReadLinguisticSchema();
                pbixModel.ReportMetadata = reader.ReadReportMetadata();
                pbixModel.ReportSettings = reader.ReadReportSettings();
                pbixModel.Version = reader.ReadVersion();
                pbixModel.CustomVisuals = reader.ReadCustomVisuals();
                pbixModel.StaticResources = reader.ReadStaticResources();

                pbixModel.DataModel = reader.ReadDataModel();  // will fire up SSAS instance if PBIX has embedded model
            }

            return pbixModel;
        }

        public static PbixModel FromFolder(string path)
        {
            throw new NotImplementedException();
        }

        public void ToFolder(string path)
        {
            // get or create directory (ProjectRootFolder)
            // read PbixProj (for format version and query ids)

            /*
               ./Mashup
                      ./Section1.m
                      ./Package
                              ./Formulas
                                       ./Section1.m
                      ./Permissions.json
                      ./Metadata.xml
                      ./Content
               ./Report
               ./Model
               ./connections.json
               ./version.txt
               ./pbixproj.json
            */

            throw new NotImplementedException();
        }

        #region IPbixModel

        public JObject Connections { get; private set; }
        public JObject DataModel { get; private set; }
        public Package MashupPackage { get; private set; }
        public JObject MashupPermissions { get; private set; }
        public XDocument MashupMetadata { get; private set; }
        public ZipArchive MashupContent { get; private set; }
        public JObject Report { get; private set; }
        public JObject DiagramViewState { get; private set; }
        public XDocument LinguisticSchema { get; private set; }
        public JObject ReportMetadata { get; private set; }
        public JObject ReportSettings { get; private set; }
        public string Version { get; private set; }
        public IDictionary<string, byte[]> CustomVisuals { get; private set; }
        public IDictionary<string, byte[]> StaticResources { get; private set; }
        public JObject PbixProj { get; private set; }

        #endregion

        public void Dispose()
        {
            (MashupPackage as IDisposable)?.Dispose();
            MashupContent?.Dispose();
        }
    }
}

// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using PbiTools.FileSystem;
using PbiTools.PowerBI;
using PbiTools.ProjectSystem;
using PbiTools.Serialization;
using PbiTools.Utils;
using Serilog;

namespace PbiTools.Model
{
    /// <summary>
    /// A storage-agnostic representation of the contents of a PBIX/PBIT file.
    /// </summary>
    public interface IPbixModel
    {
        JObject Connections { get; }
        MashupParts DataMashup { get; }
        JObject DataModel { get; }
        JObject Report { get; }
        JObject DiagramViewState { get; }
        JObject DiagramLayout { get; }
        JObject LinguisticSchema { get; }
        XDocument LinguisticSchemaXml { get; }
        JObject ReportMetadata { get; }
        JObject ReportSettings { get; }
        string Version { get; }

        // TODO CustomProperties
        // TODO ReportMobileState

        IDictionary<string, byte[]> CustomVisuals { get; } // TODO Change to Dict<Uri, Func<Stream>> ??
        IDictionary<string, byte[]> StaticResources { get; }

        PbixProject PbixProj { get; }
        PbixModelSource Type { get; }
        string SourcePath { get; }
    }


    public enum PbixModelSource
    {
        /// <summary>
        /// The PBIX Model was created from a .pbix/.pbit file. <see cref="SourcePath"/> contains the path to the original file.
        /// </summary>
        PowerBIPackage,
        /// <summary>
        /// The PBIX Model was created from a PbixProj folder. <see cref="SourcePath"/> contains the path to the folder.
        /// </summary>
        PbixProjFolder,
        /// <summary>
        /// The PBIX Model was created from a Tabular model. <see cref="SourcePath"/> contains the path to the original file or folder the model was read from.
        /// </summary>
        TabularModel,
        /// <summary>
        /// The PBIX Model was created from a live Power BI Desktop session. <see cref="SourcePath"/> contains the path to the original .pbix/.pbit file, if available.
        /// </summary>
        LiveSession
    }


    public class PbixModel : IPbixModel, IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixModel>();

        public PbixModelSource Type { get; }
        public string SourcePath { get; }

        private PbixModel(string path, PbixModelSource type)
        {
            this.SourcePath = path;
            this.Type = type;
        }

        /// <summary>
        /// Throws a <see cref="NotSupportedException"/> if this <c>PbixModel</c> was not generated from a V3 PBIX file.
        /// </summary>
        public void EnsureV3Model()
        {
            if (!IsV3Version(this.Version))
            {
                throw new NotSupportedException("The PBIX file/project does not contain a V3 model. This API only supports the V3 PBIX format.");
            }
        }


        private static readonly Version V3ModelVersion = new Version(1, 19);

        public static bool IsV3Version(string versionString)
        {
            if (System.Version.TryParse(versionString, out var version))
            {
                return version >= V3ModelVersion;
            }
            return false;
        }

#if NETFRAMEWORK

        /// <summary>
        /// Builds a <c>PbixModel</c> from the PBIX file at the path specified. Only V3 PBIX files are supported.
        /// </summary>
        public static PbixModel FromFile(string path, IDependenciesResolver dependenciesResolver = null)
        {
            using (var reader = new PbixReader(path ?? throw new ArgumentNullException(nameof(path)), dependenciesResolver ?? DependenciesResolver.Default))
            {
                return FromReader(reader);
            }
        }

        /// <summary>
        /// Builds a <c>PbixModel</c> from the provided <see cref="PbixReader"/> instance. Only V3 PBIX files are supported.
        /// </summary>
        public static PbixModel FromReader(PbixReader reader, string targetFolder = null, int? portNumber = null)
        {
            if (reader is null) throw new ArgumentNullException(nameof(reader));

            var pbixModel = new PbixModel(reader.Path, PbixModelSource.PowerBIPackage)
            { 
                Version = reader.ReadVersion()
            };

            Log.Verbose("Reading PbixModel from file at {Path} (Version: {Version})", reader.Path, pbixModel.Version);

            pbixModel.EnsureV3Model();

            Log.Debug("Reading Connections...");
            pbixModel.Connections = reader.ReadConnections();

            Log.Debug("Reading Report...");
            pbixModel.Report = reader.ReadReport();

            Log.Debug("Reading DiagramLayout...");
            pbixModel.DiagramLayout = reader.ReadDiagramLayout();

            Log.Debug("Reading DiagramViewState...");
            pbixModel.DiagramViewState = reader.ReadDiagramViewState();

            Log.Debug("Reading LinguisticSchemaXml...");
            pbixModel.LinguisticSchemaXml = reader.ReadLinguisticSchema();

            if (pbixModel.LinguisticSchemaXml == null)
            {
                Log.Debug("Reading LinguisticSchema...");
                pbixModel.LinguisticSchema = reader.ReadLinguisticSchemaV3();
            }

            Log.Debug("Reading ReportMetadata...");
            pbixModel.ReportMetadata = reader.ReadReportMetadataV3();

            Log.Debug("Reading ReportSettings...");
            pbixModel.ReportSettings = reader.ReadReportSettingsV3();

            Log.Debug("Reading CustomVisuals...");
            pbixModel.CustomVisuals = reader.ReadCustomVisuals();

            Log.Debug("Reading StaticResources...");
            pbixModel.StaticResources = reader.ReadStaticResources();

            Log.Debug("Reading DataModel...");
            if (portNumber.HasValue)
                pbixModel.DataModel = reader.ReadDataModelFromRunningInstance(portNumber.Value);
            else
                pbixModel.DataModel = reader.ReadDataModel();  // will fire up SSAS instance if PBIX has embedded model

            Log.Debug("Reading DataMashup...");
            pbixModel.DataMashup = reader.ReadMashup();

            using (var projectFolder = new ProjectRootFolder(targetFolder ?? PbixProject.GetDefaultProjectFolderForFile(pbixModel.SourcePath)))
            {
                pbixModel.PbixProj = PbixProject.FromFolder(projectFolder);
            }

            pbixModel.PbixProj.Queries = null; // remove 'Queries', in case those were inherited from a previous legacy version of the model

            return pbixModel;
        }

#endif

        public static PbixModel FromFolder(string path)
        {
            // PBIXPROJ(folder) <==> Serializer <=|PbixModel|=> PbixReader|PbixWriter <==> PowerBIPartConverter <==> PBIX|PBIT(file)
            //                       ##########                 #####################      ####################

            Log.Debug("Building PbixModel from folder: {Path}", path);

            using (var projectFolder = new ProjectRootFolder(path))
            {
                var pbixProject = PbixProject.FromFolder(projectFolder);
                var serializers = new PowerBIPartSerializers(projectFolder, pbixProject.Settings);

                var pbixModel = new PbixModel(projectFolder.BasePath, PbixModelSource.PbixProjFolder) { PbixProj = pbixProject };

                pbixModel.Version = serializers.Version.DeserializeSafe(isOptional: false); // TODO Inject default value when missing?
                pbixModel.EnsureV3Model();
                
                pbixModel.Connections = serializers.Connections.DeserializeSafe();
                pbixModel.Report = serializers.ReportDocument.DeserializeSafe(isOptional: false);
                pbixModel.DiagramLayout = serializers.DiagramLayout.DeserializeSafe();
                pbixModel.DiagramViewState = serializers.DiagramViewState.DeserializeSafe();
                pbixModel.LinguisticSchema = serializers.LinguisticSchema.DeserializeSafe();
                pbixModel.LinguisticSchemaXml = serializers.LinguisticSchemaXml.DeserializeSafe();
                pbixModel.ReportMetadata = serializers.ReportMetadata.DeserializeSafe(isOptional: false);
                pbixModel.ReportSettings = serializers.ReportSettings.DeserializeSafe(isOptional: false);
                pbixModel.CustomVisuals = serializers.CustomVisuals.DeserializeSafe();
                pbixModel.StaticResources = serializers.StaticResources.DeserializeSafe();                
                pbixModel.DataModel = serializers.DataModel.DeserializeSafe();
#if NETFRAMEWORK
                pbixModel.DataMashup = serializers.DataMashup.DeserializeSafe();
#endif
                return pbixModel;
            }
        }


        public static PbixModel FromTabularModelFolder(string path, PbixProject pbixProject = null)
        { 
            Log.Debug("Building PbixModel from tabular model folder: {Path}", path);

            using (var modelFolder = new ProjectRootFolder(path))
            {
                var serializer = new TabularModelSerializer(modelFolder.GetFolder(), new());

                var pbixModel = new PbixModel(modelFolder.BasePath, PbixModelSource.TabularModel)
                {
                    PbixProj = pbixProject ?? new()
                };
                pbixModel.DataModel = serializer.DeserializeSafe();

                return pbixModel;
            }
        }

        public static PbixModel FromTabularModelJson(JObject tmsl, string originalPath, PbixProject pbixProject = null)
        { 
            Log.Debug("Building PbixModel from tabular model json file: {Path}", originalPath);

            var pbixModel = new PbixModel(originalPath, PbixModelSource.TabularModel)
            {
                PbixProj = pbixProject ?? new()
            };
            pbixModel.DataModel = tmsl ?? throw new ArgumentNullException(nameof(tmsl));

            return pbixModel;
        }

        /// <summary>
        /// Serializes the entire model to a file system folder.
        /// </summary>
        /// <param name="path">A custom location to extract the model to (optional).</param>
        public void ToFolder(string path = null, PbixProjectSettings settings = null)
        {
            var rootFolderPath = path ?? this.GetProjectFolder();
            using (var projectFolder = new ProjectRootFolder(rootFolderPath))
            {
                var serializers = new PowerBIPartSerializers(projectFolder, settings ?? this.PbixProj.Settings);

                Log.Information("Extracting PBIX file to: {Path}", projectFolder.BasePath);

                // **** Parts ****
                if (serializers.Version.Serialize(this.Version))
                    Log.Information("Version [{Version}] extracted to: {Path}", this.Version, serializers.Version.BasePath);

                if (serializers.Connections.Serialize(this.Connections))
                    Log.Information("Connections extracted to: {Path}", serializers.Connections.BasePath);

                if (serializers.ReportDocument.Serialize(this.Report))
                    Log.Information("Report extracted to: {Path}", serializers.ReportDocument.BasePath);

                if (serializers.ReportMetadata.Serialize(this.ReportMetadata))
                    Log.Information("Metadata extracted to: {Path}", serializers.ReportMetadata.BasePath);

                if (serializers.ReportSettings.Serialize(this.ReportSettings))
                    Log.Information("Settings extracted to: {Path}", serializers.ReportSettings.BasePath);

                if (serializers.DiagramLayout.Serialize(this.DiagramLayout))
                    Log.Information("DiagramLayout extracted to: {Path}", serializers.DiagramLayout.BasePath);

                if (serializers.DiagramViewState.Serialize(this.DiagramViewState))
                    Log.Information("DiagramViewState extracted to: {Path}", serializers.DiagramViewState.BasePath);

                if (serializers.LinguisticSchema.Serialize(this.LinguisticSchema))
                    Log.Information("LinguisticSchema extracted to: {Path}", serializers.LinguisticSchema.BasePath);

                if (serializers.LinguisticSchemaXml.Serialize(this.LinguisticSchemaXml))
                    Log.Information("LinguisticSchema extracted to: {Path}", serializers.LinguisticSchemaXml.BasePath);

                if (serializers.DataModel.Serialize(this.DataModel))
                    Log.Information("DataModel extracted to: {Path}", serializers.DataModel.BasePath);
#if NETFRAMEWORK
                if (serializers.DataMashup.Serialize(this.DataMashup))
                    Log.Information("DataMashup extracted to: {Path}", serializers.DataMashup.BasePath);
#endif
                if (serializers.CustomVisuals.Serialize(this.CustomVisuals))
                    Log.Information("CustomVisuals extracted to: {Path}", serializers.CustomVisuals.BasePath);

                if (serializers.StaticResources.Serialize(this.StaticResources))
                    Log.Information("StaticResources extracted to: {Path}", serializers.StaticResources.BasePath);


                // **** Metadata ****
                this.PbixProj.Version = PbixProject.CurrentVersion; // always set latest version on new pbixproj file
                if (this.PbixProj.Created == default) this.PbixProj.Created = DateTimeOffset.Now;
                this.PbixProj.LastModified = DateTimeOffset.Now;

                if (settings == null) // Do not persist the PbixProj file if it was provided explicitly
                    this.PbixProj.Save(projectFolder);

                projectFolder.Commit();
            }
        }

        /// <summary>
        /// Generates a PBIX/PBIT file from the model.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="format"></param>
        /// <param name="dependenciesResolver"></param>
        public void ToFile(string path, PbiFileFormat format, IDependenciesResolver dependenciesResolver = null)
        {
            Log.Information("Generating {Format} file at '{Path}'...", format, path);

            if (format == PbiFileFormat.PBIX && this.DataModel != null)
                throw new PbiToolsCliException(ExitCode.InvalidArgs,
                    "Compiling a project containing a data model into a PBIX file is not supported. Please compile into a PBIT file instead."
                );

#if NETFRAMEWORK
            var modelName = PowerBIPartConverters.ConvertToValidModelName(Path.GetFileNameWithoutExtension(path));
            var converters = new PowerBIPartConverters(modelName, dependenciesResolver ?? DependenciesResolver.Default);
            var pbiPackage = new PbiPackage(this, converters, format); // TODO Handle missing Report part

            pbiPackage.Save(path);
#elif NET
            var writer = new PbixWriter(this, new PowerBIPartConverters(), format);
            writer.WriteTo(path);
#endif
        }

        #region IPbixModel

        public JObject Connections { get; private set; }
        public MashupParts DataMashup { get; private set; }
        public JObject DataModel { get; private set; }
        public JObject Report { get; private set; }
        public JObject DiagramViewState { get; private set; }
        public JObject DiagramLayout { get; private set; }
        public JObject LinguisticSchema { get; private set; }
        public XDocument LinguisticSchemaXml { get; private set; }
        public JObject ReportMetadata { get; private set; }
        public JObject ReportSettings { get; private set; }
        public string Version { get; private set; }
        public IDictionary<string, byte[]> CustomVisuals { get; private set; }
        public IDictionary<string, byte[]> StaticResources { get; private set; }

        public PbixProject PbixProj { get; private set; }

#endregion

        /// <summary>
        /// Returns the default folder location for this instance.
        /// </summary>
        public string GetProjectFolder()
        {
            switch (this.Type) 
            {
                case PbixModelSource.PbixProjFolder:
                case PbixModelSource.TabularModel:
                    return this.SourcePath;
                case PbixModelSource.PowerBIPackage:
                case PbixModelSource.LiveSession:
                    return PbixProject.GetDefaultProjectFolderForFile(this.SourcePath);
                default:
                        throw new NotSupportedException($"Unsupported PbixModelSource: {this.Type}.");
            }
        }

        public void Dispose()
        {
            // TODO Possibly only needed for live session models?
        }
    }
}

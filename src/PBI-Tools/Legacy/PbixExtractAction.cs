// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using Microsoft.Mashup.Host.Document;
using Microsoft.PowerBI.Packaging;
using PbiTools.FileSystem;
using PbiTools.PowerBI;
using PbiTools.ProjectSystem;
using PbiTools.Serialization;
using Serilog;

namespace PbiTools.Actions
{
    public class PbixExtractAction : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixExtractAction>();

        static PbixExtractAction()
        {
            var DI = DependencyInjectionService.Get();
            if (!DI.IsRegistered<IFeatureSwitchManager>())
            {
                DI.RegisterInstance<IFeatureSwitchManager>(new NoOpFeatureSwitchManager());
            }
        }

        private readonly IProjectRootFolder _rootFolder;
        private readonly PbixReader _pbixReader;


        public PbixExtractAction(PbixReader reader)
        {
            _pbixReader = reader ?? throw new ArgumentNullException(nameof(reader));
            _rootFolder = new ProjectRootFolder(PbixProject.GetDefaultProjectFolderForFile(reader.Path));
        }

        public void ExtractAll()
        {
            var versionStr = this.ExtractVersion();
            Log.Information($"Version extracted: {versionStr}");
            var isV3 = Model.PbixModel.IsV3Version(versionStr);

            this.ExtractConnections();
            Log.Information("Connections extracted");

            this.ExtractReport();
            Log.Information("Report extracted");

            this.ExtractReportMetadata(isV3);
            Log.Information("ReportMetadata extracted");

            this.ExtractReportSettings(isV3);
            Log.Information("ReportSettings extracted");

            this.ExtractDiagramViewState();
            Log.Information("DiagramViewState extracted");

            this.ExtractDiagramLayout();
            Log.Information("DiagramLayout extracted");

            this.ExtractLinguisticSchema(isV3);
            Log.Information("LinguisticSchema extracted");

            this.ExtractResources();
            Log.Information("Resources extracted");


            var pbixProj = PbixProject.FromFolder(_rootFolder);

            this.ExtractMashup(pbixProj.Settings.Mashup);
            Log.Information("Mashup extracted");

            this.ExtractModel(pbixProj);
            Log.Information("Model extracted");

            pbixProj.Version = PbixProject.CurrentVersion; // always set latest version on new pbixproj file
            pbixProj.Save(_rootFolder);

            _rootFolder.Commit();
        }

        public void ExtractModel(PbixProject pbixProj)
        {
            if (pbixProj.Queries == null) pbixProj.Queries = new Dictionary<string, string>();
            var serializer = new TabularModelSerializer(_rootFolder, pbixProj.Settings.Model, pbixProj.Queries);
            serializer.Serialize(_pbixReader.ReadDataModel());
        }

        public void ExtractResources()
        {
            var customVisuals = _pbixReader.ReadCustomVisuals();
            if (customVisuals != null)
            {
                var serializer = new ResourcesSerializer(_rootFolder, nameof(IPowerBIPackage.CustomVisuals));
                serializer.Serialize(customVisuals);
            }

            var staticResources = _pbixReader.ReadStaticResources();
            if (staticResources != null)
            {
                var serializer = new ResourcesSerializer(_rootFolder, nameof(IPowerBIPackage.StaticResources));
                serializer.Serialize(staticResources);
            }
        }

        public void ExtractMashup(MashupSettings settings)
        {
            var mashupSerializer = new MashupSerializer(_rootFolder, settings);
            mashupSerializer.Serialize(_pbixReader.ReadMashup());
        }

        public void ExtractReport()
        {
            var serializer = new ReportSerializer(_rootFolder);
            serializer.Serialize(_pbixReader.ReadReport());
        }

        public string ExtractVersion()
        {
            var serializer = new StringPartSerializer(_rootFolder, nameof(IPowerBIPackage.Version));
            var version = _pbixReader.ReadVersion();
            serializer.Serialize(version);
            return version;
        }

        public void ExtractConnections()
        {
            var serializer = new JsonPartSerializer(_rootFolder, nameof(IPowerBIPackage.Connections));
            serializer.Serialize(_pbixReader.ReadConnections());
        }

        public void ExtractReportMetadata(bool isV3)
        {
            var serializer = new JsonPartSerializer(_rootFolder, nameof(IPowerBIPackage.ReportMetadata));
            serializer.Serialize(isV3 ? _pbixReader.ReadReportMetadataV3() : _pbixReader.ReadReportMetadata());
        }

        public void ExtractReportSettings(bool isV3)
        {
            var serializer = new JsonPartSerializer(_rootFolder, nameof(IPowerBIPackage.ReportSettings));
            serializer.Serialize(isV3 ? _pbixReader.ReadReportSettingsV3() : _pbixReader.ReadReportSettings());
        }

        public void ExtractDiagramViewState()
        {
            var serializer = new JsonPartSerializer(_rootFolder, nameof(IPowerBIPackage.DiagramViewState));
            serializer.Serialize(_pbixReader.ReadDiagramViewState());
        }

        public void ExtractDiagramLayout()
        {
            var serializer = new JsonPartSerializer(_rootFolder, nameof(IPowerBIPackage.DiagramLayout));
            serializer.Serialize(_pbixReader.ReadDiagramLayout());
        }

        private void ExtractLinguisticSchema(bool isV3)
        {
            if (isV3)
            {
                var serializer = new JsonPartSerializer(_rootFolder, nameof(IPowerBIPackage.LinguisticSchema));
                serializer.Serialize(_pbixReader.ReadLinguisticSchemaV3());
            }
            else
            {
                var serializer = new XmlPartSerializer(_rootFolder, nameof(IPowerBIPackage.LinguisticSchema));
                serializer.Serialize(_pbixReader.ReadLinguisticSchema());
            }
        }


        public void Dispose()
        {
            _rootFolder.Dispose();
        }

    }
}
#endif
using System;
using System.IO;
using Microsoft.PowerBI.Packaging;
using PbiTools.FileSystem;
using PbiTools.PowerBI;
using PbiTools.ProjectSystem;
using PbiTools.Serialization;
using PbiTools.Utils;
using Serilog;

namespace PbiTools.Actions
{
    public class PbixExtractAction : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixExtractAction>();

        private readonly IProjectRootFolder _rootFolder;
        private readonly PbixReader _pbixReader;


        public PbixExtractAction(string pbixPath, IDependenciesResolver resolver)
        {
            _pbixReader = new PbixReader( // TODO Pass in IPbixReader for better testability
                pbixPath ?? throw new ArgumentNullException(nameof(pbixPath)), 
                resolver ?? throw new ArgumentNullException(nameof(resolver)));

            var baseFolder =
                // ReSharper disable once AssignNullToNotNullAttribute
                Path.Combine(Path.GetDirectoryName(pbixPath),
                    Path.GetFileNameWithoutExtension(pbixPath)); // TODO make this configurable
            _rootFolder = new ProjectRootFolder(baseFolder);
        }

        public void ExtractAll()
        {
            // TODO Change API: Convert to PbixModel, then model.SerializeToFolder()

            this.ExtractVersion();
            Log.Information("Version extracted");

            this.ExtractConnections();
            Log.Information("Connections extracted");

            this.ExtractMashup();
            Log.Information("Mashup extracted");

            this.ExtractReport(); // TODO
            Log.Information("Report extracted");

            this.ExtractReportMetadata();
            Log.Information("ReportMetadata extracted");

            this.ExtractReportSettings();
            Log.Information("ReportSettings extracted");

            this.ExtractDiagramViewState();
            Log.Information("DiagramViewState extracted");

            this.ExtractDiagramLayout();
            Log.Information("DiagramLayout extracted");

            this.ExtractLinguisticSchema();
            Log.Information("LinguisticSchema extracted");

            this.ExtractResources();
            Log.Information("Resources extracted");


            var pbixProj = PbixProject.FromFolder(_rootFolder);

            this.ExtractModel(pbixProj); // TODO
            Log.Information("Model extracted");

            pbixProj.Version = PbixProject.CurrentVersion; // always set latest version on new pbixproj file
            pbixProj.Save(_rootFolder);
        }

        public void ExtractModel(PbixProject pbixProj)
        {
            var serializer = new TabularModelSerializer(_rootFolder, pbixProj.Queries);
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

        public void ExtractMashup()
        {
            var mashupSerializer = new MashupSerializer(_rootFolder);
            mashupSerializer.Serialize(_pbixReader.ReadMashup());
        }

        public void ExtractReport()
        {
            var serializer = new ReportSerializer(_rootFolder);
            serializer.Serialize(_pbixReader.ReadReport());
        }

        public void ExtractVersion()
        {
            var serializer = new StringPartSerializer(_rootFolder, nameof(IPowerBIPackage.Version));
            serializer.Serialize(_pbixReader.ReadVersion());
        }

        public void ExtractConnections()
        {
            var serializer = new JsonPartSerializer(_rootFolder, nameof(IPowerBIPackage.Connections));
            serializer.Serialize(_pbixReader.ReadConnections());
        }

        public void ExtractReportMetadata()
        {
            var serializer = new JsonPartSerializer(_rootFolder, nameof(IPowerBIPackage.ReportMetadata));
            serializer.Serialize(_pbixReader.ReadReportMetadata());
        }

        public void ExtractReportSettings()
        {
            var serializer = new JsonPartSerializer(_rootFolder, nameof(IPowerBIPackage.ReportSettings));
            serializer.Serialize(_pbixReader.ReadReportSettings());
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

        private void ExtractLinguisticSchema()
        {
            var serializer = new XmlPartSerializer(_rootFolder, nameof(IPowerBIPackage.LinguisticSchema));
            serializer.Serialize(_pbixReader.ReadLinguisticSchema());
        }


        public void Dispose()
        {
            _pbixReader.Dispose();
            _rootFolder.Dispose();
        }

    }
}

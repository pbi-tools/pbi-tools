using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.PowerBI.Packaging;
using Newtonsoft.Json.Linq;
using Serilog;

namespace PbixTools
{
    public class PbixExtractAction : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixExtractAction>();

        private readonly IProjectRootFolder _rootFolder;
        private readonly PbixReader _pbixReader;


        public PbixExtractAction(string pbixPath, IDependenciesResolver resolver)
        {
            _pbixReader = new PbixReader(
                pbixPath ?? throw new ArgumentNullException(nameof(pbixPath)), 
                resolver ?? throw new ArgumentNullException(nameof(resolver)));

            var baseFolder = Path.Combine(Path.GetDirectoryName(pbixPath), Path.GetFileNameWithoutExtension(pbixPath)); // TODO make this configurable
            _rootFolder = new ProjectRootFolder(baseFolder);
        }

        public void ExtractAll()
        {
            this.ExtractVersion();
            Log.Information("Version extracted");

            this.ExtractConnections();
            Log.Information("Connections extracted");

            this.ExtractMashup();
            Log.Information("Mashup extracted");

            this.ExtractReport();
            Log.Information("Report extracted");

            this.ExtractReportMetadata();
            Log.Information("ReportMatadata extracted");

            this.ExtractReportSettings();
            Log.Information("ReportSettings extracted");

            this.ExtractDiagramViewState();
            Log.Information("DiagramViewState extracted");

            this.ExtractLinguisticSchema();
            Log.Information("LinguisticSchema extracted");

            this.ExtractResources();
            Log.Information("Resources extracted");


            var pbixProj = PbixProject.FromFolder(_rootFolder);

            this.ExtractModel(pbixProj);
            Log.Information("Model extracted");

            pbixProj.Save(_rootFolder);
        }

        public void ExtractModel(PbixProject pbixProj)
        {
            var tmsl = _pbixReader.ReadDataModel();
            if (tmsl == null) return;
            
            var folder = _rootFolder.GetFolder("Model");
            var serializer = new TabularModelSerializer(folder);
            serializer.Serialize(tmsl, pbixProj.Queries);
        }

        public void ExtractResources()
        {
            void WriteContents(IProjectFolder folder, IDictionary<string, byte[]> entries)
            {
                foreach (var entry in entries)
                {
                    if (Path.GetFileName(entry.Key) == "package.json")
                    {
                        try
                        {
                            var json = JObject.Parse(Encoding.UTF8.GetString(entry.Value));
                            folder.Write(json, entry.Key);
                            continue;
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e, "Failed to parse resouce at {Path} as json object.", entry.Key);
                        }
                    }

                    folder.WriteFile(entry.Key, stream =>
                    {
                        stream.Write(entry.Value, 0, entry.Value.Length);
                    });
                }
            };

            var customVisuals = _pbixReader.ReadCustomVisuals();
            if (customVisuals != null)
            {
                var folder = _rootFolder.GetFolder(nameof(IPowerBIPackage.CustomVisuals));
                WriteContents(folder, customVisuals);
            }

            var staticResources = _pbixReader.ReadStaticResources();
            if (staticResources != null)
            {
                var folder = _rootFolder.GetFolder(nameof(IPowerBIPackage.StaticResources));
                WriteContents(folder, staticResources);
            }
        }

        public void ExtractMashup()
        {
            //var folder = _rootFolder.GetFolder("Mashup");

            /*
             *   /Mashup
             *   --/Package/..
             *   -----/Config/Package.xml
             *   -----/Formulas/Section1.m
             *   --/Contents/..
             *   --/metadata.xml
             *   --/permissions.json
             */

            using (var package = _pbixReader.ReadMashupPackage())
            {
                var packageFolder = _rootFolder.GetFolder("Mashup/Package");
                foreach (var entry in package.Entries)
                {
                    if (Path.GetExtension(entry.FullName) == ".xml")
                    {
                        var xml = XDocument.Load(entry.Open());  // XmlException
                        packageFolder.Write(xml, entry.FullName);
                    }
                    else if (Path.GetExtension(entry.FullName) == ".m")
                    {
                        // TODO Check if we need to replace escape sequences
                        packageFolder.WriteFile(entry.FullName, stream =>
                        {
                            entry.Open().CopyTo(stream);
                        });
                    }
                    else
                    {
                        packageFolder.WriteFile(entry.FullName, stream =>
                        {
                            entry.Open().CopyTo(stream);
                        });
                    }
                }
            }

            var contents = _pbixReader.ReadMashupContent();
            if (contents != null)
            {
                using (contents)
                {
                    var contentsFolder = _rootFolder.GetFolder("Mashup/Contents");
                    foreach (var entry in contents.Entries)
                    {
                        contentsFolder.WriteFile(entry.FullName, stream =>
                        {
                            entry.Open().CopyTo(stream);
                        });
                    }
                }
            }

            var metadata = _pbixReader.ReadMashupMetadata();
            if (metadata != null)
            {
                var metadataFile = _rootFolder.GetFile("Mashup/metadata.xml");
                metadataFile.Write(metadata);
            }

            var queryGroups = _pbixReader.ReadQueryGroups();
            if (queryGroups != null)
            {
                var queryGroupsFile = _rootFolder.GetFile("Mashup/queryGroups.json");
                queryGroupsFile.Write(queryGroups);
            }


            var permissions = _pbixReader.ReadMashupPermissions();
            if (permissions != null)
            {
                var permissionsFile = _rootFolder.GetFile("Mashup/permissions.json");
                permissionsFile.Write(permissions);
            }

            //using (var stream = File.OpenRead(_pbixPath))
            //{
            //    if (MashupPackage.TryCreateFromPowerBIDesktopFile(stream, out MashupPackage mashup))
            //    {
            //        foreach (var file in mashup.MFiles)  // in practice, we'll only get one file back
            //        {
            //            var path = Path.Combine(GetFolder("Mashup"), Path.GetFileName(file.Key));
            //            File.WriteAllText(path, 
            //                file.Value.Replace("#(lf)", "\n").Replace("#(tab)", "\t"));
            //            // TODO Recognize all possible M escape sequences....
            //        }
            //    }
            //    else
            //    {
            //        // TODO Log error
            //    }
            //}
        }

        public void ExtractReport()
        {
            // report.json
            //   id, reportId
            //   pods (section order)
            //   resourcePackages
            //   ...
            // config.json
            // filters.json
            // LinguisticSchema.xml
            // /sections: /{name} ("ReportSection1")
            //            filters.json
            //   /visualContainers: /{config.name} ("638f08d2f495792449ca")
            //                      config.json
            //                      query.json
            //                      dataTransforms.json
            //                      filters.json

            var reportFolder = _rootFolder.GetFolder("Report");

            // ReportDocument   [/Report/Layout]
            var jReport = _pbixReader.ReadReport();
            if (jReport == null) return;

            jReport.ExtractObject("config", reportFolder);
            jReport.ExtractArray("filters", reportFolder);

            // sections:
            foreach (var jSection in jReport.ArrayAs<JObject>("sections"))
            {
                var name = jSection["name"]?.Value<string>();
                var sectionFolder = reportFolder.GetSubfolder("sections", name);

                jSection.ExtractObject("config", sectionFolder);
                jSection.ExtractArray("filters", sectionFolder);

                // visualContainers:
                foreach (var jVisual in jSection.ArrayAs<JObject>("visualContainers"))
                {
                    var visualConfig = jVisual.ExtractObject("config", null); // not saving yet as folder name still has to be determined
                    var visualName = visualConfig["name"]?.Value<string>() ?? jVisual["id"].Value<string>(); // TODO Handle missing name/id

                    var visualFolder = sectionFolder.GetSubfolder("visualContainers", visualName);

                    jVisual.ExtractObject("query", visualFolder);
                    jVisual.ExtractArray("filters", visualFolder);
                    jVisual.ExtractObject("dataTransforms", visualFolder);

                    visualConfig.Save("config", visualFolder);
                    jVisual.Save("visualContainer", visualFolder);
                }

                jSection.Save("section", sectionFolder);
            }

            jReport.Save("report", reportFolder);
        }

        public void ExtractVersion()
        {
            var version = _pbixReader.ReadVersion();
            if (version != null)
            {
                _rootFolder.GetFile($"{nameof(IPowerBIPackage.Version)}.txt").Write(version);
            }
        }

        public void ExtractConnections()
        {
            ExtractJsonPart(_pbixReader.ReadConnections(), nameof(IPowerBIPackage.Connections));
        }

        public void ExtractReportMetadata()
        {
            ExtractJsonPart(_pbixReader.ReadReportMetadata(), nameof(IPowerBIPackage.ReportMetadata));
        }

        public void ExtractReportSettings()
        {
            ExtractJsonPart(_pbixReader.ReadReportSettings(), nameof(IPowerBIPackage.ReportSettings));
        }

        public void ExtractDiagramViewState()
        {
            ExtractJsonPart(_pbixReader.ReadDiagramViewState(), nameof(IPowerBIPackage.DiagramViewState));
        }

        private void ExtractJsonPart(JToken json, string name)
        {
            if (json != null)
            {
                _rootFolder.GetFile($"{name}.json").Write(json);
            }
        }

        private void ExtractLinguisticSchema()
        {
            var linguisticSchema = _pbixReader.ReadLinguisticSchema();
            if (linguisticSchema != null)
            {
                _rootFolder.GetFile($"{nameof(IPowerBIPackage.LinguisticSchema)}.xml").Write(linguisticSchema);
            }
        }


        public void Dispose()
        {
            _pbixReader.Dispose();
            _rootFolder.Dispose();
        }

    }
}

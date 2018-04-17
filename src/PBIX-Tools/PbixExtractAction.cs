using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Mashup.Client.Packaging;
using Microsoft.PowerBI.Packaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbixTools
{
    public class PbixExtractAction : IDisposable
    {
        /* PbixExtractor : IDisposable
           - assembles and invokes PackageComponents (Mashup, Model, Report, Version, ...)
           - create PBIXPROJ component (init from existing, modifiable by each child component, write on exit) -- check version
           - provides Context: BaseFolder (derived from PBIX path or customized, PBIXPROJ, IPowerBiPackage)
           - each PackageComponent should provide extract/compile methods (to keep respective actions in same place)
         */

        private readonly string _pbixPath;
        private readonly Stream _pbixStream;
        private readonly IPowerBIPackage _package;
        private readonly IDependenciesResolver _resolver;

        private readonly string _baseFolder;

        public PbixExtractAction(string pbixPath, IDependenciesResolver resolver)
        {
            _pbixPath = pbixPath ?? throw new ArgumentNullException(nameof(pbixPath));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            if (!File.Exists(pbixPath)) throw new FileNotFoundException("PBIX file not found.", pbixPath);

            _baseFolder = Path.Combine(Path.GetDirectoryName(pbixPath), Path.GetFileNameWithoutExtension(pbixPath));
            Directory.CreateDirectory(_baseFolder);

            _pbixStream = File.OpenRead(pbixPath);
            _package = PowerBIPackager.Open(_pbixStream);
        }

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public void ExtractModel()
        {
            var tmsl = default(JObject);

            if (_package.DataModelSchema != null)
            {
                using (var reader = new JsonTextReader(new StreamReader(_package.DataModelSchema.GetStream(), Encoding.Unicode)))
                {
                    tmsl = new JsonSerializer().Deserialize<JObject>(reader);
                }
            }
            else if (_package.DataModel != null)
            {
                const string
                    forbiddenCharacters =
                        @". , ; ' ` : / \ * | ? "" & % $ ! + = ( ) [ ] { } < >"; // grabbed these from an AMO exception message
                var modelName = forbiddenCharacters // TODO Could also use TOM.Server.Databases.CreateNewName()
                    .Replace(" ", "")
                    .ToCharArray()
                    .Aggregate(
                        Path.GetFileNameWithoutExtension(_pbixPath),
                        (n, c) => n.Replace(c, '_')
                    );

                using (var msmdsrv = new AnalysisServicesServer(new ASInstanceConfig
                {
                    DeploymentMode = DeploymentMode.SharePoint, // required for PBI Desktop
                    DisklessModeRequested = true,
                    EnableDisklessTMImageSave = true,
                    // Dirs will be set automatically
                }, _resolver))
                {
                    msmdsrv.HideWindow = true;

                    msmdsrv.Start();
                    msmdsrv.LoadPbixModel(_package, modelName, modelName);

                    using (var server = new TOM.Server())
                    {
                        server.Connect(msmdsrv.ConnectionString);
                        using (var db = server.Databases[modelName])
                        {
                            var json = TOM.JsonSerializer.SerializeDatabase(db, new TOM.SerializeOptions
                            {
                                IgnoreTimestamps = true, // that way we don't have to strip them out below
                                IgnoreInferredObjects = true,
                                IgnoreInferredProperties = true,
                                SplitMultilineStrings = true
                            });
                            tmsl = JObject.Parse(json);
                        }
                    }
                }
            }


            using (var folder = Folder("Model"))
            {
                if (tmsl == null) return; // Checking inside here ensures ProjectFolder cleanup
                var serializer = new TabularModelSerializer(folder);
                serializer.Serialize(tmsl);
            }

        }

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        internal string GetFolder(string label, bool skipCreate = false)
        {
            var dir = Path.Combine(Path.GetDirectoryName(_pbixPath), Path.GetFileNameWithoutExtension(_pbixPath), label);
            if (!skipCreate) Directory.CreateDirectory(dir);
            return dir;
        }

        private IProjectFolder Folder(string label) => new ProjectFolder(Path.Combine(_baseFolder, label));

        public void ExtractResources()
        {
            // TODO Collect existing files first...
            // Account for CustomVisuals or StaticResources having been removed completely

            IEnumerable<(string Label, string Path, IStreamablePowerBIPackagePartContent Content)> EnumerateResources(
                IDictionary<Uri, IStreamablePowerBIPackagePartContent> part, string label)
            {
                if (part != null)
                {
                    foreach (var file in part)
                    {
                        yield return (Label: label, Path: file.Key.ToString(), Content: file.Value);
                    }
                }
            };

            var resources = EnumerateResources(_package.CustomVisuals, nameof(IPowerBIPackage.CustomVisuals)).Concat(
                EnumerateResources(_package.StaticResources, nameof(IPowerBIPackage.StaticResources)));

            foreach (var file in resources)
            {
                var path = Path.Combine(GetFolder(file.Label, skipCreate: true), file.Path);
                using (var src = file.Content.GetStream())
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)); // ensuring all subfolders are in place
                    if (file.Label == nameof(_package.CustomVisuals) && Path.GetFileName(path).Equals("package.json", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Expanding the package.json file:
                        using (var reader = new JsonTextReader(new StreamReader(src)))
                        {
                            File.WriteAllText(path, JToken.ReadFrom(reader).ToString(Formatting.Indented));
                        }
                    }
                    else // any other file
                    {
                        using (var dest = File.Create(path))
                            src.CopyTo(dest);
                    }
                }
            }
        }

        public void ExtractMashup()
        {
            using (var stream = File.OpenRead(_pbixPath))
            {
                if (MashupPackage.TryCreateFromPowerBIDesktopFile(stream, out MashupPackage mashup))
                {
                    foreach (var file in mashup.MFiles)  // in practice, we'll only get one file back
                    {
                        var path = Path.Combine(GetFolder("Mashup"), Path.GetFileName(file.Key));
                        File.WriteAllText(path, 
                            file.Value.Replace("#(lf)", "\n").Replace("#(tab)", "\t"));
                        // TODO Recognize all possible M escape sequences....
                    }
                }
                else
                {
                    // TODO Log error
                }
            }
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

            var reportFolder = GetFolder("Report");

            using (var stream = File.OpenRead(_pbixPath))
            using (var package = PowerBIPackager.Open(stream))
            {
                // ReportDocument   [/Report/Layout]
                using (var reader = new JsonTextReader(new StreamReader(package.ReportDocument.GetStream(), Encoding.Unicode /* this is the crucial bit! */)))
                {
                    var jReport = JToken.ReadFrom(reader) as JObject;

                    jReport.ExtractObject("config", reportFolder);
                    jReport.ExtractArray("filters", reportFolder);

                    // sections:
                    foreach (var jSection in jReport.ArrayAs<JObject>("sections"))
                    {
                        var name = jSection["name"]?.Value<string>();
                        var sectionFolder = Path.Combine(reportFolder, "sections", name);
                        Directory.CreateDirectory(sectionFolder);

                        jSection.ExtractObject("config", sectionFolder);
                        jSection.ExtractArray("filters", sectionFolder);

                        // visualContainers:
                        foreach (var jVisual in jSection.ArrayAs<JObject>("visualContainers"))
                        {
                            var visualConfig = jVisual.ExtractObject("config", null);
                            var visualName = visualConfig["name"]?.Value<string>() ?? jVisual["id"].Value<string>(); // TODO Handle missing name

                            var visualFolder = Path.Combine(sectionFolder, "visualContainers", visualName);
                            Directory.CreateDirectory(visualFolder);

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
            }

        }

        // TODO Version
        // TODO Connections
        // TODO Metadata
        // TODO Settings
        // TODO DiagramState
        // TODO PBIXPROJ.json

        public void Dispose()
        {
            _package.Dispose();
            _pbixStream.Dispose();
        }

    }
}

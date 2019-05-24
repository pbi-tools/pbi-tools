using System;
using Newtonsoft.Json.Linq;
using PbiTools.FileSystem;

namespace PbiTools.Serialization
{
    public class ReportSerializer : IPowerBIPartSerializer<JObject>
    {
        public static string FolderName => "Report";

        private readonly IProjectFolder _reportFolder;

        public ReportSerializer(IProjectRootFolder rootFolder)
        {
            if (rootFolder == null) throw new ArgumentNullException(nameof(rootFolder));
            _reportFolder = rootFolder.GetFolder(FolderName);
        }

        public void Serialize(JObject content)
        {
            // report.json
            // {
            //   id, reportId
            //   pods (section order)
            //   resourcePackages
            //   ...
            // }
            // config.json
            // filters.json
            //
            // /sections
            //        ../{name} ("ReportSection1")
            //             section.json
            //             config.json
            //             filters.json
            //
            //          ../visualContainers
            //                  ../{config.name} ("638f08d2f495792449ca")
            //                       visualContainer.json
            //                       config.json
            //                       filters.json
            //                       query.json
            //                       dataTransforms.json


            // ReportDocument   [/Report/Layout]
            if (content == null) return;

            content.ExtractObject("config", _reportFolder);
            content.ExtractArray("filters", _reportFolder);

            // sections:
            foreach (var jSection in content.RemoveArrayAs<JObject>("sections"))
            {
                var name = jSection["name"]?.Value<string>();
                var sectionFolder = _reportFolder.GetSubfolder("sections", name);

                jSection.ExtractObject("config", sectionFolder);
                jSection.ExtractArray("filters", sectionFolder);

                // visualContainers:
                foreach (var jVisual in jSection.RemoveArrayAs<JObject>("visualContainers"))
                {
                    var visualConfig = jVisual.ExtractObject("config", folder: null); // not saving yet as folder name still has to be determined
                    var visualName = visualConfig["name"]?.Value<string>() ?? jVisual["id"].Value<string>(); // TODO Handle missing name/id

                    var visualFolder = sectionFolder.GetSubfolder("visualContainers", visualName);

                    jVisual.ExtractObject("query", visualFolder);
                    jVisual.ExtractArray("filters", visualFolder);
                    jVisual.ExtractObject("dataTransforms", visualFolder);

                    visualConfig.Save("config", visualFolder);
                    jVisual.Save("visualContainer", visualFolder
                        , ReportJsonTransforms.SortProperties
                        , ReportJsonTransforms.NormalizeNumbers
                        , ReportJsonTransforms.RemoveProperties("queryHash")); // TODO Make transforms configurable
                }

                jSection.Save("section", sectionFolder
                    , ReportJsonTransforms.SortProperties
                    , ReportJsonTransforms.NormalizeNumbers
                    , ReportJsonTransforms.RemoveProperties("objectId", "id"));
            }

            content.Save("report", _reportFolder
                , ReportJsonTransforms.SortProperties
                , ReportJsonTransforms.RemoveProperties("objectId")); // resourcePackage ids tend to change when exporting from powerbi.com
        }

        public bool TryDeserialize(out JObject part)
        {
            throw new NotImplementedException();

            // report.json
            //   add config, filters
            //   add section (+ config, filters)
            //     add visualContainers
        }
    }
}
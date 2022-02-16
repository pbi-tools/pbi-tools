// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PbiTools.Serialization
{
    using FileSystem;
    using Utils;

    public class ReportSerializer : IPowerBIPartSerializer<JObject>
    {
        public static string FolderName => "Report";

        private readonly IProjectFolder _reportFolder;

        public ReportSerializer(IProjectRootFolder rootFolder)
        {
            if (rootFolder == null) throw new ArgumentNullException(nameof(rootFolder));
            _reportFolder = rootFolder.GetFolder(FolderName);
        }

        public string BasePath => _reportFolder.BasePath;

        public bool Serialize(JObject content)
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
            if (content == null) return false;

            content.ExtractObject("config", _reportFolder);
            // modelExtensions
            // bookmarks
            // settings
            content.ExtractArray("filters", _reportFolder);

            // sections:
            foreach (var jSection in content.RemoveArrayAs<JObject>("sections"))
            {
                var sectionFolder = _reportFolder.GetSubfolder("sections", GenerateSectionFolderName(jSection));

                jSection.ExtractObject("config", sectionFolder);
                jSection.ExtractArray("filters", sectionFolder);

                var visualFolderNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase); // ensures visualContainer folder names are unique

                // visualContainers:
                foreach (var jVisual in jSection.RemoveArrayAs<JObject>("visualContainers"))
                {
                    var visualConfig = jVisual.ExtractObject("config", folder: null); // not saving yet as folder name still has to be determined
                    var visualFolder = sectionFolder.GetSubfolder("visualContainers", GenerateVisualFolderName(jVisual, visualConfig, visualFolderNames));

                    jVisual.ExtractObject("query", visualFolder);
                    jVisual.ExtractArray("filters", visualFolder);
                    jVisual.ExtractObject("dataTransforms", visualFolder);

                    visualConfig.Save("config", visualFolder);
                    jVisual.Save("visualContainer", visualFolder
                        , JsonTransforms.SortProperties
                        , JsonTransforms.NormalizeNumbers
                        , JsonTransforms.RemoveProperties("queryHash")); // TODO Make transforms configurable
                }

                jSection.Save("section", sectionFolder
                    , JsonTransforms.SortProperties
                    , JsonTransforms.NormalizeNumbers
                    , JsonTransforms.RemoveProperties("objectId", "id"));
            }

            content.Save("report", _reportFolder
                , JsonTransforms.SortProperties
                , JsonTransforms.RemoveProperties("objectId")); // resourcePackage ids tend to change when exporting from powerbi.com

            return true;
        }

        internal static string GenerateSectionFolderName(JObject jSection)
        { 
            var name = jSection.ReadPropertySafe<string>("name");
            var displayName = jSection.ReadPropertySafe<string>("displayName");
            var ordinal = jSection.ReadPropertySafe<int>("ordinal", 999);
            
            return $"{ordinal:000}_{displayName.SanitizeFilename() ?? name}";
        }

        internal static string GenerateVisualFolderName(JObject jVisual, JObject jConfig, ISet<string> folderNames)
        {
            var name = jConfig.ReadPropertySafe<string>("name")?.Substring(0, 5);
            var id = jVisual.ReadPropertySafe<long>("id");
            var tabOrder = jVisual.ReadPropertySafe<int>("tabOrder");
            string ExtractTitle(string t) => t == null ? null : (t.StartsWith("'") && t.EndsWith("'") ? t.Substring(1, t.Length - 2) : t);
            var title = ExtractTitle(jConfig.SelectToken("singleVisual.vcObjects.title[0].properties.text..Literal.Value")?.Value<string>());
            var groupName = jConfig.SelectToken("singleVisualGroup.displayName")?.Value<string>();
            var visualType = jConfig.SelectToken("singleVisual.visualType")?.Value<string>();

            var nameBase = $"{tabOrder:00000}_{title.SanitizeFilename() ?? groupName.SanitizeFilename() ?? ($"{visualType.SanitizeFilename()} ({name ?? id.ToString()})")}";
            if (folderNames.Add(nameBase)) {
                return nameBase;
            }
            else {
                var nameWithId = $"{nameBase} ({name ?? id.ToString()})";
                folderNames.Add(nameWithId);
                return nameWithId;
            }
        }

        public bool TryDeserialize(out JObject part)
        {
            var reportJson = DeserializeReportElement(_reportFolder, "report.json", 
                "sections", sectionFolder => DeserializeReportElement(sectionFolder, "section.json",
                "visualContainers", visualFolder => DeserializeReportElement(visualFolder, "visualContainer.json")
                    .InsertObjectFromFile(visualFolder, "query.json")
                    .InsertObjectFromFile(visualFolder, "dataTransforms.json")
                )
            );

            part = reportJson;
            return true;
        }

        internal static JObject DeserializeReportElement(IProjectFolder folder, string objectFileName, string childCollectionName = null, Func<IProjectFolder, JObject> childElementFactory = null)
        {
            var elementJson = folder.GetFile(objectFileName)
                .ReadJson()
                .InsertObjectFromFile(folder, "config.json")
                .InsertArrayFromFile(folder, "filters.json");

            if (!String.IsNullOrEmpty(childCollectionName))
            { 
                var childCollectionFolder = folder.GetSubfolder(childCollectionName);
                if (childCollectionFolder.Exists())
                {
                    elementJson[childCollectionName] = 
                        new JArray(childCollectionFolder.GetSubfolders("*").Select(childElementFactory));
                }
            }

            return elementJson;
        }

    }
}
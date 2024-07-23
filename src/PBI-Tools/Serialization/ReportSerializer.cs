/*
 * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.
 * Copyright (C) 2018 Mathias Thierbach
 *
 * pbi-tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * pbi-tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * A copy of the GNU Affero General Public License is available in the LICENSE file,
 * and at <https://goto.pbi.tools/license>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Serilog;

namespace PbiTools.Serialization
{
    using FileSystem;
    using Utils;

    public class ReportSerializer : IPowerBIPartSerializer<JObject>
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ReportSerializer>();

        public static string FolderName => "Report";
        public static string BookmarksFolder => "bookmarks";

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
            // /bookmarks
            //        ../{name}
            //             bookmark.json
            //
            //          ../{child-1}
            //            ../sections
            //              ../{name}
            //                ../visualContainers
            //                     {id}.json
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

            var config = content.ExtractAndParseAsObject("config");

            var bookmarks = config.RemoveArrayAs<JObject>("bookmarks");
            SerializeBookmarks(bookmarks, _reportFolder.GetSubfolder("bookmarks"));

            config.Save("config", _reportFolder);
            // modelExtensions
            // bookmarks
            // settings
            content.ExtractAndParseAsArray("filters", _reportFolder);

            // sections:
            foreach (var jSection in content.RemoveArrayAs<JObject>("sections"))
            {
                var sectionFolder = _reportFolder.GetSubfolder("sections", GenerateSectionFolderName(jSection));

                jSection.ExtractAndParseAsObject("config", sectionFolder);
                jSection.ExtractAndParseAsArray("filters", sectionFolder);

                var visualFolderNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase); // ensures visualContainer folder names are unique

                // visualContainers:
                foreach (var jVisual in jSection.RemoveArrayAs<JObject>("visualContainers"))
                {
                    var visualConfig = jVisual.ExtractAndParseAsObject("config", folder: null); // not saving yet as folder name still has to be determined
                    var visualFolder = sectionFolder.GetSubfolder("visualContainers", GenerateVisualFolderName(jVisual, visualConfig, visualFolderNames));

                    jVisual.ExtractAndParseAsObject("query", visualFolder);
                    jVisual.ExtractAndParseAsArray("filters", visualFolder);
                    jVisual.ExtractAndParseAsObject("dataTransforms", visualFolder);

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

        internal static void SerializeBookmarks(IEnumerable<JObject> jBookmarks, IProjectFolder baseFolder)
        {
            if (jBookmarks == null) return;

            foreach (var jBookmark in jBookmarks)
            {
                var name = jBookmark.Value<string>("displayName");
                var folder = baseFolder.GetSubfolder(name.SanitizeFilename());

                var children = jBookmark.RemoveArrayAs<JObject>("children");

                if (jBookmark.SelectToken("explorationState.sections") is JObject sections)
                {
                    sections.Parent.Remove();

                    foreach (var sectionRef in sections.Properties()) // each property of the 'sections' object is assumed to be a page reference
                    {
                        var sectionName = sectionRef.Name;
                        var sectionFolder = folder.GetSubfolder("sections", sectionName.SanitizeFilename());

                        if (sectionRef.Value is not JObject section) {
                            Log.Warning("The bookmarks/section token is not a Json object: {TokenType}", sectionRef.Value.Type);
                            continue;
                        }

                        foreach (var sectionProperty in section.Properties())
                        {
                            if (sectionProperty.Name.Equals("visualContainers", StringComparison.OrdinalIgnoreCase))
                                continue;

                            sectionProperty.Value.Save(sectionProperty.Name, sectionFolder);
                        }

                        if (section["visualContainers"] is not JObject visualContainers)
                            continue; // assumes the only possible property is 'visualContainers'

                        var visualContainersFolder = sectionFolder.GetSubfolder("visualContainers");

                        foreach (var visualProp in visualContainers.Properties())
                        {
                            var visualJson = visualProp.Value as JObject;
                            visualJson.Save(visualProp.Name, visualContainersFolder);
                        }
                    }
                }

                jBookmark.Save("bookmark", folder);

                SerializeBookmarks(children, folder);
            }
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
                ),
                extendConfig: DeserializeBookmarks
            );

            part = reportJson;
            return true;
        }

        internal static JObject DeserializeReportElement(IProjectFolder folder, string objectFileName
            , string childCollectionName = null
            , Func<IProjectFolder, JObject> childElementFactory = null
            , Func<IProjectFolder, JObject, JObject> extendConfig = null)
        {
            var elementJson = folder.GetFile(objectFileName)
                .ReadJson()
                .InsertObjectFromFile(folder, "config.json", extendConfig)
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

        internal static JObject DeserializeBookmarks(IProjectFolder reportFolder, JObject configJson)
        {
            var bookmarksFolder = reportFolder.GetSubfolder(BookmarksFolder);
            if (!bookmarksFolder.Exists())
                return configJson;

            // ./bookmarks/* subfolders:
            //    ./bookmark.json
            //    ./sections/
            //      ./visualContainers/*.json
            //      ./*.json
            //    ./* -- nested bookmarks children[]

            configJson["bookmarks"] = new JArray(bookmarksFolder
                .GetSubfolders("*")
                .Select(DeserializeSingleBookmark)
                .Where(j => j != null)
            );

            return configJson;
        }

        private static JObject DeserializeSingleBookmark(IProjectFolder bookmarkFolder)
        {
            // get bookmark.json
            var bookmarkFile = bookmarkFolder.GetFile("bookmark.json");
            if (!bookmarkFile.Exists()) return default;

            var bookmarkJson = bookmarkFile.ReadJson();

            // iterate ./sections & ./visualContainers
            // >> /explorationState/sections
            var sectionsFolder = bookmarkFolder.GetSubfolder("sections");
            if (sectionsFolder.Exists())
            {
                var explorationStateJson = bookmarkJson.EnsureObject("explorationState");
                explorationStateJson["sections"] = new JObject(sectionsFolder
                    .GetSubfolders("*")
                    .Select(f => (f.Name, Json: DeserializeBookmarkSection(f)))
                    .Where(x => x.Json != null)
                    .Select(x => new JProperty(x.Name, x.Json))
                );
            }

            // iterate ./* children
            // >> /children
            var childFolders = bookmarkFolder.GetSubfolders("*").Where(f => f.Name != "sections").ToArray();
            if (childFolders.Length > 0) {
                bookmarkJson["children"] = new JArray(childFolders.Select(DeserializeSingleBookmark).Where(x => x != null));
            }

            return bookmarkJson;
        }

        private static JObject DeserializeBookmarkSection(IProjectFolder sectionFolder)
        {
            // new JObject
            // >> /visualContainers
            // ./*.json

            var sectionJson = new JObject(sectionFolder
                .GetFiles("*.json")
                .Select(f => new JProperty(f.Path.WithoutExtension(), f.ReadJson()))
            );

            var visualContainersFolder = sectionFolder.GetSubfolder("visualContainers");
            if (visualContainersFolder.Exists()) {
                sectionJson["visualContainers"] = new JObject(visualContainersFolder
                    .GetFiles("*.json")
                    .Select(f => new JProperty(f.Path.WithoutExtension(), f.ReadJsonToken()))
                );
            }

            return sectionJson;
        }

    }
}

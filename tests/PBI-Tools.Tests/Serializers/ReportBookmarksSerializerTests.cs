// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PbiTools.Tests
{
    using FileSystem;
    using ProjectSystem;
    using Serialization;
    using Utils;

    public class ReportBookmarksSerializerTests : IClassFixture<ReportBookmarksSerializerTests.Fixture>
    {
        // Each bookmark is serialized into folder using the DisplayName
        // DisplayName is url-encoded
        // Child bookmarks are serialized into subfolders
        // Each bookmark payload goes into 'bookmark.json' (name, displayName, explorationState, options)
        // explorationState/sections is extracted into folder: 'sections/XXX/visualContainers/{name}.json'
        // section objects are extracted into files: 'sections/XXX/filters.json'

        /// <summary>
        /// Serializes an embedded test 'config.json' file into a <see cref="TempFolder"/>.
        /// </summary>
        public class Fixture : HasTempFolder, IDisposable
        {
            public Fixture()
            {
                // Serialize config.json into TempFolder
                _rootFolder = new ProjectRootFolder(base.TestFolder.Path);
                var reportJson = new JObject {
                    { "config", Resources.GetEmbeddedResourceString("data.bookmarks.config.json") }
                };

                var serializer = new ReportSerializer(_rootFolder);
                serializer.Serialize(reportJson);

                this.Path = base.TestFolder.Path;
            }

            public string Path { get; }
            protected IProjectRootFolder _rootFolder;

            public override void Dispose()
            {
                base.Dispose();
                this._rootFolder.Dispose();
            }
        }


        private readonly string _basePath;

        public ReportBookmarksSerializerTests(Fixture fixture)
        {
            _basePath = fixture.Path;
        }

        [Fact]
        public void Creates__bookmarks__subfolder()
        {
            var dir = new DirectoryInfo(Path.Combine(_basePath, ReportSerializer.FolderName, "bookmarks"));
            Assert.True(dir.Exists);
        }

        [Fact]
        public void The_bookmarks_array_is_removed_from_the_report_config_file()
        {
            var path = Path.Combine(_basePath,
                "Report",
                "config.json");
            var json = JObject.Parse(File.ReadAllText(path));
            Assert.Null(json["bookmarks"]);
        }

        [Fact]
        public void The_sections_object_is_removed_from_the_bookmark_explorationState()
        {
            var path = Path.Combine(_basePath,
                "Report",
                "bookmarks",
                "%3CGroup 1%3E",
                "FY2018",
                "bookmark.json");
            var json = JObject.Parse(File.ReadAllText(path));
            Assert.Null(json.SelectToken("explorationState.sections"));
        }

        [Fact]
        public void Bookmark_foldername_is_urlencoded()
        {
            Assert.True(Directory.Exists(Path.Combine(_basePath, "Report", "bookmarks", "%3CGroup 1%3E")));
        }

        [Fact]
        public void Each_bookmark_has__bookmark_json__file()
        {
            Assert.True(File.Exists(Path.Combine(_basePath, "Report", "bookmarks", "%3CGroup 1%3E", "bookmark.json")));
            Assert.True(File.Exists(Path.Combine(_basePath, "Report", "bookmarks", "%3CGroup 1%3E", "FY2018", "bookmark.json")));
        }

        [Fact]
        public void Each_visual_state_is_extracted_into_separate_file()
        {
            var sectionFolder = Path.Combine(_basePath,
                "Report",
                "bookmarks",
                "%3CGroup 1%3E",
                "FY2018",
                "sections",
                "ReportSection",
                "visualContainers"
                );
            Assert.Collection(Directory.EnumerateFiles(sectionFolder, "*.json", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileNameWithoutExtension),
                x => Assert.Equal("2ae136c950139397c92b", x),
                x => Assert.Equal("2e36d3431b2d8e5b6995", x),
                x => Assert.Equal("5fbd1f3a94930259664c", x),
                x => Assert.Equal("8a45b304b1829e0e1216", x)
            );
        }

        [TestNotImplemented]
        public void Extracts_all_section_properties_into_files()
        { }

    }


    public class ReportBookmarksDeserializerTests : IClassFixture<ReportBookmarksDeserializerTests.Fixture>
    {

        /// <summary>
        /// Performs a round-trip serialization/deserialization of an embedded test 'config.json' file
        /// and extracts the report config object for inspection.
        /// </summary>
        public class Fixture : ReportBookmarksSerializerTests.Fixture
        {
            public Fixture()
            {
                var serializer = new ReportSerializer(_rootFolder);
                this.ReportConfig = serializer.DeserializeSafe().ExtractAndParseAsObject("config");
            }

            public JObject ReportConfig { get; }

        }


        private readonly JObject _reportConfig;

        public ReportBookmarksDeserializerTests(Fixture fixture)
        {
            _reportConfig = fixture.ReportConfig;
        }

        [Fact]
        public void Contains_one_root_bookmark()
        {
            var rootBookmarks = _reportConfig.SelectTokens("bookmarks[*].displayName");
            Assert.Collection(rootBookmarks,
                x => Assert.Equal("<Group 1>", x)
            );
        }

        [Fact]
        public void Bookmark_contains_single_section()
        {
            var rootBookmarks = _reportConfig.SelectTokens("bookmarks[0].children[0].explorationState.sections.*");
            Assert.Collection(rootBookmarks,
                x => Assert.Equal("ReportSection", (x.Parent as JProperty).Name)
            );
        }

        [Fact]
        public void First_section_has_four_visualContainers()
        {
            var rootBookmarks = _reportConfig.SelectTokens("bookmarks[0].children[0].explorationState.sections.ReportSection.visualContainers.*");
            Assert.Collection(rootBookmarks,
                x => Assert.Equal("2ae136c950139397c92b", (x.Parent as JProperty).Name),
                x => Assert.Equal("2e36d3431b2d8e5b6995", (x.Parent as JProperty).Name),
                x => Assert.Equal("5fbd1f3a94930259664c", (x.Parent as JProperty).Name),
                x => Assert.Equal("8a45b304b1829e0e1216", (x.Parent as JProperty).Name)
            );
        }
    }
}
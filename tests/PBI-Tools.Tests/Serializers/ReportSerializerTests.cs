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

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PbiTools.Tests
{
    using Serialization;
    using Utils;

    public class ReportSerializerTests
    {
        [Fact]
        public void GenerateVisualFolderName__UsesDistinctFallbackNamesForTextboxVisuals()
        {
            var layout = LoadSampleReportLayout();
            var section = layout["sections"]
                .OfType<JObject>()
                .Single(x => x.Value<string>("displayName") == "IT Spend Trend");

            var textboxVisuals = section["visualContainers"]
                .OfType<JObject>()
                .Select(jVisual => new
                {
                    Visual = jVisual,
                    Config = JObject.Parse(jVisual.Value<string>("config"))
                })
                .Where(x => x.Config.SelectToken("singleVisual.visualType")?.Value<string>() == "textbox")
                .ToArray();

            Assert.True(textboxVisuals.Length >= 4);

            var folderNames = new HashSet<string>(System.StringComparer.InvariantCultureIgnoreCase);
            var generatedNames = textboxVisuals
                .Select(x => ReportSerializer.GenerateVisualFolderName(x.Visual, x.Config, folderNames))
                .ToArray();

            Assert.Equal(textboxVisuals.Length, generatedNames.Distinct(System.StringComparer.InvariantCultureIgnoreCase).Count());
            Assert.Contains(generatedNames, x => x.Contains("VisualContainer6"));
            Assert.Contains(generatedNames, x => x.Contains("VisualContainer7"));
        }

        private static JObject LoadSampleReportLayout()
        {
            using var pbixStream = Resources.GetEmbeddedResourceStream("IT Spend Analysis Sample PBIX.pbix");
            using var archive = new ZipArchive(pbixStream, ZipArchiveMode.Read);
            var layoutEntry = archive.GetEntry("Report/Layout");

            Assert.NotNull(layoutEntry);

            using var layoutStream = layoutEntry.Open();
            using var memory = new MemoryStream();
            layoutStream.CopyTo(memory);

            var layoutText = Encoding.Unicode.GetString(memory.ToArray());
            layoutText = layoutText.Substring(0, layoutText.LastIndexOf('}') + 1);

            return JObject.Parse(layoutText);
        }
    }
}

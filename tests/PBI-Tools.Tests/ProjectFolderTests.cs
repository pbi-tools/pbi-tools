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

using System.IO;
using PbiTools.FileSystem;
using Xunit;

namespace PbiTools.Tests
{
    public class ProjectFolderTests : HasTempFolder
    {
        [Fact]
        public void Removes_base_folder_if_no_files_have_been_written()
        {
            var baseDir = Path.Combine(TestFolder.Path, "Test");
            Directory.CreateDirectory(baseDir);

            File.WriteAllText(Path.Combine(baseDir, "test.txt"), "...");

            // BEFORE
            Assert.True(Directory.Exists(baseDir));

            using (var root = new ProjectRootFolder(baseDir))
            {
                // Do nothing (not writing any files)

                root.Commit();
            }

            // AFTER
            Assert.False(Directory.Exists(baseDir));
            // this will have removed 'test.txt'
        }

        [Fact]
        public void Does_not_create_base_folder_if_no_files_are_written()
        {
            var baseDir = Path.Combine(TestFolder.Path, "Test");

            // BEFORE
            Assert.False(Directory.Exists(baseDir));

            using (new ProjectRootFolder(baseDir))
            {
                // Do nothing (not writing any files)
            }

            // AFTER
            Assert.False(Directory.Exists(baseDir));
        }
    }
}

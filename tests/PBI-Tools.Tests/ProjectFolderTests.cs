// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

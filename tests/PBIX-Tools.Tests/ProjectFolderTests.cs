using System.IO;
using Xunit;

namespace PbixTools.Tests
{
    public class ProjectFolderTests : HasTempFolder
    {
        [Fact]
        public void Removes_base_folder_if_no_files_have_been_written()
        {
            var baseDir = Path.Combine(TestFolder.Path, "Test"); // this is the equivalent of a labelled sub-folder in the PBIXPROJ dir, e.g. "Mashup"
            Directory.CreateDirectory(baseDir);
            File.WriteAllText(Path.Combine(baseDir, "test.txt"), "...");

            // BEFORE
            Assert.True(Directory.Exists(baseDir));

            using (new ProjectFolder(baseDir))
            {
                // Do nothing (not writing any files)
            }

            // AFTER
            Assert.False(Directory.Exists(baseDir));
        }

        [Fact]
        public void Does_not_create_base_folder_if_no_files_are_written()
        {
            var baseDir = Path.Combine(TestFolder.Path, "Test"); // this is the equivalent of a labelled sub-folder in the PBIXPROJ dir, e.g. "Mashup"

            // BEFORE
            Assert.False(Directory.Exists(baseDir));

            using (new ProjectFolder(baseDir))
            {
                // Do nothing (not writing any files)
            }

            // AFTER
            Assert.False(Directory.Exists(baseDir));
        }
    }
}

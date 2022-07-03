// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Xunit;

namespace PbiTools.Tests.Deployments
{
    using PbiTools.Deployments;

    public class ReportDeploymentTests
    {
        /// <summary>
        /// https://github.com/pbi-tools/pbi-tools/issues/127
        /// </summary>
        [Fact]
        public void Handles_paths_with_spaces()
        {
            var baseDir = @"C:/Temp";
            var fullPath = @"C:/Temp/foo 2 3.bar";

            Assert.Equal("foo 2 3.bar", DeploymentManager.MakeRelativePath(baseDir, fullPath));
        }
    }
}
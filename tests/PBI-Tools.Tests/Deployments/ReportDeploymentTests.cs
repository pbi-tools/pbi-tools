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

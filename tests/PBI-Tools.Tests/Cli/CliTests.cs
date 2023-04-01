// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Xunit;
using PowerArgs;
using System.Linq;

namespace PbiTools.Tests
{
    using Cli;

    public class CliTests
    {
        [Fact]
        public void CliTest() 
        {
            var definitions = CmdLineArgumentsDefinitionExtensions.For<CmdLineActions>();
            definitions.Actions.Select(a => a.Aliases);
        }

        [Fact]
        public void Can_export_usage()
        {
            new CmdLineActions().ExportUsage(null);
        }

        [Theory]
        [InlineData("info")]
        public void Can_parse_action(params string[] args)
        {
            var action = Args.ParseAction<CmdLineActions>(args);

            Assert.NotNull(action);
        }
    }
}
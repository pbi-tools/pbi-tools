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
using System.IO;
using System.Linq;
using System.Text;
using PowerArgs;

namespace PbiTools.Cli
{
    public partial class CmdLineActions
    {

        [ArgActionMethod, ArgShortcut("export-usage"), OmitFromUsageDocs]
        public void ExportUsage(
            [ArgDescription("The optional path to a file to write into. Prints to console if not provided.")]
                string outPath
        )
        {
            var sb = new StringBuilder();
            
            var definitions = CmdLineArgumentsDefinitionExtensions.For<CmdLineActions>().RemoveAutoAliases(true);
            definitions.ExeName =
#if NETFRAMEWORK
                "pbi-tools"
#elif NET
                "pbi-tools.core"
#endif
                ;

            sb.AppendLine("## Usage");
            sb.AppendLine();
            sb.AppendLine($"    {definitions.UsageSummary}");
            sb.AppendLine();
            sb.AppendLine($"_{definitions.Description.Replace("|", "\\|")}_");
            sb.AppendLine();
            sb.AppendLine("### Actions");
            sb.AppendLine();

            foreach (var action in definitions.UsageActions)
            {
                sb.AppendLine($"#### {action.DefaultAlias}");
                sb.AppendLine();
                sb.AppendLine($"    {action.UsageSummary}");
                sb.AppendLine();
                sb.AppendLine(action.Description);
                sb.AppendLine();

                if (action.HasArguments)
                { 
                    sb.AppendLine("| Option | Default Value | Is Switch | Description |");
                    sb.AppendLine("| --- | --- | --- | --- |");

                    foreach (var arg in action.UsageArguments.Where(a => !a.OmitFromUsage))
                    {
                        var enumValues = arg.EnumValuesAndDescriptions.Aggregate(new StringBuilder(), (sb, fullDescr) => {
                            var pos = fullDescr.IndexOf(" - ");
                            var value = fullDescr.Substring(0, pos);
                            var descr = fullDescr.Substring(pos);
                            sb.Append($" <br> `{value}` {descr}");
                            return sb;
                        });
                        sb.AppendLine($"| {arg.DefaultAlias}{(arg.IsRequired ? "*" : "")} | {(arg.HasDefaultValue ? $"`{arg.DefaultValue}`" : "")} | {(arg.ArgumentType == typeof(bool) ? "X" : "")} | {arg.Description}{enumValues} |");
                    }
                    sb.AppendLine();
                }

                if (action.HasExamples)
                {
                    foreach (var example in action.Examples)
                    {
                        if (example.HasTitle)
                        { 
                            sb.AppendLine($"**{example.Title}**");
                            sb.AppendLine();
                        }

                        sb.AppendLine($"    {example.Example}");
                        sb.AppendLine();
                        sb.AppendLine($"_{example.Description}_");
                        sb.AppendLine();
                    }
                }
            }

            if (string.IsNullOrEmpty(outPath))
            { 
                using (_appSettings.SuppressConsoleLogs())
                {
                    Console.WriteLine(sb.ToString());
                }
            }
            else
            {
                using (var writer = File.CreateText(outPath))
                {
                    writer.Write(sb.ToString());
                }
            }
        }

    }

}

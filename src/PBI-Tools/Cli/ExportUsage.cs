// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

            if (String.IsNullOrEmpty(outPath))
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
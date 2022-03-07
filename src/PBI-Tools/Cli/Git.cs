// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using LibGit2Sharp;
using PowerArgs;

namespace PbiTools.Cli
{
    public partial class CmdLineActions
    {

        [ArgActionMethod, ArgShortcut("git")]
        [ArgDescription("Integrates with git and exposes certain actions to perform in the current working directory.")]
        [ArgExample(
            "pbi-tools git branch", 
            "Displays the active git branch in the current working directory. Automatically detects the root of the current git repository.")]
        public void Git(
            [ArgRequired, ArgDescription("The git action to perform.")]
                GitAction action
        )
        {
            switch (action)
            {
                case GitAction.Branch:
                    var gitRepo = Repository.Discover(Environment.CurrentDirectory);
                    if (gitRepo == null) { 
                        Log.Warning("No valid git repository found at current working directory: {CWD}.", Environment.CurrentDirectory);
                        return;
                    }
                    using (var repo = new Repository(gitRepo))
                    {
                        Log.Information("Using git repository at: {GitPath}", gitRepo);
                        Log.Information("Active branch: {Branch}", repo.Head.FriendlyName);
                    }
                    break;
                default:
                    throw new PbiToolsCliException(ExitCode.InvalidArgs, $"Invalid action: {action}.");
            }
        }

    }

    public enum GitAction
    {
        [ArgDescription("Displays the active git branch in the current working directory.")]
        Branch = 1,
    }

}
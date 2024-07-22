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

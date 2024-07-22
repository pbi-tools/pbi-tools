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
using PowerArgs;

namespace PbiTools.Cli
{
    using Utils;

    public partial class CmdLineActions
    {

#if NETFRAMEWORK
        [ArgActionMethod, ArgShortcut("cache")]
        [ArgDescription("Manages the internal assembly cache.")]
        [ArgExample("pbi-tools cache list", "Lists all cache folders present in the current user profile.")]
        public void Cache(
            [ArgRequired, ArgDescription("The cache action to perform.")]
                CacheAction action
        )
        {
            var folders = Directory.GetDirectories(ApplicationFolders.AppDataFolder);
            
            switch (action)
            {
                case CacheAction.List:
                    Array.ForEach(folders, f =>
                        Console.WriteLine($"- {Path.GetFileName(f)}")
                    );
                    break;
                case CacheAction.ClearAll:
                    Array.ForEach(folders, f => 
                    {
                        Directory.Delete(f, recursive: true);
                        Console.WriteLine($"Deleted: {Path.GetFileName(f)}");
                    });
                    break;
                case CacheAction.ClearOutdated:
                    Array.ForEach(folders.OrderByDescending(x => x).Skip(1).ToArray(), f => 
                    {
                        Directory.Delete(f, recursive: true);
                        Console.WriteLine($"Deleted: {Path.GetFileName(f)}");
                    });
                    break;
            }
        }
#endif

    }

    public enum CacheAction
    {
        [ArgDescription("List all cache folders.")]
        List = 1,
        [ArgDescription("Clear all cache folders.")]
        ClearAll,
        [ArgDescription("Clear all cache folders except the most recent one.")]
        ClearOutdated
    }
    
}

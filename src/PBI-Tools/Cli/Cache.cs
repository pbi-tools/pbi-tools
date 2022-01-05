// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
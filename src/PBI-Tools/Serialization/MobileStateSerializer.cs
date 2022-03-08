// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PbiTools.Serialization
{
    using FileSystem;
    using Utils;

    public class MobileStateSerializer : IPowerBIPartSerializer<JObject>
    {
        public static string FolderName => "Report/mobileState";

        private readonly IProjectFolder _mobileStateFolder;

        public MobileStateSerializer(IProjectRootFolder rootFolder)
        {
            if (rootFolder == null) throw new ArgumentNullException(nameof(rootFolder));
            _mobileStateFolder = rootFolder.GetFolder(FolderName);
        }

        public string BasePath => _mobileStateFolder.BasePath;

        public bool Serialize(JObject content)
        {
            if (content == null) return false;
            
            content.ExtractObject("explorationState", _mobileStateFolder);
            _mobileStateFolder.Write(content, "mobileState.json");

            return true;
        }

        public bool TryDeserialize(out JObject part)
        {
            var file = _mobileStateFolder.GetFile("mobileState.json");
            if (!file.Exists())
            {
                part = null;
                return false;
            }

            part = file.ReadJson()
                .InsertObjectFromFile(_mobileStateFolder, "explorationState.json");
            return true;
        }

    }
}
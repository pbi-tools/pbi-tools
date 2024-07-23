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
            
            content.ExtractAndParseAsObject("explorationState", _mobileStateFolder);
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

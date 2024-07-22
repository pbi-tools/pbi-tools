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

using System.Collections.Generic;
using PbiTools.Serialization;

namespace PbiTools.Tests
{
    public class MockQueriesLookup : IQueriesLookup
    {
        private readonly IDictionary<string, string> _lookup;

        public MockQueriesLookup() : this(new Dictionary<string, string>())
        {
        }

        public MockQueriesLookup(IDictionary<string, string> lookup)
        {
            _lookup = lookup;
        }


        public string LookupOriginalDataSourceId(string currentDataSourceId)
        {
            return _lookup[currentDataSourceId] ?? currentDataSourceId;
        }
    }
}

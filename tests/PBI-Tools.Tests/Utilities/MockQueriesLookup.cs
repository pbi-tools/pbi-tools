// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
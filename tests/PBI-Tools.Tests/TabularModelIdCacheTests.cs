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
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PbiTools.Serialization;
using Xunit;

namespace PbiTools.Tests
{
    public class TabularModelIdCacheTests
    {

        [Fact]
        public void BuildCurrentLocationsLookup__TestHappyPath()
        {
            var dataSources = JArray.Parse(@"[
      {
        ""name"": ""ef820111-b036-4610-bd66-563fa042a683"",
        ""connectionString"": ""provider=Microsoft.PowerBI.OleDb;global pipe=11557f93-8d48-4d20-9052-4b1649d187c4;mashup=xxx;location=Revenue"",
        ""impersonationMode"": ""impersonateCurrentUser""
      },
      {
        ""name"": ""df4c985f-4c12-4140-82de-abaccd2969d6"",
        ""connectionString"": ""provider=Microsoft.PowerBI.OleDb;global pipe=11557f93-8d48-4d20-9052-4b1649d187c4;mashup=xxx;location=Info"",
        ""impersonationMode"": ""impersonateCurrentUser""
      },
      {
        ""name"": ""4546ed4c-1cf7-40be-93bf-3771fa2b5b72"",
        ""connectionString"": ""provider=Microsoft.PowerBI.OleDb;global pipe=11557f93-8d48-4d20-9052-4b1649d187c4;mashup=xxx;location=Currency"",
        ""impersonationMode"": ""impersonateCurrentUser""
      }]");
            var lookup = TabularModelIdCache.BuildCurrentLocationsLookup(dataSources);

            Assert.Equal(3, lookup.Count);
            Assert.Equal("Revenue", lookup["ef820111-b036-4610-bd66-563fa042a683"]);
            Assert.Equal("Info", lookup["df4c985f-4c12-4140-82de-abaccd2969d6"]);
            Assert.Equal("Currency", lookup["4546ed4c-1cf7-40be-93bf-3771fa2b5b72"]);
        }

        [Fact]
        public void BuildDataSourceLookup__TestHappyPath()
        {
            var dataSources = JArray.Parse(@"[
      {
        ""name"": ""ef820111-b036-4610-bd66-563fa042a683"",
        ""connectionString"": ""provider=Microsoft.PowerBI.OleDb;global pipe=11557f93-8d48-4d20-9052-4b1649d187c4;mashup=xxx;location=Revenue"",
        ""impersonationMode"": ""impersonateCurrentUser""
      },
      {
        ""name"": ""df4c985f-4c12-4140-82de-abaccd2969d6"",
        ""connectionString"": ""provider=Microsoft.PowerBI.OleDb;global pipe=11557f93-8d48-4d20-9052-4b1649d187c4;mashup=xxx;location=Info"",
        ""impersonationMode"": ""impersonateCurrentUser""
      },
      {
        ""name"": ""4546ed4c-1cf7-40be-93bf-3771fa2b5b72"",
        ""connectionString"": ""provider=Microsoft.PowerBI.OleDb;global pipe=11557f93-8d48-4d20-9052-4b1649d187c4;mashup=xxx;location=Currency"",
        ""impersonationMode"": ""impersonateCurrentUser""
      }]");
            var lookup = TabularModelIdCache.BuildDataSourceLookup(dataSources);

            Assert.Equal(3, lookup.Count);
            Assert.Equal("ef820111-b036-4610-bd66-563fa042a683", lookup["Revenue"]);
            Assert.Equal("df4c985f-4c12-4140-82de-abaccd2969d6", lookup["Info"]);
            Assert.Equal("4546ed4c-1cf7-40be-93bf-3771fa2b5b72", lookup["Currency"]);
        }

    }
}

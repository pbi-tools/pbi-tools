// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PbiTools.Serialization
{
    public interface IQueriesLookup
    {
        /// <summary>
        /// Given a dataSource id from the current model, looks up the cached id as maintained in the PBIXPROJ file (if any).
        /// </summary>
        /// <param name="currentDataSourceId"></param>
        /// <returns></returns>
        string LookupOriginalDataSourceId(string currentDataSourceId);
    }


    public class TabularModelIdCache : IQueriesLookup
    {
        // TODO Must convert this into two separate components, 1 - PBIXPROJ file, 2 - IdCache
        // TODO Log the things
        // TODO defensive error handling

        private readonly IDictionary<string, string> _dataSourcesByLocation; // get from existing cache file
        private readonly IDictionary<string, string> _locationsByDataSource; // build from current dataSources

        public TabularModelIdCache(JArray dataSources, IDictionary<string, string> queriesLookup)
        {
            if (dataSources == null) throw new ArgumentNullException(nameof(dataSources));

            // only consider dataSources with a connectionString having a 'location' property

            _dataSourcesByLocation = queriesLookup ?? new Dictionary<string, string>();
            var currentDataSources = BuildDataSourceLookup(dataSources);

            // Add newly added data sources (merge)
            foreach (var dataSource in currentDataSources)
            {
                // key: Location, value: Data Source Guid
                if (!_dataSourcesByLocation.ContainsKey(dataSource.Key))
                    _dataSourcesByLocation.Add(dataSource);
            }
            // Remove deleted ddata sources
            foreach (var location in _dataSourcesByLocation.Keys.ToArray())
            {
                if (!currentDataSources.ContainsKey(location))
                    _dataSourcesByLocation.Remove(location);
            }

            // Lookup: dataSource ->> location
            _locationsByDataSource = BuildCurrentLocationsLookup(dataSources);
        }

        internal static IDictionary<string, string> BuildCurrentLocationsLookup(JArray dataSources)
        {
            return BuildLookup(dataSources, (dict, name, location) => dict.Add(name, location));
        }

        internal static IDictionary<string, string> BuildDataSourceLookup(JArray dataSources)
        {
            return BuildLookup(dataSources, (dict, name, location) => dict.Add(location, name));
        }

        private static IDictionary<string, string> BuildLookup(JArray dataSources, Action<Dictionary<string, string>, string, string> onEntry)
        {
            var dict = new Dictionary<string, string>();
            foreach (var dataSource in dataSources)
            {
                var name = dataSource.Value<string>("name");
                var connectionString = dataSource.Value<string>("connectionString");
                if (name != null && connectionString != null)
                {
                    var connStrBldr = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
                    if (connStrBldr.TryGetValue("Location", out var location))
                    {
                        onEntry(dict, name, location.ToString());
                    }
                }
            }

            return dict;
        }

        /// <summary>
        /// Given a dataSource id from a table partition, gets the original id for the same dataSource if provided in the local cache file.
        /// Returns the input if there is no match.
        /// </summary>
        public string LookupOriginalDataSourceId(string currentDataSourceId)
        {
            // get location from current dataSource, then original Id for same location from cache
            // return same if lookup fails
            if (_locationsByDataSource.TryGetValue(currentDataSourceId, out var location) &&
                _dataSourcesByLocation.TryGetValue(location, out var dataSource))
                return dataSource;
            return currentDataSourceId;
        }

        // init: TMSL, existing ".id-cache.json"
        // create cache file when not exits, otherwise load
        // 
        // read model/dataSources (map location => name (guid)
        // partition: lookup dataSource/location, then name (guid) from cache; keep current value if no cache
        // add new dataSources as they appear (remove old ones?)
    }

}
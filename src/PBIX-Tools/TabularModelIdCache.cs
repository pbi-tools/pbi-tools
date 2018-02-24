using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PbixTools
{
    public class TabularModelIdCache
    {
        public static string FileName = ".id-cache.json";

        private readonly JsonSerializer _jsonSerializer = new JsonSerializer();
        private readonly IProjectFolder _folder;
        private readonly IDictionary<string, string> _dataSourcesByLocation; // get from existing cache file
        private readonly IDictionary<string, string> _locationsByDataSource; // build from current dataSources

        public TabularModelIdCache(IProjectFolder folder, JArray dataSources)
        {
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
            if (dataSources == null) throw new ArgumentNullException(nameof(dataSources));
            _jsonSerializer.Formatting = Formatting.Indented; // makes it readable in source control

            // only consider dataSources with a connectionString having a 'location' property

            // Lookup: location ->> dataSource (static)
            if (folder.TryGetFile(FileName, out var stream))
            {
                // load file and convert to dict
                using (var reader = new JsonTextReader(new StreamReader(stream)))
                {
                    _dataSourcesByLocation = _jsonSerializer.Deserialize<Dictionary<string, string>>(reader);
                }
            }
            else
            {
                // build dict from current dataSources
                // assume each location only occurs once
                _dataSourcesByLocation = BuildDataSourceLookup(dataSources);
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
                    var connStrBldr = new System.Data.OleDb.OleDbConnectionStringBuilder(connectionString);
                    if (connStrBldr.TryGetValue("location", out var location))
                    {
                        onEntry(dict, name, location.ToString());
                    }
                }
            }

            return dict;
        }

        public void WriteCacheFile()
        {
            // dont write if empty
            if (_dataSourcesByLocation.Count > 0)
            {
                _folder.WriteText(FileName, writer =>
                {
                    _jsonSerializer.Serialize(writer, _dataSourcesByLocation);
                });
            }
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
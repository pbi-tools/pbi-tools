// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using PbiTools.FileSystem;
using Serilog;

namespace PbiTools.Serialization
{
    /// <summary>
    /// Serializes a tabular database (represented as TMSL/json) into a <see cref="IProjectFolder"/>.
    /// </summary>
    /// <remarks>Methods for deserialization to be added in a future version.</remarks>
    public class TabularModelSerializer : IPowerBIPartSerializer<JObject>
    {

        private static readonly ILogger Log = Serilog.Log.ForContext<TabularModelSerializer>();

        public static string FolderName => "Model";

        private readonly IProjectFolder _folder;
        private readonly IDictionary<string, string> _queries;

        // TODO Support SerializerSettings, providing control over extraction format (Raw, Default, TabularEditor)
        //      Persist settings in .pbixproj.json

        public TabularModelSerializer(IProjectRootFolder rootFolder, IDictionary<string, string> queries = null)
        {
            if (rootFolder == null) throw new ArgumentNullException(nameof(rootFolder));
            _queries = queries ?? new Dictionary<string, string>();
            _folder = rootFolder.GetFolder(FolderName);
        }

        public string BasePath => _folder.BasePath;

        public bool Serialize(JObject db)
        {
            // Ignore PBIT timestamps -- TODO: Make this configurable
            db = db.RemoveProperties("modifiedTime", "refreshedTime", "lastProcessed", "structureModifiedTime", "lastUpdate", "lastSchemaUpdate", "createdTimestamp");

            if (db == null) return false;

            var dataSources = db.SelectToken("model.dataSources") as JArray ?? new JArray();
            var idCache = new TabularModelIdCache(dataSources, _queries);

            // 
            // model
            // model.tables []       // special handling of partitions, measures
            // model.dataSources []  // special handling of connectionString

            /* expand: tables
                       table/measures (extract into XML)
                       table/hierarchies -- {name}.json
                       dataSources
                       dataSource/mashup
                       expressions { name, kind=m, expression } ==> {name}.m  [~~not relevant for PBI~~]
            */

            db = SerializeDataSources(db, _folder, idCache);
            db = SerializeTables(db, _folder, idCache);
            db = SerializeExpressions(db, _folder);
            // TODO *** QueryGroups ***

            /* model.queryGroups:

    "queryGroups": [
      {
        "folder": "Group 1"
      }
    ]

               table.partitions:
        "partitions": [
          {
            "name": "Contacts-2e6aebda-3dcf-4569-9b6f-536c2a6592b4",
            "queryGroup": "Group 1",

            */

            SaveDatabase(db, _folder);

            return true;
        }

        internal static JObject SerializeTables(JObject db, IProjectFolder folder, IQueriesLookup idCache)
        {
            // hierarchies
            // measures

            if (!(db.SelectToken("model.tables") is JArray tables)) return db;

            // /tables sub-folder
            // remove from db
            // sanitize filenames 

            foreach (var table in tables.OfType<JObject>())
            {
                var name = table["name"]?.Value<string>()?.SanitizeFilename();
                if (name == null) continue;

                // TODO Come up with a more elegant API
                var _table = SerializeMeasures(table, folder, $@"tables\{name}");
                _table = SerializeHierarchies(_table, folder, $@"tables\{name}");
                _table = SerializeTablePartitions(_table, folder, idCache);

                folder.Write(_table, $@"tables\{name}\table.json");
            }

            tables.Parent.Remove();

            return db;
        }

        internal static JObject SerializeMeasures(JObject table, IProjectFolder folder, string pathPrefix)
        {
            var measures = table["measures"]?.Value<JArray>();
            if (measures == null) return table;

            foreach (var measure in measures)
            {
                var name = measure["name"]?.Value<string>();
                if (name == null) continue;

                folder.WriteText(Path.Combine(pathPrefix, "measures", $"{name.SanitizeFilename()}.xml"), WriteMeasureXml(measure));
            }

            table = new JObject(table);
            table.Remove("measures");
            return table;
        }

        internal static JObject SerializeHierarchies(JObject table, IProjectFolder folder, string pathPrefix)
        {
            // TODO Remove code duplication (measures, tables, dataSources)
            var hierarchies = table["hierarchies"]?.Value<JArray>();
            if (hierarchies == null) return table;

            foreach (var hierarchy in hierarchies)
            {
                var name = hierarchy["name"]?.Value<string>();
                if (name == null) continue;

                folder.Write(hierarchy, Path.Combine(pathPrefix, "hierarchies", $"{name.SanitizeFilename()}.json"));
            }

            table = new JObject(table);
            table.Remove("hierarchies");
            return table;
        }

        /// <summary>
        /// Converts an M expression from a TMSL payload into a single string, accounting for both single and multi-line expressions.
        /// </summary>
        internal static string ConvertExpression(JToken j) => (j is JArray)
            ? String.Join(Environment.NewLine, j.ToObject<string[]>())
            : j.Value<string>();

        /// <summary>
        /// Converts an M expression into a TMSL payload, accounting for both, single and multi-line, expressions.
        /// </summary>
        private static JToken ConvertExpression(string expression)
        {
            var lines = (expression ?? "").Split(new[] { Environment.NewLine}, StringSplitOptions.None);
            if (lines.Length == 1)
                return new JValue(lines[0]);
            return new JArray(lines);
        }

        internal static JObject SerializeTablePartitions(JObject table, IProjectFolder folder, IQueriesLookup idCache)
        {
            var _table = new JObject(table);

            /* Legacy models: Partition has source.dataSource */
            var dataSources = _table.SelectTokens("partitions[*].source.dataSource").OfType<JValue>();
            foreach (var dataSource in dataSources)
            {
                dataSource.Value = idCache.LookupOriginalDataSourceId(dataSource.Value<string>());
            }

            /* V3 models: Partition has source.expression */
            var mPartitions = _table.SelectTokens("partitions[?(@.source.type == 'm')]").OfType<JObject>().ToArray();
            if (mPartitions.Length == 1)
            {
                folder.Write(
                    ConvertExpression(mPartitions[0].SelectToken("source.expression")),
                    Path.Combine("queries", $"{table.Value<string>("name").SanitizeFilename()}.m")
                );
                mPartitions[0].Remove();
            }
            // TODO Determine payload for Incremental Refresh partitions...

            // Remove empty 'table.partitions'
            if (_table.SelectToken("partitions") is JArray partitions && partitions.Count == 0)
            {
                partitions.Parent.Remove();
            }

            return _table;
        }

        internal static JObject SerializeDataSources(JObject db, IProjectFolder folder, IQueriesLookup idCache)
        {
            // if Provider=PowerBI, strip out global pipe
            // if mashup, strip out & extract package

            // /tables/{name}/dataSources
            //   ./{name}
            //     /{name}.json  { name, impersonationMode, connectionString:Provider,location }
            //     /mashup
            //       /Formulas/Section1.m ...

            if (!(db.SelectToken("model.dataSources") is JArray dataSources)) return db;

            // /tables sub-folder
            // remove from db
            // sanitize filenames 

            foreach (var dataSource in dataSources.OfType<JObject>())
            {
                var name = dataSource["name"]?.Value<string>();  //TODO replace name using IDCache
                if (name == null) continue;
                var dir = name;

                // connectionString: Global Pipe, Mashup
                var connectionStringToken = dataSource["connectionString"] as JValue;
                var connectionString = connectionStringToken?.Value<string>();
                if (connectionStringToken != null && IsPowerBIConnectionString(connectionString, out var location, out var mashup))
                {
                    // lookup static name
                    name = idCache.LookupOriginalDataSourceId(name); // idCache is traversing via location
                    dataSource["name"] = name;
                    dir = location;
                    // strip values:
                    var bldr = new OleDbConnectionStringBuilder(connectionString);
                    bldr.Remove("global pipe");
                    bldr.Remove("mashup");
                    // keep Provider, Location
                    connectionStringToken.Value = bldr.ConnectionString;

                    var mashupPrefix = Path.Combine(
                        "dataSources",
                        location,
                        "mashup");
                    MashupSerializer.ExtractMashup(folder, mashupPrefix, mashup);
                }

                folder.Write(dataSource, $@"dataSources\{dir.SanitizeFilename()}\dataSource.json");
            }

            db.Value<JObject>("model").Remove("dataSources");

            return db;
        }

        internal static JObject SerializeExpressions(JObject db, IProjectFolder folder)
        {
            if (!(db.SelectToken("model.expressions") is JArray expressions)) return db;

            foreach (var expression in expressions.OfType<JObject>())
            {
                var name = expression["name"]?.Value<string>();
                if (name == null) continue;

                if (expression.Value<string>("kind") == "m")
                {
                    // TODO Account for queryfolders!
                    folder.Write(
                        ConvertExpression(expression.SelectToken("expression")),
                        Path.Combine("queries", $"{name.SanitizeFilename()}.m")
                    );

                    expression.Remove("expression"); // keeps annotations and other metadata in place
                }
            }

            return db;
        }

        // ReSharper disable once InconsistentNaming
        private static bool IsPowerBIConnectionString(string connectionString, out string location, out string mashup)
        {
            try
            {
                var bldr = new OleDbConnectionStringBuilder(connectionString);
                if (bldr.Provider.Equals("Microsoft.PowerBI.OleDb", StringComparison.InvariantCultureIgnoreCase)
                    && bldr.TryGetValue("mashup", out var _mashup)
                    && bldr.TryGetValue("location", out var _location))
                {
                    location = _location.ToString();
                    mashup = _mashup.ToString();
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // invalid connection string
            }

            location = null;
            mashup = null;
            return false;
        }


        internal static void SaveDatabase(JObject db, IProjectFolder folder)
        {
            folder.Write(db, "database.json");
        }


        private static Action<TextWriter> WriteMeasureXml(JToken json)
        {
            return writer =>
            {
                using (var xml = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true, WriteEndDocumentOnClose = true }))
                {
                    xml.WriteStartElement("Measure");
                    xml.WriteAttributeString("Name", json.Value<string>("name"));

                    // Handle Expression
                    if (json["expression"] != null)
                    {
                        xml.WriteStartElement("Expression");
                        if (json["expression"] is JArray expr)
                        {
                            xml.WriteCData(String.Join(Environment.NewLine, expr.ToObject<string[]>()));
                        }
                        else
                        {
                            xml.WriteCData(json.Value<string>("expression"));
                        }
                        xml.WriteEndElement();
                    }

                    // Any other properties
                    foreach (var prop in json.Values<JProperty>().Where(p => !(new[] { "name", "expression", "annotations", "extendedProperties" }).Contains(p.Name)))
                    {
                        xml.WriteStartElement(prop.Name.ToPascalCase());
                        xml.WriteValue(prop.Value.Value<string>());
                        xml.WriteEndElement();
                    }

                    // ExtendedProperties
                    if (json["extendedProperties"] is JArray extendedProperties)
                    {
                        foreach (var extendedProperty in extendedProperties)
                        {
                            xml.WriteStartElement("ExtendedProperty");
                            {
                                xml.WriteCData(extendedProperty.ToString());
                            }
                            xml.WriteEndElement();
                        }
                    }

                    // Annotations
                    if (json["annotations"] is JArray annotations)
                    {
                        foreach (var annotation in annotations)
                        {
                            xml.WriteStartElement("Annotation");
                            xml.WriteAttributeString("Name", annotation.Value<string>("name"));
                            var value = annotation?.Value<string>("value");
                            try
                            {
                                XElement.Parse(value).WriteTo(xml);
                            }
                            catch (XmlException)
                            {
                                xml.WriteValue(value);
                            }
                            xml.WriteEndElement();
                        }
                    }
                }
            };
        }

        // Handle Data Sources, ID lookups, mashup blobs, Pipe handles
        // .ids.json
        // global.settings -- pbix-tools settings (all)
        // [name].settings -- pbix-tools settings (specific pbix)
        // database.json (model/relationships,model/annotations)
        //   * ignore timestamps (createdTimestamp, lastUpdate, lastSchemaUpdate, lastProcessed, model.modifiedTime, model.structureModifiedTime
        //   * ignore: Annotation["DataTypeAtRefresh"]
        // /dataSources []
        //   /[name]
        //     [name].json -- keep provider, replace global pipe, remove mashup
        //     /mashup
        //      {package}
        // /tables []
        //   /[name] -- must encode invalid characters '/' -> '%2f' (Format: x2)
        //     [name].json (columns, annotations)
        //     /partitions ([name].json)
        //     /measures (XML format??) -- expression: CDATA, annotations['Format'] as xml
        //     /hierarchies
        // /expressions (extract into *.m files)

        public bool TryDeserialize(out JObject database)
        {
            database = null;
            if (!_folder.Exists()) return false;

            if (_folder.GetSubfolder("dataSources").Exists())
            {
                // TODO Support V1 models
                throw new NotSupportedException("Legacy PBIX models cannot be deserialized. Please convert the project to the V3 Power BI metadata format first.");
            }

            // handle: no /Model folder

            //   database.json -- model/relationships,model/annotations ++ tables,dataSources
            //   tables/{name}/table.json -- columns,*partitions*,annotations
            //   tables/{name}/measures/{measure}.xml -- Expression,FormatString,Annotation
            //   tables/{name}/measures/{measure}.json
            //   tables/{name}/hierarchies/{hierarchy}.json
            // **dataSources/{name}/dataSource.json -- Provider,Location
            // **dataSources/{name}/mashup/** (ZipArchive)

            var db = _folder.GetFile("database.json").ReadJson();

            var model = db.Value<JObject>("model");

            // append tables (convert measures, partitions)
            var tables = DeserializeTables(_folder);
            model.Add("tables", tables);

            // Legacy: datasources

            // expressions (queries) -- exclude table (non shared) expressions
            DeserializeExpressions(_folder, model);

            database = db;
            return true;
        }


        internal static JArray DeserializeTables(IProjectFolder modelFolder)
        {
            var tables = new List<JObject>();
            var tablesFolder = modelFolder.GetSubfolder("tables");
            var queriesFolder = modelFolder.GetSubfolder("queries");

            foreach (var tableFolder in tablesFolder.GetSubfolders("*"))
            {
                var tableFile = tableFolder.GetFile("table.json");
                if (!tableFile.Exists()) continue;
                var tableJson = tableFile.ReadJson();
                var tableName = tableJson.Value<string>("name");

                // partitions -- check if partition is already M partition -- need to escape query name: #"{name}"
                if (tableJson.ContainsKey("partitions"))
                {
                    foreach (var partition in tableJson["partitions"] as JArray)
                    {
                        // Skips calculated and m partitions
                        if (partition.SelectToken("source.query") != null)
                        {
                            partition["source"] = new JObject // overwrites existing QueryPartitionSource
                            {
                                { "type", "m" },
                                { "expression", new JArray(new [] {
                                    $"let",
                                    $"    Source = #\"{tableJson.Value<string>("name")}\"",
                                    $"in",
                                    $"    Source"
                                })} // TODO Must find correct query name!!! (get from annotation 'LinkedQueryName')
                            };
                        }
                    }
                }

                // Get (single) query from /queries folder matching the current table's name
                var tableQuery = queriesFolder.GetFiles($"{tableName.SanitizeFilename()}.m", SearchOption.AllDirectories).FirstOrDefault();
                if (tableQuery != null)
                {
                    if (!tableJson.ContainsKey("partitions")) tableJson["partitions"] = new JArray();
                    var partitionsJson = tableJson["partitions"] as JArray;
                    var partitionName = partitionsJson.Count == 0
                        ? tableName
                        : $"{tableName}-{Guid.NewGuid()}";

                    partitionsJson.Add(new JObject
                    {
                        { "name", partitionName },
                        { "mode", "import" },
                        // { "queryGroup", "TODO" },
                        { "source", new JObject 
                        {
                            { "type", "m" },
                            { "expression", ConvertExpression(tableQuery.ReadText()) }
                        }}
                    });
                }

                var measuresFolder = tableFolder.GetSubfolder("measures");
                if (measuresFolder.Exists())
                {
                    tableJson.Add("measures", new JArray(
                        measuresFolder.GetFiles("*.xml")
                            .Select(file =>
                            {
                                Log.Verbose("Processing measure file: {Path}", file.Path);
                                var xml = file.ReadXml();
                                return ConvertMeasureXml(xml);
                            })
                            .Where(x => x != null)
                    ));
                }

                // hierarchies
                var hierarchiesFolder = tableFolder.GetSubfolder("hierarchies");
                if (hierarchiesFolder.Exists())
                {
                    tableJson.Add("hierarchies", new JArray(
                        hierarchiesFolder.GetFiles("*.json")
                            .Select(file => 
                            {
                                Log.Verbose("Processing hierarchies file: {Path}", file.Path);
                                return file.ReadJson();
                            })
                    ));
                }

                // TODO columns (if expanded)

                tables.Add(tableJson);
            }

            return new JArray(tables);
        }

        internal static void DeserializeExpressions(IProjectFolder modelFolder, JObject modelJson)
        {
            var queriesFolder = modelFolder.GetSubfolder("queries");
            if (!queriesFolder.Exists()) return;

            var tableNames = modelFolder.GetSubfolder("tables").GetSubfolders("*")
                .Select(f => Path.GetFileName(f.BasePath))
                .ToArray();

            // All *.m queries w/o a corresponding table: 
            var sharedExpressions = queriesFolder.GetFiles("*.m", SearchOption.AllDirectories)
                .Where(f => !tableNames.Contains(Path.GetFileNameWithoutExtension(f.Path)))
                .ToArray();

            if (sharedExpressions.Length == 0) return;

            // Ensure model.expressions node:
            var expressionsJson = modelJson["expressions"] as JArray;
            if (expressionsJson == null)
            {
                modelJson.Add("expressions", new JArray());
                expressionsJson = modelJson["expressions"] as JArray;
            }

            // Upsert each shared expression
            foreach (var expressionFile in sharedExpressions)
            {
                var exprName = Path.GetFileNameWithoutExtension(expressionFile.Path).UnsanitizeFilename();
                var exprJson = expressionsJson.SelectToken($"[?(@.kind == 'm' && @.name == '{exprName}')]") as JObject;
                if (exprJson == null)
                {
                    exprJson = new JObject 
                    {
                        { "name", exprName },
                        { "kind", "m" }
                    };
                    expressionsJson.Add(exprJson);
                }

                exprJson["expression"] = ConvertExpression(expressionFile.ReadText());
            }
        }

        public static JObject ConvertMeasureXml(XDocument xml)
        {
            if (xml == null) return default(JObject);

            var measure = new JObject {
                { "name", xml.Root.Attribute("Name")?.Value }
            };

            string ToCamelCase(string s)
            {
                var sb = new StringBuilder(s);
                if (sb.Length > 0) sb[0] = Char.ToLower(s[0]);
                return sb.ToString();
            };

            foreach (var element in xml.Root.Elements())
            {
                if (element.Name == "Annotation")
                {
                    var sb = new StringBuilder();

                    using (var writer = XmlWriter.Create(new StringWriter(sb), new XmlWriterSettings { Indent = false, OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
                    {
                        element.FirstNode.WriteTo(writer);
                    }

                    var annotation = new JObject {
                        { "name", element.Attribute("Name")?.Value },
                        { "value", sb.ToString() }
                    };

                    JArray GetOrInsertAnnotations()
                    {
                        if (measure["annotations"] is JArray array)
                            return array;
                        var newArray = new JArray();
                        measure.Add("annotations", newArray);
                        return newArray;
                    };

                    GetOrInsertAnnotations().Add(annotation);
                }
                else if (element.Name == "Expression")
                {
                    var expressionsArray = element.Value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    var token = expressionsArray.Length == 1 ? (JToken)expressionsArray[0] : new JArray(expressionsArray);
                    measure.Add("expression", token);
                }
                else if (element.Name == "IsHidden" && Boolean.TryParse(element.Value, out var boolValue))
                {
                    measure.Add(ToCamelCase(element.Name.LocalName), boolValue);
                }
                //		else if (Int64.TryParse(element.Value, out var intValue))
                //		{
                //			measure.Add(ToCamelCase(element.Name.LocalName), intValue);
                //		}
                //		else if (Double.TryParse(element.Value, out var doubleValue))
                //		{
                //			measure.Add(ToCamelCase(element.Name.LocalName), doubleValue);
                //		}
                else
                {
                    measure.Add(ToCamelCase(element.Name.LocalName), element.Value);
                }
            }

            return measure;
        }


        // TODO place AAS conversions into TabularModelConversion.ToAASModel(JObject db, JObject extensions)

    }

    public static class XmlExtensions
    {
        public static string ToPascalCase(this string s)
        {
            var sb = new StringBuilder(s);
            if (sb.Length > 0) sb[0] = Char.ToUpper(s[0]);
            return sb.ToString();
        }
    }

    public static class PathExtensions
    {

        private static readonly Dictionary<char, string> FilenameCharReplace = "\"<>|:*?/\\".ToCharArray().ToDictionary(c => c, c => $"%{((int)c):X}");
        // Note - This can be reversed via WebUtility.UrlDecode()

        public static string SanitizeFilename(this string name)
        {
            var sb = new StringBuilder();
            foreach (var c in name)
            {
                if (FilenameCharReplace.TryGetValue(c, out var s))
                    sb.Append(s);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string UnsanitizeFilename(this string name) => System.Net.WebUtility.UrlDecode(name);

    }
}
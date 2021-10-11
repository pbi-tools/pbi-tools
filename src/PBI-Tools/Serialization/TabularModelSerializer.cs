// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Serilog;

namespace PbiTools.Serialization
{
    using FileSystem;
    using ProjectSystem;

    /// <summary>
    /// Serializes a tabular database (represented as TMSL/json) into a <see cref="IProjectFolder"/>.
    /// </summary>
    public class TabularModelSerializer : IPowerBIPartSerializer<JObject>
    {

        private static readonly ILogger Log = Serilog.Log.ForContext<TabularModelSerializer>();

        public static string FolderName => "Model";

        private readonly IProjectFolder _modelFolder;
        private readonly ModelSettings _settings;
        private readonly IDictionary<string, string> _queries;


        public TabularModelSerializer(IProjectRootFolder rootFolder, ModelSettings settings, IDictionary<string, string> queries = null)
        {
            if (rootFolder == null) throw new ArgumentNullException(nameof(rootFolder));
            _modelFolder = rootFolder.GetFolder(FolderName);
            _settings = settings;
            _queries = queries ?? new Dictionary<string, string>();
        }

        public string BasePath => _modelFolder.BasePath;

        #region Model Serialization

        public bool Serialize(JObject db)
        {
            if (db == null) return false;

            Log.Information("Using tabular model serialization mode: {Mode}", _settings.SerializationMode);

            if (_settings.SerializationMode == ModelSerializationMode.Default)
            { 
                db = db.RemoveProperties(_settings?.IgnoreProperties);

                var dataSources = db.SelectToken("model.dataSources") as JArray ?? new JArray();
#if NETFRAMEWORK
                var idCache = new TabularModelIdCache(dataSources, _queries); // Applies to legacy PBIX files only (is ignored for V3 models)
#elif NET
                var idCache = default(IQueriesLookup);
#endif
                db = SerializeDataSources(db, _modelFolder, idCache);
                db = SerializeTables(db, _modelFolder, idCache);
                db = SerializeExpressions(db, _modelFolder);
                db = SerializeCultures(db, _modelFolder);
            }

            SaveDatabase(db, _modelFolder);

            return true;
        }

        internal static JObject SerializeTables(JObject db, IProjectFolder modelFolder, IQueriesLookup idCache)
        {
            if (!(db.SelectToken("model.tables") is JArray tables)) return db;

            foreach (var _table in tables.OfType<JObject>())
            {
                var name = _table["name"]?.Value<string>();
                if (name == null) continue;

                var table = _table;
                var pathPrefix = $@"tables\{name.SanitizeFilename()}";

                table = SerializeColumns(table, modelFolder, pathPrefix);
                table = SerializeMeasures(table, modelFolder, pathPrefix);
                table = SerializeHierarchies(table, modelFolder, pathPrefix);
                table = SerializeTablePartitions(table, modelFolder, pathPrefix, idCache);

                modelFolder.Write(table, $@"{pathPrefix}\table.json");
            }

            tables.Parent.Remove();

            return db;
        }

        internal static JObject SerializeColumns(JObject table, IProjectFolder modelFolder, string pathPrefix)
        {
            var columns = table["columns"]?.Value<JArray>();
            if (columns == null) return table;

            foreach (JObject column in columns)
            {
                var name = column["name"]?.Value<string>();
                if (name == null) continue;

                if (column["type"]?.Value<string>() == "calculated" && column.ContainsKey("expression"))
                {
                    var expression = column.SelectToken("expression");
                    modelFolder.Write(
                        ConvertExpression(expression),
                        Path.Combine(pathPrefix, "columns", $"{name.SanitizeFilename()}.dax")
                    );
                    expression.Parent.Remove();
                }

                modelFolder.Write(column, 
                    Path.Combine(pathPrefix, "columns", $"{name.SanitizeFilename()}.json")
                );
            }

            table = new JObject(table);
            table.Remove("columns");
            return table;
        }

        internal static JObject SerializeMeasures(JObject table, IProjectFolder modelFolder, string pathPrefix)
        {
            var measures = table["measures"]?.Value<JArray>();
            if (measures == null) return table;

            foreach (var measure in measures)
            {
                var name = measure["name"]?.Value<string>();
                if (name == null) continue;

                modelFolder.WriteText(
                    Path.Combine(pathPrefix, "measures", $"{name.SanitizeFilename()}.xml"), 
                    WriteMeasureXml(measure)
                );

                var expression = measure.SelectToken("expression");
                if (expression != null)
                { 
                    modelFolder.Write(
                        ConvertExpression(expression),
                        Path.Combine(pathPrefix, "measures", $"{name.SanitizeFilename()}.dax")
                    );
                }
            }

            table = new JObject(table);
            table.Remove("measures");
            return table;
        }

        internal static JObject SerializeHierarchies(JObject table, IProjectFolder modelFolder, string pathPrefix)
        {
            var hierarchies = table["hierarchies"]?.Value<JArray>();
            if (hierarchies == null) return table;

            foreach (var hierarchy in hierarchies)
            {
                var name = hierarchy["name"]?.Value<string>();
                if (name == null) continue;

                modelFolder.Write(hierarchy, 
                    Path.Combine(pathPrefix, "hierarchies", $"{name.SanitizeFilename()}.json")
                );
            }

            table = new JObject(table);
            table.Remove("hierarchies");
            return table;
        }

        internal static JObject SerializeCultures(JObject db, IProjectFolder modelFolder)
        {
            if (!(db.SelectToken("model.cultures") is JArray cultures)) return db;

            foreach (var _culture in cultures.OfType<JObject>())
            {
                var name = _culture["name"]?.Value<string>();
                if (name == null) continue;

                modelFolder.Write(_culture, $@"cultures\{name.SanitizeFilename()}.json");
            }

            cultures.Parent.Remove();

            return db;
        }

        internal static JObject SerializeTablePartitions(JObject table, IProjectFolder modelFolder, string pathPrefix, IQueriesLookup idCache)
        {
            // TODO Allow option to export raw partitions, w/o transforms

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
                modelFolder.Write(
                    ConvertExpression(mPartitions[0].SelectToken("source.expression")),
                    Path.Combine("queries", $"{table.Value<string>("name").SanitizeFilename()}.m")
                );
                mPartitions[0].Remove();
            }
            // TODO Determine payload for Incremental Refresh partitions...

            var calcPartitions = _table.SelectTokens("partitions[?(@.source.type == 'calculated')]").OfType<JObject>().ToArray();
            if (calcPartitions.Length == 1)
            {
                var expression = calcPartitions[0].SelectToken("source.expression");
                modelFolder.Write(
                    ConvertExpression(expression),
                    Path.Combine(pathPrefix, "table.dax")
                );
                expression.Parent.Remove(); // Only remove 'expression' property, but retain partition object
            }

            // Remove empty 'table.partitions'
            if (_table.SelectToken("partitions") is JArray partitions && partitions.Count == 0)
            {
                partitions.Parent.Remove();
            }

            return _table;
        }

        internal static JObject SerializeDataSources(JObject db, IProjectFolder modelFolder, IQueriesLookup idCache)
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
#if NETFRAMEWORK
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
                    var bldr = new DbConnectionStringBuilder() { ConnectionString = connectionString };
                    bldr.Remove("Global Pipe");
                    bldr.Remove("Mashup");
                    // keep Provider, Location
                    connectionStringToken.Value = bldr.ConnectionString;

                    var mashupPrefix = Path.Combine(
                        "dataSources",
                        location,
                        "mashup");
                    MashupSerializer.ExtractMashup(modelFolder, mashupPrefix, mashup);
                }
#endif
                modelFolder.Write(dataSource, $@"dataSources\{dir.SanitizeFilename()}\dataSource.json");
            }

            db.Value<JObject>("model").Remove("dataSources");

            return db;
        }

        internal static JObject SerializeExpressions(JObject db, IProjectFolder modelFolder)
        {
            if (!(db.SelectToken("model.expressions") is JArray expressions)) return db;

            foreach (var expression in expressions.OfType<JObject>())
            {
                var name = expression["name"]?.Value<string>();
                if (name == null) continue;

                if (expression.Value<string>("kind") == "m")
                {
                    // TODO Account for queryfolders!
                    modelFolder.Write(
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
                var bldr = new DbConnectionStringBuilder() { ConnectionString = connectionString };
                if (bldr.TryGetValue("Provider", out var provider)
                    && provider.ToString().Equals("Microsoft.PowerBI.OleDb", StringComparison.InvariantCultureIgnoreCase)
                    && bldr.TryGetValue("Mashup", out var _mashup)
                    && bldr.TryGetValue("Location", out var _location))
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

#endregion

#region Model Deserialization

        public bool TryDeserialize(out JObject database)
        {
            database = null;

            // handle: no /Model folder
            if (!_modelFolder.Exists()) return false;

            if (_modelFolder.GetSubfolder("dataSources").Exists())
            {
                // TODO Support V1 models
                throw new NotSupportedException("Deserialization of legacy PBIX models is not supported. Please convert the project to the V3 Power BI metadata format first.");
            }

            //   database.json -- model/relationships,model/annotations ++ tables,dataSources
            //   tables/{name}/table.json -- columns,*partitions*,annotations
            //   tables/{name}/table.dax  -- optional, for calculated tables
            //   tables/{name}/measures/{measure}.xml -- FormatString,Annotation
            //   tables/{name}/measures/{measure}.dax
            //   tables/{name}/columns/{column}.json
            //   tables/{name}/columns/{column}.dax -- optional, for calculated columns only
            //   tables/{name}/hierarchies/{hierarchy}.json
            //   queries/{name}.m
            //   cultures/{name}.json
            // **dataSources/{name}/dataSource.json -- Provider,Location
            // **dataSources/{name}/mashup/** (ZipArchive)

            var db = _modelFolder.GetFile("database.json").ReadJson();

            var model = db.Value<JObject>("model");

            // append tables (convert measures, partitions)
            var tables = DeserializeTables(_modelFolder);
            if (tables != null)
                model.Add("tables", tables);

            // expressions (queries) -- exclude table (non shared) expressions
            DeserializeExpressions(_modelFolder, model);

            // cultures
            DeserializeCultures(_modelFolder, model);

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

                if (!tableJson.ContainsKey("partitions")) tableJson["partitions"] = new JArray();
                var partitionsJson = tableJson["partitions"] as JArray;

                if (partitionsJson.Count == 1) // TODO Handle multiple partitions
                {
                    var partition = partitionsJson[0];

                    // Legacy models: Convert to M partition
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

                    if (partition.SelectToken("source.type")?.Value<string>() == "calculated")
                    { 
                        var daxFile = tableFolder.GetFile("table.dax");
                        if (daxFile.Exists())
                        { 
                            partition["source"]["expression"] = ConvertExpression(daxFile.ReadText());
                        }
                    }
                }

                // Get (single) query from /queries folder matching the current table's name
                var tableQuery = queriesFolder.GetFiles($"{tableName.SanitizeFilename()}.m", SearchOption.AllDirectories).FirstOrDefault();
                if (tableQuery != null && partitionsJson.Count == 0)
                {
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

                DeserializeMeasures(tableFolder, tableJson);
                DeserializeColumns(tableFolder, tableJson);
                DeserializeHierarchies(tableFolder, tableJson);

                tables.Add(tableJson);
            }

            return tables.Count > 0 ? new JArray(tables) : null;
        }

        internal static void DeserializeMeasures(IProjectFolder tableFolder, JObject tableJson) =>
            DeserializeTablePropertyFromFolder(tableFolder, tableJson, "measures", 
                (measuresFolder, file) =>
                {
                    Log.Verbose("Processing measure file: {Path}", file.Path);
                    var json = ConvertMeasureXml(file.ReadXml());
                    var daxFile = measuresFolder.GetFile($"{Path.GetFileNameWithoutExtension(file.Path)}.dax");
                    if (daxFile.Exists()) { 
                        json["expression"] = ConvertExpression(daxFile.ReadText());
                    }
                    return json;
                }, 
                "*.xml"
            );

        internal static void DeserializeColumns(IProjectFolder tableFolder, JObject tableJson) =>
            DeserializeTablePropertyFromFolder(tableFolder, tableJson, "columns", 
                (columnsFolder, file) =>
                {
                    Log.Verbose("Processing column file: {Path}", file.Path);
                    var json = file.ReadJson();
                    var daxFile = columnsFolder.GetFile($"{Path.GetFileNameWithoutExtension(file.Path)}.dax");
                    if (daxFile.Exists()) { 
                        json["expression"] = ConvertExpression(daxFile.ReadText());
                    }
                    return json;
                }
            );

        internal static void DeserializeHierarchies(IProjectFolder tableFolder, JObject tableJson) =>
            DeserializeTablePropertyFromFolder(tableFolder, tableJson, "hierarchies", 
                (_, file) =>
                {
                    Log.Verbose("Processing hierarchies file: {Path}", file.Path);
                    return file.ReadJson();
                }
            );

        private static void DeserializeTablePropertyFromFolder(IProjectFolder tableFolder, JObject tableJson, string name, Func<IProjectFolder, IProjectFile, JToken> transformFile, string searchPattern = "*.json")
        { 
            var subFolder = tableFolder.GetSubfolder(name);
            if (subFolder.Exists())
            {
                tableJson.Add(name, new JArray(
                    subFolder
                        .GetFiles(searchPattern)
                        .Select(file => transformFile(subFolder, file))
                        .Where(x => x != null)
                ));
            }
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

        internal static void DeserializeCultures(IProjectFolder modelFolder, JObject modelJson)
        {
            var culturesFolder = modelFolder.GetSubfolder("cultures");
            if (culturesFolder.Exists())
            { 
                modelJson.Add("cultures", new JArray(
                    culturesFolder
                        .GetFiles("*.json")
                        .Select(file => {
                            Log.Verbose("Processing culture file: {Path}", file.Path);
                            return file.ReadJson();
                        })
                        .Where(x => x != null)
                ));
            }
        }

#endregion

#region DAX Expressions

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

#endregion

#region Measures

        private static Action<TextWriter> WriteMeasureXml(JToken json)
        {
            return writer =>
            {
                using (var xml = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true, WriteEndDocumentOnClose = true }))
                {
                    xml.WriteStartElement("Measure");
                    xml.WriteAttributeString("Name", json.Value<string>("name"));

#if false // Removed in pbixproj v0.6
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
#endif
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

#endregion

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
}
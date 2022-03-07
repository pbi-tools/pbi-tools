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
    using Utils;

    /// <summary>
    /// Serializes a tabular database (represented as TMSL/json) into a <see cref="IProjectFolder"/>.
    /// </summary>
    public class TabularModelSerializer : IPowerBIPartSerializer<JObject>
    {

        internal static readonly ILogger Log = Serilog.Log.ForContext<TabularModelSerializer>();

        public const string FolderName = "Model";
        public const string DefaultDatabaseFileName = "database.json";

        public static class Names
        {
            public const string DataSources = "dataSources";
            public const string Tables = "tables";
            public const string Cultures = "cultures";
            public const string Queries = "queries";
            public const string Columns = "columns";
            public const string Hierarchies = "hierarchies";
            public const string Measures = "measures";
            public const string Partitions = "partitions";
            public const string Annotations = "annotations";
            public const string Expressions = "expressions";
            public const string Relationships = "relationships";
        }


        private readonly IProjectFolder _modelFolder;
        private readonly ModelSettings _settings;
        private readonly IDictionary<string, string> _queries;
        private readonly string _fileName = DefaultDatabaseFileName;


        public TabularModelSerializer(IProjectRootFolder rootFolder, ModelSettings settings, IDictionary<string, string> queries = null)
        {
            if (rootFolder == null) throw new ArgumentNullException(nameof(rootFolder));
            _modelFolder = rootFolder.GetFolder(FolderName);
            _settings = settings;
            _queries = queries ?? new Dictionary<string, string>();
        }

        public TabularModelSerializer(IProjectFolder modelFolder, ModelSettings settings, string dbFileName = DefaultDatabaseFileName)
        {
            _modelFolder = modelFolder ?? throw new ArgumentNullException(nameof(modelFolder));
            _settings = settings;
            _fileName = dbFileName;
            _queries = new Dictionary<string, string>();
        }

        public string BasePath => _modelFolder.BasePath;

        #region Model Serialization

        public bool Serialize(JObject db)
        {
            if (db == null) return false;

            Log.Information("Using tabular model serialization mode: {Mode}", _settings.SerializationMode);

            if (_settings.SerializationMode == ModelSerializationMode.Default)
            { 
                db = db
                    .RemoveProperties(_settings?.IgnoreProperties)
                    .ApplyAnnotationRules(_settings?.Annotations);

#if NETFRAMEWORK
                var dataSources = db.SelectToken("model.dataSources") as JArray ?? new JArray();
                var idCache = new TabularModelIdCache(dataSources, _queries); // Applies to legacy PBIX files only (is ignored for V3 models)
#elif NET
                var idCache = default(IQueriesLookup);
#endif
                db = SerializeDataSources(db, _modelFolder, idCache);
                db = SerializeTables(db, _modelFolder, idCache, _settings.Measures);
                db = SerializeExpressions(db, _modelFolder);
                db = SerializeCultures(db, _modelFolder);

                // Perspectives
                // Roles
                // Translations
                // Relationships
            }

            // TODO: ModelSerializationMode.TabularEditor

            SaveDatabase(db, _modelFolder, _fileName);

            return true;
        }

        internal static JObject SerializeTables(JObject db, IProjectFolder modelFolder, IQueriesLookup idCache, ModelMeasureSettings measureSettings)
        {
            if (!(db.SelectToken("model.tables") is JArray tables)) return db;

            foreach (var _table in tables.OfType<JObject>())
            {
                var name = _table["name"]?.Value<string>();
                if (name == null) continue;

                var table = _table;
                var pathPrefix = $@"{Names.Tables}\{name.SanitizeFilename()}";

                table = SerializeColumns(table, modelFolder, pathPrefix);
                table = SerializeMeasures(table, modelFolder, pathPrefix, measureSettings);
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
                        Path.Combine(pathPrefix, Names.Columns, $"{name.SanitizeFilename()}.dax")
                    );
                    expression.Parent.Remove();
                }

                modelFolder.Write(column, 
                    Path.Combine(pathPrefix, Names.Columns, $"{name.SanitizeFilename()}.json")
                );
            }

            table = new JObject(table);
            table.Remove("columns");
            return table;
        }

        internal static JObject SerializeMeasures(JObject table, IProjectFolder modelFolder, string pathPrefix, ModelMeasureSettings settings)
        {
            var measures = table["measures"]?.Value<JArray>();
            if (measures == null) return table;

            foreach (var measure in measures)
            {
                var name = measure["name"]?.Value<string>();
                if (name == null) continue;

                if (settings.Format == ModelMeasureSerializationFormat.Xml
                    || (settings.Format == ModelMeasureSerializationFormat.Json && settings.ExtractExpression))
                {
                    var expression = measure.SelectToken("expression");
                    if (expression != null)
                    { 
                        modelFolder.Write(
                            ConvertExpression(expression),
                            Path.Combine(pathPrefix, Names.Measures, $"{name.SanitizeFilename()}.dax")
                        );

                        expression.Parent.Remove();
                    }
                }

                if (settings.Format == ModelMeasureSerializationFormat.Xml)
                {
                    modelFolder.WriteText(
                        Path.Combine(pathPrefix, Names.Measures, $"{name.SanitizeFilename()}.xml"), 
                        WriteMeasureXml(measure)
                    );
                }
                else
                {
                    modelFolder.Write(measure, Path.Combine(pathPrefix, Names.Measures, $"{name.SanitizeFilename()}.json"));
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
                    Path.Combine(pathPrefix, Names.Hierarchies, $"{name.SanitizeFilename()}.json")
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

                modelFolder.Write(_culture, $@"{Names.Cultures}\{name.SanitizeFilename()}.json");
            }

            cultures.Parent.Remove();

            return db;
        }

        internal static JObject SerializeTablePartitions(JObject table, IProjectFolder modelFolder, string pathPrefix, IQueriesLookup idCache)
        {
/*  public enum PartitionSourceType
    {
        *Query = 1,
        *Calculated = 2,
        None = 3,
        *M = 4,
        Entity = 5,
        PolicyRange = 6,
        CalculationGroup = 7,
        Inferred = 8
    }*/

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
                var expression = mPartitions[0].SelectToken("source.expression");
                modelFolder.Write(
                    ConvertExpression(expression),
                    Path.Combine(Names.Queries, $"{table.Value<string>("name").SanitizeFilename()}.m")
                );
                expression.Parent.Remove(); // Only remove 'expression' property, but retain partition object
            }

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

                // connectionString: Global Pipe, Mashup
                var connectionStringToken = dataSource["connectionString"] as JValue;
                var connectionString = connectionStringToken?.Value<string>();
                if (connectionStringToken != null && IsPowerBIConnectionString(connectionString, out var location, out var mashup))
                {
#if NETFRAMEWORK
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
                        Names.DataSources,
                        dir.SanitizeFilename(),
                        "mashup");
                    MashupSerializer.ExtractMashup(modelFolder, mashupPrefix, mashup);
#endif
                }
                modelFolder.Write(dataSource, $@"{Names.DataSources}\{dir.SanitizeFilename()}\dataSource.json");
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
                        Path.Combine(Names.Queries, $"{name.SanitizeFilename()}.m")
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


        internal static void SaveDatabase(JObject db, IProjectFolder folder, string filename = DefaultDatabaseFileName)
        {
            folder.Write(db, filename);
        }

#endregion

        #region Model Deserialization

        public bool TryDeserialize(out JObject database)
        {
            database = null;

            // handle: no /Model folder
            if (!_modelFolder.Exists()) return false;

            if (_modelFolder.GetSubfolder(Names.DataSources).Exists())
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
            var tables = DeserializeTables(_modelFolder);  // "tables", "tables/columns", "tables/hierarchies", "tables/measures"
            if (tables != null)
                model.Add("tables", tables);

            // expressions (queries) -- exclude table (non shared) expressions
            DeserializeExpressions(_modelFolder, model);   // "queries"

            // cultures
            DeserializeCultures(_modelFolder, model);      // "cultures"

            // Support TE folders:
            /*
    "Data Sources",
    "Perspectives",
    "Relationships",
    "Roles",
    "Shared Expressions",
    "Tables",
    "Tables/Annotations",
    "Tables/Calculation Items",
    "Tables/Columns",
    "Tables/Hierarchies",
    "Tables/Measures",
    "Tables/Partitions",
    "Translations"
             */

            database = db;
            return true;
        }


        internal static JArray DeserializeTables(IProjectFolder modelFolder)
        {
            var tables = new List<JObject>();
            var tablesFolder = modelFolder.GetSubfolder(Names.Tables);
            var queriesFolder = modelFolder.GetSubfolder(Names.Queries); // Contains M queries in PbixProj format

            foreach (var tableFolder in tablesFolder.GetSubfolders("*"))
            {
                var tableFile = tableFolder.GetFirstFile("table.json", $"{tableFolder.Name}.json");
                if (!tableFile.Exists()) {
                    // TODO Log Warning
                    continue;
                }

                var tableJson = tableFile.ReadJson();
                var tableName = tableJson.Value<string>("name");
                var tableQuery = queriesFolder.GetFiles($"{tableName.SanitizeFilename()}.m", SearchOption.AllDirectories).FirstOrDefault();

                // PARTITIONS
                var partitionsFolder = tableFolder.GetSubfolder(Names.Partitions);
                if (partitionsFolder.Exists())
                {
                    // If '/Partitions' folder exists, any existing partitions[] element is replaced!
                    tableJson["partitions"] = new JArray(partitionsFolder
                        .GetFiles("*.json")
                        .Select(f => f.ReadJson())
                    );
                }
                else
                {
                    // No '/Partitions' folder: Partitions are either implicit (from 'Queries') or held in table json already

                    var partitionsJson = tableJson.EnsureArray("partitions");

                    if (partitionsJson.Count == 1) // TODO Handle multiple partitions
                    {
                        var partition = partitionsJson[0] as JObject;

                        // LEGACY models only: Convert to M partition
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

                        // Calculated table: Insert partition expression from *.dax file (if exists)
                        else if (partition.SelectToken("source.type")?.Value<string>() == "calculated")
                        {
                            var daxFile = tableFolder.GetFile("table.dax");
                            if (daxFile.Exists())
                            {
                                partition["source"]["expression"] = ConvertExpression(daxFile.ReadText());
                            }
                        }

                        // M partition: Merge expression
                        else if (tableQuery != null && (partition.SelectToken("source") == null || partition.SelectToken("source.type")?.Value<string>() == "m"))
                        {
                            partition.Merge(new JObject {
                                { "source", new JObject {
                                    { "type", "m" },
                                    { "expression", ConvertExpression(tableQuery.ReadText()) }
                                }}
                            });
                        }

                    } // Single partition present

                    // Get (single) query from '/queries' folder matching the current table's name
                    if (tableQuery != null && partitionsJson.Count == 0)
                    {
                        var partitionName = partitionsJson.Count == 0
                            ? tableName
                            : $"{tableName}-{Guid.NewGuid()}"; // TODO Provide option to configure generated partition name

                        partitionsJson.Add(new JObject
                        {
                            { "name", partitionName },
                            { "mode", "import" },
                            { "source", new JObject
                            {
                                { "type", "m" },
                                { "expression", ConvertExpression(tableQuery.ReadText()) }
                            }}
                        });
                    }
                }


                DeserializeMeasures(tableFolder, tableJson);
                DeserializeColumns(tableFolder, tableJson);
                DeserializeHierarchies(tableFolder, tableJson);

                // ANNOTATIONS (TE Format)
                var annotationsFolder = tableFolder.GetSubfolder(Names.Annotations);
                if (annotationsFolder.Exists())
                {
                    tableJson["annotations"] = new JArray(annotationsFolder
                        .GetFiles("*.json")
                        .Select(f => f.ReadJson())
                    );
                }


                // Calculation Items

                tables.Add(tableJson);
            }

            return tables.Count > 0 ? new JArray(tables) : null;
        }

        internal static void DeserializeMeasures(IProjectFolder tableFolder, JObject tableJson) =>
            DeserializeTablePropertyFromFolder(tableFolder, tableJson, Names.Measures,
                (measuresFolder, file) =>
                {
                    Log.Verbose("Processing measure file: {Path}", file.Path);

                    var extension = file.Path.GetExtension();

                    JObject json = null;
                    if (extension == ".json") {
                        json = file.ReadJson();
                    }
                    else if (extension == ".xml") {
                        var jsonFile = measuresFolder.GetFile($"{file.Path.WithoutExtension()}.json");
                        if (jsonFile.Exists())
                            return null; // Skip the xml file, json takes precedence
                        else
                            json = ConvertMeasureXml(file.ReadXml());
                    }
                    else // Ignore any other extensions
                        return null;

                    var daxFile = measuresFolder.GetFile($"{file.Path.WithoutExtension()}.dax");
                    if (daxFile.Exists())
                    {
                        json["expression"] = ConvertExpression(daxFile.ReadText());
                    }
                    return json;
                }, 
                "*.*"
            );

        internal static void DeserializeColumns(IProjectFolder tableFolder, JObject tableJson) =>
            DeserializeTablePropertyFromFolder(tableFolder, tableJson, Names.Columns, 
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
            DeserializeTablePropertyFromFolder(tableFolder, tableJson, Names.Hierarchies, 
                (_, file) =>
                {
                    Log.Verbose("Processing hierarchies file: {Path}", file.Path);
                    return file.ReadJson();
                }
            );

        /// <summary>
        /// Deserializes items from a table subfolder into an array property of the TOM table object.
        /// </summary>
        /// <param name="tableFolder">The PbixProj table folder.</param>
        /// <param name="tableJson">The TOM Table object to populate.</param>
        /// <param name="name">The name of the TOM Table property to create. Also the expected subfolder name.</param>
        /// <param name="transformFile">A function transforming a file into a JToken. A file will be skipped if <c>null</c> is returned.</param>
        /// <param name="searchPattern">The file search pattern. Default: '*.json'.</param>
        private static void DeserializeTablePropertyFromFolder(IProjectFolder tableFolder, JObject tableJson, string name, Func<IProjectFolder, IProjectFile, JToken> transformFile, string searchPattern = "*.json")
        { 
            var subFolder = tableFolder.GetSubfolder(name);
            if (subFolder.Exists())
            {
                // TODO Support json merge
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
            var queriesFolder = modelFolder.GetSubfolder(Names.Queries);
            if (!queriesFolder.Exists()) return;

            var tableNames = modelFolder.GetSubfolder(Names.Tables).GetSubfolders("*")
                .Select(f => Path.GetFileName(f.BasePath))
                .ToArray();

            // All *.m queries w/o a corresponding table: 
            var sharedExpressions = queriesFolder.GetFiles("*.m", SearchOption.AllDirectories)
                .Where(f => !tableNames.Contains(Path.GetFileNameWithoutExtension(f.Path)))
                .ToArray();

            if (sharedExpressions.Length == 0) return;

            // Ensure model.expressions node:
            var expressionsJson = modelJson.EnsureArray("expressions");

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
            var culturesFolder = modelFolder.GetSubfolder(Names.Cultures);
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

        internal static Action<TextWriter> WriteMeasureXml(JToken json)
        {
            return writer =>
            {
                using (var xml = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true, WriteEndDocumentOnClose = true }))
                {
                    xml.WriteStartElement("Measure");
                    xml.WriteAttributeString("Name", json.Value<string>("name"));

                    // Any other properties
                    foreach (var prop in json.Values<JProperty>().Where(p => !(new[] { "name", "expression", "annotations", "extendedProperties" }).Contains(p.Name)))
                    {
                        xml.WriteStartElement(prop.Name.ToPascalCase());
                        try {
                            xml.WriteValue(prop.Value.Value<string>());
                        }
                        catch (System.InvalidCastException) {
                            xml.WriteCData(prop.Value.ToString());
                        }
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

        internal static JObject ConvertMeasureXml(XDocument xml)
        {
            if (xml == null) return default(JObject);

            var measure = new JObject {
                { "name", xml.Root.Attribute("Name")?.Value }
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

                    measure.EnsureArray("annotations").Add(annotation);
                }
                else if (element.Name == "ExtendedProperty")
                {
                    var extendedProperty = JObject.Parse(element.Value);
                    measure.EnsureArray("extendedProperties").Add(extendedProperty);
                }
                else if (element.Name == "Expression")
                {
                    var expressionsArray = element.Value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    var token = expressionsArray.Length == 1 ? (JToken)expressionsArray[0] : new JArray(expressionsArray);
                    measure.Add("expression", token);
                }
                else if (element.Name == "IsHidden" && Boolean.TryParse(element.Value, out var boolValue))
                {
                    measure.Add(element.Name.LocalName.ToCamelCase(), boolValue);
                }
                else if (element.Value.TryParseJsonObject(out var obj))
                { 
                    measure.Add(element.Name.LocalName.ToCamelCase(), obj);
                }
                else if (element.Value.TryParseJsonArray(out var array))
                { 
                    measure.Add(element.Name.LocalName.ToCamelCase(), array);
                }
                else
                {
                    measure.Add(element.Name.LocalName.ToCamelCase(), element.Value);
                }
            }

            return measure;
        }

        #endregion

        // TODO place AAS conversions into TabularModelConversion.ToAASModel(JObject db, JObject extensions)

    }

}
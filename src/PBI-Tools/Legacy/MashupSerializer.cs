// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Mashup.Client.Packaging.SerializationObjectModel;
using Microsoft.Mashup.Engine.Interface;
using Microsoft.Mashup.Host.Document;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace PbiTools.Serialization
{
    using FileSystem;
    using Model;
    using ProjectSystem;
    using Utils;

    // ReSharper disable IdentifierTypo
    public class MashupSerializer : IPowerBIPartSerializer<MashupParts>
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<MashupSerializer>();

        // ReSharper disable once StringLiteralTypo
        public static string FolderName => "Mashup";
        internal IProjectFolder MashupFolder { get; }
        private readonly MashupSettings _settings;
    
        private static class Names
        {
            public static string PackageFolder => "Package";
            public static string MetadataFolder => "Metadata";
            public static string MetadataFile => "metadata.xml";
            public static string ContentsFolder => "Contents";
            public static string PermissionsFile => "permissions.json";
            public static string QueryGroupsFile => "Metadata/queryGroups.json";
        }

        private static readonly XmlSerializer PackageMetadataSerializer = new XmlSerializer(typeof(SerializedPackageMetadata));

        public MashupSerializer(IProjectRootFolder rootFolder, MashupSettings settings)
        {
            if (rootFolder == null) throw new ArgumentNullException(nameof(rootFolder));
            this.MashupFolder = rootFolder.GetFolder(FolderName);
            _settings = settings;
        }

        public string BasePath => MashupFolder.BasePath;

#region Package

        internal void SerializePackage(ZipArchive archive)
        {
            var packageFolder = MashupFolder.GetSubfolder(Names.PackageFolder);

            foreach (var entry in archive.Entries)
            {
                Log.Debug("Processing Mashup/Package entry: {RelativePath}", entry.FullName);

                using (var entryStream = entry.Open())
                {
                    // *** RAW Serialization Mode ***

                    if (_settings.SerializationMode == MashupSerializationMode.Raw || _settings.SerializationMode == MashupSerializationMode.Default)
                    {
                        packageFolder.WriteFile(entry.FullName, stream =>
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            entryStream.CopyTo(stream);
                        });

                        continue;
                    }

                    // *** DEFAULT Serialization Mode ***

                    if (Path.GetExtension(entry.FullName) == ".xml")
                    {
                        var xml = XDocument.Load(entryStream);       // might throw XmlException
                        packageFolder.Write(xml, entry.FullName);    // ensures XML is formatted for readability
                    }
                    else if (Path.GetExtension(entry.FullName) == ".m")
                    {
                        using (var streamClone = new MemoryStream(entryStream.ReadAllBytes()))
                        {
                            if (TryExtractSectionMembers(streamClone, out var exports))
                            {
                                // Ensure we can create FOLDER "Section1.m" if FILE with same name already exists
                                if (packageFolder.ContainsFile(entry.FullName))
                                    packageFolder.DeleteFile(entry.FullName);

                                foreach (var export in exports)
                                {
                                    //                              /Formulas/Section1.m/Query1.m
                                    packageFolder.Write(export.Value,
                                        $"{entry.FullName}/{EscapeItemPathSegment(export.Key)}.m");
                                }
                            }
                            else
                            {
                                // Resetting stream here since we've already read from it above
                                streamClone.Seek(0, SeekOrigin.Begin);

                                // Fallback: Just extract the plain file
                                // ReSharper disable once AccessToDisposedClosure
                                packageFolder.WriteFile(entry.FullName, stream => streamClone.CopyTo(stream));
                            }
                        }
                    }
                    else
                    {
                        packageFolder.WriteFile(entry.FullName, stream =>
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            entryStream.CopyTo(stream);
                        });
                    }
                }
            }
        }

        internal static bool TryExtractSectionMembers(Stream mStream, out IDictionary<string, string> members)
        {
            try
            {
                string m;
                using (var reader = new StreamReader(mStream, Encoding.UTF8, true, 1024, leaveOpen: true))
                {
                    m = reader.ReadToEnd();
                }

                IEngine engine = Engines.Version1;
                var tokens = engine.Tokenize(m);
                var doc = engine.Parse(tokens, new TextDocumentHost(m), error => Log.Debug("MashupEngine parser error: {Message}", error.Message));

                if (doc is ISectionDocument sectionDocument)
                {
                    members = sectionDocument.Section.Members.ToDictionary(
                        export => export.Name.Name,
                        export => tokens.GetText(export.Range.Start, export.Range.End).ToString());
                    return members.Count > 0;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "An error occurred trying to extract exports from a M SectionDocument.");
            }
            members = null;
            return false;
        }

#endregion

#region Metadata

        internal void SerializeMetadata(XDocument xmlMetadata)
        {
            if (_settings.SerializationMode == MashupSerializationMode.Raw || _settings.SerializationMode == MashupSerializationMode.Default)
            {
                this.MashupFolder.Write(xmlMetadata, Names.MetadataFile);
                return;
            }

            var metadataFolder = MashupFolder.GetSubfolder(Names.MetadataFolder);

            var metadata = (SerializedPackageMetadata)PackageMetadataSerializer.Deserialize(xmlMetadata.CreateReader());

            var metadataJson = new JObject();

            JObject CreateEntry(string section, string path = null)
            {
                JObject jSection;
                if (metadataJson.TryGetValue(section, out JToken token))
                    jSection = (JObject) token;
                else
                    metadataJson.Add(section, jSection = new JObject());

                if (path == null)
                    return jSection;

                var jEntry = new JObject();
                jSection.Add(path, jEntry);
                return jEntry;
            };

            foreach (SerializedPackageItemMetadata item in metadata.Items)
            {
                var sectionName = item.ItemLocation.ItemType == SerializedPackageItemType.Formula
                    ? "Formulas"
                    : item.ItemLocation.ItemType.ToString();

                // Creates new JObject '"AllFormulas": { .. }' or '"Formulas": { "Section1/Query1": { .. } }' to be populated with item entries
                var entry = CreateEntry(sectionName, 
                    String.IsNullOrEmpty(item.ItemLocation.ItemPath) 
                    ? null 
                    : WebUtility.UrlDecode(item.ItemLocation.ItemPath)
                );

                // emit 'null' if there are no entries
                if (item.Entries == null || item.Entries.Length == 0)
                    entry.Replace(JValue.CreateNull());

                // Handling of item entries:
                // - Property name: entry.Type
                // - Skip 'AllFormulas/QueryGroups" as that is being serialized separately
                // - Special handling of 'LastAnalysisServicesFormulaText'
                //   - Extract 'RootFormulaText' into separate *.m file
                //   - Extract 'ReferencedQueriesFormulaText' into separate 'ReferencedQueries\*.m' files
                foreach (var metadataEntry in item.Entries.OrderBy(x => x.Type))
                {
                    // QueryGroups entry is being serialized into 'queryGroups.json'
                    if (metadataEntry.Type == "QueryGroups" && item.ItemLocation.ItemType == SerializedPackageItemType.AllFormulas)
                        continue;

                    var token = ConvertEntry(metadataEntry);

                    if (metadataEntry.Type == "LastAnalysisServicesFormulaText" 
                        && item.ItemLocation.ItemType == SerializedPackageItemType.Formula 
                        && token is JObject obj)
                    {
                        // ./Mashup/Metadata/Section1/Query1
                        var itemFolder = metadataFolder.GetSubfolder(EscapeItemPath(item.ItemLocation.ItemPath));

                        var rootFormulaText = token.Value<string>("RootFormulaText");
                        itemFolder.Write(rootFormulaText, "RootFormulaText.m");
                        obj.Remove("RootFormulaText");

                        var referencedQueries = token["ReferencedQueriesFormulaText"];
                        if (referencedQueries != null && referencedQueries is JObject refQueriesObj)
                        {
                            var queriesFolder = itemFolder.GetSubfolder("ReferencedQueries");
                            foreach (var queryProperty in refQueriesObj.Properties())
                            {
                                queriesFolder.Write(
                                    queryProperty.Value.ToString(), 
                                    $"{EscapeItemPathSegment(queryProperty.Name)}.m"
                                );
                            }
                            obj.Remove("ReferencedQueriesFormulaText");
                        }
                    }

                    entry.Add(metadataEntry.Type, token);
                }
            }

            metadataFolder.Write(metadataJson, "metadata.json");

            /*
/Mashup/Metadata <= ProjectFolder
 [./metadata.xml]
  ./metadata.json
  {
    "AllFormulas|{ItemPath}": {
      "IsPrivate": 0,
      "FillObjectType": "ConnectionOnly",
      "ResultType": "Text",
      "QueryGroupID": "ee99a7d9-458d-49d0-8ca1-7ff5e3a279cc"
    },
    "Section1/Revenue/Source": null
  }
  ./queryGroups.json
  ./Section1
   ./Query1
   [/metadata.json]
    /RootFormulaText.m
    ./ReferencedQueries
     /Query2.m
     /Query3.m
     /Query4.m
            */

            // convert ItemPath to valid folder ('%20' > ' ')
            // convert values (l,f,s,c,d) -- isContentID, isDateTime
            // convert JObject, JArray
            // extract RootFormulaText, ReferencedQueriesFormulaText ("IncludesReferencedQueries": true)
            // skip QueryGroups
        }

        internal static string EscapeItemPathSegment(string segment)
            => WebUtility.UrlDecode(segment).SanitizeFilename();
        internal static string EscapeItemPath(string path)
        {
            return String.Join("/", path
                .Split('/')
                .Select(EscapeItemPathSegment));
        }

        internal static JToken ConvertEntry(SerializedMetadataEntry metadataEntry)
        {
            if (metadataEntry.IsLong)
                return new JValue(metadataEntry.LongValue);
            if (metadataEntry.IsDouble)
                return new JValue(metadataEntry.DoubleValue);
            if (metadataEntry.IsGuid)
                return new JObject
                {
                    { "Value", metadataEntry.StringValue },
                    { "isContentID", true }
                };
            if (metadataEntry.IsDateTime)
                return new JObject
                {
                    { "Value", metadataEntry.StringValue },
                    { "isDateTime", true }
                };

            try
            {
                if (metadataEntry.StringValue.StartsWith("[") || metadataEntry.StringValue.StartsWith("{"))
                    return JToken.Parse(metadataEntry.StringValue);
            }
            catch (JsonException ex)
            {
                Log.Warning(ex, "Failed to convert entry value to Json token. {StringValue}", metadataEntry.StringValue);
            }

            return metadataEntry.StringValue;
        }
        

#endregion

        /// <summary>
        /// Extracts the mashup blob from a Power BI OleDb connection string into the given location of a ProjectFolder.
        /// Used by TabularModelSerializer to extract a mashup package into <c>Model/dataSources/{..}/mashup</c>
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="prefix"></param>
        /// <param name="base64MashupPackage"></param>
        public static void ExtractMashup(IProjectFolder folder, string prefix, string base64MashupPackage)
        {
            var mashup = Convert.FromBase64String(base64MashupPackage);  // FormatException
            using (var zip = new ZipArchive(new MemoryStream(mashup)))   // IO.InvalidDataException
            {
                foreach (var zipEntry in zip.Entries)
                {
                    var path = Path.Combine(prefix, zipEntry.FullName).Replace('/', '\\');

                    // M files - handle escape sequences
                    if (Path.GetExtension(zipEntry.Name).Equals(".m", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var reader = new StreamReader(zipEntry.Open()))
                        {
                            var m = MashupHelpers.ReplaceEscapeSeqences(reader.ReadToEnd());
                            folder.WriteText(path, writer =>
                            {
                                writer.Write(m);
                            });
                        }
                    }
                    // Format all XML files (except '[Content_Types].xml')
                    else if (Path.GetExtension(zipEntry.Name).Equals(".xml", StringComparison.OrdinalIgnoreCase)
                        && zipEntry.Name != "[Content_Types].xml")
                    {
                        var xml = XDocument.Load(zipEntry.Open());  // TODO Handle XmlException
                        folder.Write(xml, path);
                    }
                    else // any other files written as-is
                    {
                        folder.WriteFile(path, stream => 
                        {
                            using (var entry = zipEntry.Open())
                            {
                                entry.CopyTo(stream);
                            }
                        });
                    }
                }
            }
        }


        public bool Serialize(MashupParts content)
        {
            if (content == null) return false;

            Log.Information("Using mashup serialization mode: {Mode}", _settings.SerializationMode);
            
            /*
             *   /Mashup
             *   --/Package/
             *   -----/Config/Package.xml
             *   -----/Formulas/Section1.m/Query1.m
             *   --/Contents/
             *   --/Metadata/                    {metadataFolder}
             *   -----/metadata.xml (==='Raw' extraction mode===)
             *   -----/metadata.json
             *   -----/queryGroups.json          [MashupParts.QueryGroups]
             *   -----/Section1/
             *   --------/Query1/                {itemFolder}
             *   --------/RootFormulaText.m
             *   --------/ReferencedQueries/     {queriesFolder}
             *   -----------/Query2.m
             *   --/permissions.json
             */

            if (content.Package != null)
            {
                using (var zip = new ZipArchive(content.Package, ZipArchiveMode.Read, leaveOpen: true))
                {
                    this.SerializePackage(zip);
                }
            }

            if (content.Metadata != null)
            {
                this.SerializeMetadata(content.Metadata);
            }

            var contents = content.Content;
            if (contents != null && contents.Length > 0)
            {
                var contentsFolder = MashupFolder.GetSubfolder(Names.ContentsFolder);
                using (var zip = new ZipArchive(contents, ZipArchiveMode.Read, leaveOpen: true))
                {
                    foreach (var entry in zip.Entries)
                    {
                        contentsFolder.WriteFile(entry.FullName, stream =>
                        {
                            entry.Open().CopyTo(stream);
                        });
                    }
                }
            }

            if (content.QueryGroups != null && _settings.SerializationMode != MashupSerializationMode.Raw)
            {
                MashupFolder.Write(content.QueryGroups, Names.QueryGroupsFile);
            }

            if (content.Permissions != null)
            {
                MashupFolder.Write(content.Permissions, Names.PermissionsFile);
            }

            return true;
        }

        public bool TryDeserialize(out MashupParts part)
        {
            part = default(MashupParts);
            if (!this.MashupFolder.Exists()) return false;

            Log.Debug("Deserializing Mashup parts...");

            part = new MashupParts();

            var packageFolder = this.MashupFolder.GetSubfolder(Names.PackageFolder);
            if (packageFolder.Exists())
            { 
                part.Package = new MemoryStream();
                using (var zipArchive = new ZipArchive(part.Package, ZipArchiveMode.Create, leaveOpen: true))
                {
                    // *** Handle expanded Formulas/SectionX.m files ***

                    var filesProcessed = new HashSet<string>();
                    foreach (var formulaFolder in packageFolder.GetSubfolder("Formulas").GetSubfolders("*.m"))
                    {
                        var entry = zipArchive.CreateEntry($"Formulas/{formulaFolder.Name}");
                        Log.Verbose("Creating PackagePart entry: {ZipEntryPath} from: {FormulaFolder}", entry.FullName, formulaFolder.BasePath);

                        using (var writer = new StreamWriter(entry.Open()))
                        {
                            writer.WriteLine($"section {Path.GetFileNameWithoutExtension(formulaFolder.Name)};");
                            writer.WriteLine();
                            foreach (var sectionFile in formulaFolder.GetFiles("*.m"))
                            {
                                filesProcessed.Add(sectionFile.Path);
                                Log.Verbose("Adding file: {Path}", sectionFile.Path);

                                writer.WriteLine(sectionFile.ReadText());
                                writer.WriteLine();
                            }
                        }
                    }

                    // *** Handle all other files ***

                    foreach (var file in packageFolder.GetFiles("*.*", SearchOption.AllDirectories))
                    {
                        if (filesProcessed.Contains(file.Path)) continue;

                        var relativePath = file.GetRelativePath(packageFolder);

                        var entry = zipArchive.CreateEntry(relativePath);
                        Log.Verbose("Creating PackagePart entry: {ZipEntryPath}, Copying from: {SourceFilePath}", entry.FullName, file.Path);

                        using (var entryStream = entry.Open())
                        {
                            if (file.TryReadFile(out var sourceStream))
                            using (sourceStream)
                            {
                                sourceStream.CopyTo(entryStream);
                            }
                        }
                    }
                }
            }

            var metadataFolder = this.MashupFolder.GetSubfolder(Names.MetadataFolder);
            if (metadataFolder.Exists())
            {
                // Throw if Mashup was not extracted using Raw serialization mode
                if (metadataFolder
                    .GetFiles("*.*", SearchOption.AllDirectories)
                    .Count(f => Path.GetFileName(f.Path) != "queryGroups.json") > 0)
                {
                    throw new NotSupportedException("Deserialization is not supported for projects with Legacy/Expanded Mashup serialization mode. Extract the file using the Default or Raw modes for Mashup.");
                }
            }

            var metadataFile = this.MashupFolder.GetFile(Names.MetadataFile);
            if (metadataFile.Exists())
            {
                Log.Verbose("Creating MetadataPart from {Path}", metadataFile.Path);
                part.Metadata = metadataFile.ReadXml();
            }

            var contentsFolder = this.MashupFolder.GetSubfolder(Names.ContentsFolder);
            if (contentsFolder.Exists())
            {
                part.Content = new MemoryStream();
                using (var zipArchive = new ZipArchive(part.Content, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var file in contentsFolder.GetFiles("*.*", SearchOption.AllDirectories))
                    {
                        var relativePath = file.GetRelativePath(contentsFolder);

                        var entry = zipArchive.CreateEntry(relativePath);
                        Log.Verbose("Creating ContentPart entry: {ZipEntryPath}, Copying from: {SourceFilePath}", entry.FullName, file.Path);

                        using (var entryStream = entry.Open())
                        {
                            if (file.TryReadFile(out var sourceStream))
                            using (sourceStream)
                            {
                                sourceStream.CopyTo(entryStream);
                            }
                        }
                    }
                }
            }

            var queryGroupsFile = this.MashupFolder.GetFile(Names.QueryGroupsFile);
            if (queryGroupsFile.Exists())
            {
                Log.Verbose("Reading QueryGroups from {Path}", queryGroupsFile.Path);
                part.QueryGroups = queryGroupsFile.ReadJsonArray();
            }

            var permissionsFile = this.MashupFolder.GetFile(Names.PermissionsFile);
            if (permissionsFile.Exists())
            {
                Log.Verbose("Reading Permissions from {Path}", permissionsFile.Path);
                part.Permissions = permissionsFile.ReadJson();
            }

            return true;
        }
    }
}
#endif
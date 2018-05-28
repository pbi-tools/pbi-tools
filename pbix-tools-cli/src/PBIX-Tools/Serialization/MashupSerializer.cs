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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PbixTools.FileSystem;
using PbixTools.Utils;

namespace PbixTools.Serialization
{
    public class MashupSerializer
    {
        private readonly IProjectFolder _folder; /* './Mashup/' */

        public MashupSerializer(IProjectFolder folder)
        {
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        }

        public void SerializeMetadata(XDocument xmlMetdata)
        {
            var metadataFolder = _folder.GetSubfolder("Metadata");

            var serializer = new XmlSerializer(typeof(SerializedPackageMetadata));
            var metadata = (SerializedPackageMetadata)serializer.Deserialize(xmlMetdata.CreateReader());

            var json = new JObject();

            JObject CreateEntry(string section, string path = null)
            {
                JObject jSection;
                if (json.TryGetValue(section, out JToken token))
                    jSection = (JObject) token;
                else
                    json.Add(section, jSection = new JObject());

                if (path == null)
                    return jSection;

                var jEntry = new JObject();
                jSection.Add(path, jEntry);
                return jEntry;
            };

            foreach (var item in metadata.Items)
            {
                var sectionName = item.ItemLocation.ItemType == SerializedPackageItemType.Formula
                    ? "Formulas"
                    : item.ItemLocation.ItemType.ToString();

                var entry = CreateEntry(sectionName, String.IsNullOrEmpty(item.ItemLocation.ItemPath) ? null : WebUtility.UrlDecode(item.ItemLocation.ItemPath));

                if (item.Entries == null || item.Entries.Length == 0)
                    entry.Replace(JValue.CreateNull());

                foreach (var metadataEntry in item.Entries)
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
                                queriesFolder.Write(queryProperty.Value.ToString(), $"{EscapeItemPathSegment(queryProperty.Name)}.m");
                            }
                            obj.Remove("ReferencedQueriesFormulaText");
                        }
                    }

                    entry.Add(metadataEntry.Type, token);
                }
            }

            metadataFolder.Write(json, "metadata.json");

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
            // convert values (l,f,s,c,d)
            // convert JOBject, JArray
            // extract RootFormulaText, ReferencedQueriesFormulaText ("IncludesReferencedQueries": true)
        }

        private static readonly Dictionary<char, string> FilenameCharReplace = "\"<>|:*?/\\".ToCharArray().ToDictionary(c => c, c => $"%{((int)c):X}");

        internal static string EscapeItemPathSegment(string segment)
        {
            var sb = new StringBuilder();
            foreach (var c in WebUtility.UrlDecode(segment).ToCharArray())
            {
                if (FilenameCharReplace.TryGetValue(c, out var s))
                    sb.Append(s);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

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
            catch (JsonException)
            {
            }

            return metadataEntry.StringValue;
        }

        public bool TryDeserializeMetadata(out XDocument xmlMetadata)
        {
            throw new NotImplementedException();
        }

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
                    else if (Path.GetExtension(zipEntry.Name).Equals(".xml", StringComparison.OrdinalIgnoreCase)
                        && zipEntry.Name != "[Content_Types].xml")
                    {
                        var xml = XDocument.Load(zipEntry.Open());  // XmlException
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
    }
}
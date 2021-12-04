// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace PbiTools.FileSystem
{

    public static class ProjectFolderExtensions
    {

        public static void Write(this IProjectFolder folder, string text, string path)
        {
            folder.WriteText(path, writer =>
            {
                writer.Write(text);
            });
        }

        /// <summary>
        /// Writes the json token to a given path inside the project folder provided.
        /// The json payload is formatted for readability.
        /// Any existing file is replaced.
        /// </summary>
        public static void Write(this IProjectFolder folder, JToken json, string path)
        {
            folder.WriteText(path, writer =>
            {
                using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
                {
                    json.WriteTo(jsonWriter);
                }
            });
        }

        /// <summary>
        /// Writes the XML document to a given path inside the project folder provided.
        /// The XML payload is indented for readability, and the XML declaration is omitted.
        /// Any existing file is replaced.
        /// </summary>
        public static void Write(this IProjectFolder folder, XDocument xml, string path, bool omitXmlDeclaration = true)
        {
            folder.WriteText(path, writer =>
            {
                using (var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = omitXmlDeclaration }))
                {
                    xml.WriteTo(xmlWriter);
                }
            });
        }

        /// <summary>
        /// Tests a series of file paths against the project folder and returns the first file found.
        /// Returns null if none of the paths have existing files.
        /// </summary>
        public static IProjectFile GetFirstFile(this IProjectFolder folder, params string[] filePaths)
        {
            foreach (var path in filePaths ?? new string[0])
            {
                var file = folder.GetFile(path);
                if (file.Exists())
                    return file;
            }

            return default;
        }
    }

}
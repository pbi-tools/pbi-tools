// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Formatting = Newtonsoft.Json.Formatting;

namespace PbiTools.FileSystem
{
    public static class ProjectFileExtensions
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ProjectFile>();

        public static void Write(this IProjectFile file, JToken json)
        {
            file.WriteText(writer =>
            {
                using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
                {
                    json.WriteTo(jsonWriter);
                }
            });
        }

        public static void Write(this IProjectFile file, string text)
        {
            file.WriteText(writer =>
            {
                writer.Write(text);
            });
        }

        public static void Write(this IProjectFile file, XDocument xml, bool omitXmlDeclaration = true)
        {
            file.WriteText(writer =>
            {
                using (var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = omitXmlDeclaration }))
                {
                    xml.WriteTo(xmlWriter);
                }
            });
        }

        /// <summary>
        /// Parses the file contents as a Json object.
        /// Returns an empty object in case of a Json parser error.
        /// </summary>
        public static JObject ReadJson(this IProjectFile file, JsonLoadSettings settings = null)
        {
            if (file.TryReadFile(out var stream))
            {
                using (var reader = new JsonTextReader(new StreamReader(stream)))
                {
                    try
                    {
                        return JObject.Load(reader, settings);
                    }
                    catch (JsonException e)
                    {
                        Log.Error(e, "Json file is invalid: {Path}", file.Path);
                    }
                }
            }

            return default(JObject);
        }

        /// <summary>
        /// Parses the file contents as a Json array.
        /// Returns an empty array in case of a Json parser error.
        /// </summary>
        public static JArray ReadJsonArray(this IProjectFile file, JsonLoadSettings settings = null)
        {
            if (file.TryReadFile(out var stream))
            {
                using (var reader = new JsonTextReader(new StreamReader(stream)))
                {
                    try
                    {
                        return JArray.Load(reader, settings);
                    }
                    catch (JsonException e)
                    {
                        Log.Error(e, "Json file is invalid: {Path}", file.Path);
                    }
                }
            }

            return default(JArray);
        }

        public static XDocument ReadXml(this IProjectFile file, XmlReaderSettings readerSettings = null)
        {
            if (file.TryReadFile(out var stream))
            {
                using (stream)
                using (var reader = XmlReader.Create(stream, readerSettings ?? new XmlReaderSettings { IgnoreWhitespace = true }))
                {
                    try
                    {
                        return XDocument.Load(reader);
                    }
                    catch (XmlException e)
                    {
                        Log.Error(e, "Xml file is invalid: {Path}", file.Path);
                    }
                }
            }

            return default(XDocument);
        }

        public static string ReadText(this IProjectFile file)
        {
            if (file.TryReadFile(out var stream))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }

            return default(string);
        }

        /// <summary>
        /// Return the relative path of this <see cref="IProjectFile"/> in relation to the specified <see cref="IProjectFolder"/>.
        /// Uses a forward slash <c>/</c> as path separator.
        /// </summary>
        public static string GetRelativePath(this IProjectFile file, IProjectFolder folder)
            => new Uri(folder.BasePath + @"\")
                .MakeRelativeUri(new Uri(file.Path))
                .ToString();

    }
}
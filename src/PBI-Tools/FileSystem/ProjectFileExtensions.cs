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
        /// Parses the file contents as a Json token.
        /// Returns an empty object in case of a Json parser error.
        /// </summary>
        public static JToken ReadJsonToken(this IProjectFile file, JsonLoadSettings settings = null)
        {
            if (file.TryReadFile(out var stream))
            {
                using (var reader = new JsonTextReader(new StreamReader(stream)))
                {
                    try
                    {
                        return JToken.Load(reader, settings);
                    }
                    catch (JsonException e)
                    {
                        Log.Error(e, "Json file is invalid: {Path}", file.Path);
                    }
                }
            }

            return default;
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

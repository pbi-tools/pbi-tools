// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PbiTools.Utils
{
    public static class Resources
    {
        public static T GetEmbeddedResource<T>(string name, Func<Stream, T> transform, Assembly assembly = null)
        {
            using (var stream = GetEmbeddedResourceStream(name, assembly ?? Assembly.GetCallingAssembly()))
            {
                return transform(stream);
            }
        }

        public static string GetEmbeddedResourceString(string name, Encoding encoding = null, Assembly assembly = null)
        {
            using (var stream = GetEmbeddedResourceStream(name, assembly ?? Assembly.GetCallingAssembly()))
            using (var reader = new StreamReader(stream, encoding ?? Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public static T GetEmbeddedResourceFromString<T>(string name, Func<string, T> transform, Assembly assembly = null) =>
            GetEmbeddedResource<T>(name, stream =>
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return transform(reader.ReadToEnd());
                }
            }, assembly ?? Assembly.GetCallingAssembly());

        public static Stream GetEmbeddedResourceStream(string name, Assembly assembly = null)
        {
            var asm = assembly ?? Assembly.GetCallingAssembly();

            var resourceNames = asm.GetManifestResourceNames();
            var match = resourceNames.FirstOrDefault(n => n.EndsWith(name));
            if (match == null) throw new ArgumentException($"Embedded resource '{name}' not found.", nameof(name));

            return asm.GetManifestResourceStream(match);
        }

    }
}

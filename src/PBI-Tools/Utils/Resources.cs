// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PbiTools.Utils
{
    public static class Resources
    {
        public static T GetEmbeddedResource<T>(string name, Func<Stream, T> transform)
        {
            var asm = Assembly.GetCallingAssembly();

            using (var stream = GetEmbeddedResourceStream(name, asm))
            {
                return transform(stream);
            }
        }

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

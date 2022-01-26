// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;

namespace PbiTools.Tests
{
    using static PbiTools.Utils.Resources;

    public static class TestData
    {

        public const string GlobalPipe = "7f3b9dee-6ed9-4965-9caa-c1b33a5c34ad";
        
        /// Base64-encoded MashupPackage containing only an empty section.
        public static string MinimalMashupPackageBase64 => Convert.ToBase64String(MashupPackage.Value);
        //@"UEsDBBQAAgAIAKdYVkwLpf7AqwAAAPoAAAASABwAQ29uZmlnL1BhY2thZ2UueG1sIKIYACigFAAAAAAAAAAAAAAAAAAAAAAAAAAAAIWPQQ6CMBREr0K657cFS5R8SqILN5KYmBi3BCs0QjG0CHdz4ZG8giaKcedu5uUtZh63O6ZjU3tX1VndmoRwYMRTpmiP2pQJ6d3Jn5NU4jYvznmpvJdsbDzaY0Iq5y4xpcMwwBBC25U0YIzTQ7bZFZVqcvKV9X/Z18a63BSKSNy/x8gAhADBOINoxpFOGDNtpsxBQBgsImBIfzCu+tr1nZLK+Osl0qki/fyQT1BLAwQUAAIACACnWFZMD8rpq6QAAADpAAAAEwAcAFtDb250ZW50X1R5cGVzXS54bWwgohgAKKAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAbY5LDsIwDESvEnmfurBACDVlAdyAC0TB/Yjmo8ZF4WwsOBJXIG13iKVn5nnm83pXx2QH8aAx9t4p2BQlCHLG33rXKpi4kXs41tX1GSiKHHVRQcccDojRdGR1LHwgl53Gj1ZzPscWgzZ33RJuy3KHxjsmx5LnH1BXZ2r0NLC4pCyvtRkHcVpzc5UCpsS4yPiXsD95HcLQG83ZxCRtlHYhcRlefwFQSwMEFAACAAgAp1hWTCiKR7gOAAAAEQAAABMAHABGb3JtdWxhcy9TZWN0aW9uMS5tIKIYACigFAAAAAAAAAAAAAAAAAAAAAAAAAAAACtOTS7JzM9TCIbQhtYAUEsBAi0AFAACAAgAp1hWTAul/sCrAAAA+gAAABIAAAAAAAAAAAAAAAAAAAAAAENvbmZpZy9QYWNrYWdlLnhtbFBLAQItABQAAgAIAKdYVkwPyumrpAAAAOkAAAATAAAAAAAAAAAAAAAAAPcAAABbQ29udGVudF9UeXBlc10ueG1sUEsBAi0AFAACAAgAp1hWTCiKR7gOAAAAEQAAABMAAAAAAAAAAAAAAAAA6AEAAEZvcm11bGFzL1NlY3Rpb24xLm1QSwUGAAAAAAMAAwDCAAAAQwIAAAAA";

        public static byte[] MinimalMashupPackageBytes => MashupPackage.Value;



        private static readonly Lazy<byte[]> MashupPackage = new Lazy<byte[]>(BuildMinimalMashupPackage);

        private static byte[] BuildMinimalMashupPackage()
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var packageXml = zip.CreateEntry("Config/Package.xml");
                    using (var writer = new StreamWriter(packageXml.Open()))
                    {
                        writer.Write(
@"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
    <Version>2.55.5010.641</Version>
    <MinVersion>1.5.3296.0</MinVersion>
    <Culture>en-GB</Culture>
</Package>");
                    }

                    var contentTypesXml = zip.CreateEntry("[Content_Types].xml"); // must have written previous entry before opening new one, see https://stackoverflow.com/a/37533305/736263
                    using (var writer = new StreamWriter(contentTypesXml.Open()))
                    {
                        writer.Write(
@"<?xml version=""1.0"" encoding=""utf-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
    <Default Extension=""xml"" ContentType=""text/xml"" />
    <Default Extension=""m"" ContentType=""application/x-ms-m"" />
</Types>");
                    }

                    var section1 = zip.CreateEntry("Formulas/Section1.m");
                    using (var writer = new StreamWriter(section1.Open()))
                    {
                        writer.Write(
@"section Section1;");
                    }
                }

                return stream.ToArray();
            }
        }

        public static class AdventureWorks
        {
            internal static void WriteDatabaseJsonTo(string path)
            {
                using (var source = GetEmbeddedResourceStream("AdventureWorksDW2020.json"))
                using (var dest = File.Create(path))
                {
                    source.CopyTo(dest);
                }
            }
        }
    }
}

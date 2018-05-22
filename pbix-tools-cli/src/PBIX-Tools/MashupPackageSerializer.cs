using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace PbixTools
{
    public class MashupPackageSerializer
    {
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
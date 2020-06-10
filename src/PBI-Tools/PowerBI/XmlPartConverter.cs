using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.PowerBI.Packaging;

namespace PbiTools.PowerBI
{
    public class XmlPartConverter : IPowerBIPartConverter<XDocument>
    {
        private readonly Encoding _encoding;

        public XmlPartConverter() : this(Encoding.Unicode)
        {
        }

        public XmlPartConverter(Encoding encoding)
        {
            _encoding = encoding;
        }

        public XDocument FromPackagePart(IStreamablePowerBIPackagePartContent part)
        {
            if (part == null || (part.ContentType?.Contains("json") ?? false)) return default(XDocument);
            using (var reader = new StreamReader(part.GetStream(), _encoding))
            {
                return XDocument.Load(reader);  // TODO Error Handling
            }
        }

        public IStreamablePowerBIPackagePartContent ToPackagePart(XDocument content)
        {
            if (content == null) return new StreamablePowerBIPackagePartContent(default(string));
            return new StreamablePowerBIPackagePartContent(_encoding.GetBytes(content.ToString(SaveOptions.DisableFormatting)));
        }
    }
}
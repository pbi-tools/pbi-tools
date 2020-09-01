// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.Text;
using Microsoft.PowerBI.Packaging;

namespace PbiTools.PowerBI
{
    public class StringPartConverter : IPowerBIPartConverter<string>
    {
        private readonly Encoding _encoding;

        public StringPartConverter() : this(Encoding.Unicode)
        {
        }

        public StringPartConverter(Encoding encoding)
        {
            _encoding = encoding;
        }


        public string FromPackagePart(IStreamablePowerBIPackagePartContent part)
        {
            if (part == null) return default(string);
            using (var reader = new StreamReader(part.GetStream(), _encoding))
            {
                return reader.ReadToEnd();
            }
        }

        public IStreamablePowerBIPackagePartContent ToPackagePart(string content)
        {
            if (content == null) return new StreamablePowerBIPackagePartContent(default(string));
            return new StreamablePowerBIPackagePartContent(_encoding.GetBytes(content));
        }
    }
}
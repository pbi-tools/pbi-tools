// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.PowerBI.Packaging;

namespace PbiTools.PowerBI
{
    public class BytesPartConverter : IPowerBIPartConverter<byte[]>
    {
        public byte[] FromPackagePart(IStreamablePowerBIPackagePartContent part)
        {
            return PowerBIPackagingUtils.GetContentAsBytes(part, isOptional: true);
        }

        public IStreamablePowerBIPackagePartContent ToPackagePart(byte[] content)
        {
            return new StreamablePowerBIPackagePartContent(content);
        }
    }
}
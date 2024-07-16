// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Text;
using Microsoft.PowerBI.Api.Models;

namespace PbiTools.PowerBI;

public record CapacityInfo(Guid Id, string Description);

public static class PowerBIApiExtensions
{
    public static bool TryGetCapacityInfo(this Capacity capacity, out CapacityInfo info)
    {
        info = default;
        if (capacity == null || capacity.Id == default) return false;

        var description = new StringBuilder();

        if (!string.IsNullOrEmpty(capacity.Sku))
        {
            description.Append("[");
            description.Append(capacity.Sku);
            description.Append("]");
        }
        if (!string.IsNullOrEmpty(capacity.DisplayName))
        {
            description.Append(" \"");
            description.Append(capacity.DisplayName);
            description.Append("\"");
        }

        info = new CapacityInfo(capacity.Id, description.ToString());
        return true;
    }
}
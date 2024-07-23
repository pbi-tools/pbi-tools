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

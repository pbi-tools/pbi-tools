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

namespace PbiTools.PowerBI
{
    /// <summary>
    /// Converts back-and-forth between a native Power BI package part Stream
    /// and a specific representation of the underlying content as <see cref="T"/>.
    /// </summary>
    /// <typeparam name="T">A type that represents a Power BI package part content without any dependency on the Power BI API.</typeparam>
    // ReSharper disable once InconsistentNaming
    public interface IPowerBIPartConverter<T>
        where T : class
    {
        T FromPackagePart(Func<Stream> part, string contentType = null);
        Func<Stream> ToPackagePart(T content);
        Uri PartUri { get; }
        bool IsOptional { get; }
        string ContentType { get; }
    }

    public static class StreamExtensions
    {
        public static bool TryGetStream(this Func<Stream> part, out Stream stream)
        { 
            stream = (part == null) ? null : part();
            return (stream != null);
        }
    }
}
